using Ionic.Zip;
using System;
using System.IO;

namespace PatchMaker
{
    public class PatchBuilder
    {
        private readonly Action<string> _log;

        public PatchBuilder(Action<string> log) { _log = log; }

        public string Build(string workspacePath, int patchNumber, string description, string sahKeyHex, string deleteLstContent, bool encryptSah = true)
        {
            string dataDir = Path.Combine(workspacePath, "data");
            string outDir  = Path.Combine(workspacePath, "output");
            Directory.CreateDirectory(outDir);

            if (!Directory.Exists(dataDir))
                throw new DirectoryNotFoundException(L.ErrDataDirNotFound(dataDir));

            string rawSahPath = Path.Combine(workspacePath, "_build_data.sah");
            string rawSafPath = Path.Combine(workspacePath, "_build_data.saf");

            Log(L.AnalyzingData);
            Log(L.FilesDetected(SahSafBuilder.DescribeLayout(dataDir)));

            Log(L.GeneratingSah);
            SahSafBuilder.Build(dataDir, rawSahPath, rawSafPath);
            Log(L.SahSafSizes(new FileInfo(rawSahPath).Length / 1024, new FileInfo(rawSafPath).Length / 1024));

            string updateSah = Path.Combine(workspacePath, "update.sah");
            string updateSaf = Path.Combine(workspacePath, "update.saf");

            byte[] plainSah = File.ReadAllBytes(rawSahPath);
            if (!EsahEncryptor.IsPlainSah(plainSah))
                throw new InvalidDataException(L.ErrSahInvalid);

            if (encryptSah)
            {
                Log(L.EncryptingSah);
                byte[] key  = EsahEncryptor.ParseHexKey(sahKeyHex);
                byte[] esah = EsahEncryptor.Encrypt(plainSah, key);
                File.WriteAllBytes(updateSah, esah);
                Log(L.UpdateSahSize(esah.Length / 1024));
            }
            else
            {
                Log(L.SahPlainMode);
                File.WriteAllBytes(updateSah, plainSah);
                Log(L.UpdateSahSize(plainSah.Length / 1024));
            }

            File.Copy(rawSafPath, updateSaf, overwrite: true);
            Log(L.UpdateSafSize(new FileInfo(updateSaf).Length / 1024));

            try { File.Delete(rawSahPath); File.Delete(rawSafPath); } catch { }

            string safeName  = SanitizeDesc(description);
            string patchName = $"{patchNumber:D4}_{safeName}.patch";
            string patchPath = Path.Combine(outDir, patchName);

            Log(L.CreatingZip(patchName));
            if (File.Exists(patchPath)) File.Delete(patchPath);
            using (ZipFile zip = new ZipFile())
            {
                zip.AddFile(updateSah, "");
                zip.AddFile(updateSaf, "");

                if (!string.IsNullOrWhiteSpace(deleteLstContent))
                {
                    string tmpDel = Path.Combine(workspacePath, "delete.lst");
                    File.WriteAllText(tmpDel, deleteLstContent.Replace("\r\n", "\n").TrimEnd() + "\n");
                    zip.AddFile(tmpDel, "");
                }

                zip.Save(patchPath);
            }

            try { File.Delete(updateSah); File.Delete(updateSaf); } catch { }

            Log(L.PatchDone(patchName, new FileInfo(patchPath).Length / 1024));
            return patchPath;
        }

        private static string SanitizeDesc(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return "patch";
            var sb = new System.Text.StringBuilder();
            foreach (char c in desc.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                else if (c == ' ') sb.Append('_');
            }
            return sb.Length > 0 ? sb.ToString() : "patch";
        }

        private void Log(string msg) => _log?.Invoke(msg);
    }
}
