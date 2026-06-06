using Ionic.Zip;
using System;
using System.IO;

namespace Shaiya_Duff_Updater
{
    public static class PatchExtractor
    {
        public static void ExtractZipSafe(string zipPath, string destination)
        {
            ExtractZipSafe(zipPath, destination, null);
        }

        public static void ExtractZipSafe(string zipPath, string destination, Action<int> progress)
        {
            Directory.CreateDirectory(destination);
            string root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())) root += Path.DirectorySeparatorChar;

            using (ZipFile zip = ZipFile.Read(zipPath))
            {
                foreach (ZipEntry entry in zip)
                {
                    if (string.IsNullOrWhiteSpace(entry.FileName)) continue;
                    string normalizedEntry = entry.FileName.Replace('\\', '/');
                    if (normalizedEntry.StartsWith("/") || normalizedEntry.Contains(":") || normalizedEntry.Contains("../") || normalizedEntry.Equals("..", StringComparison.Ordinal))
                        throw new InvalidDataException("Unsafe patch entry path: " + entry.FileName);

                    string target = Path.GetFullPath(Path.Combine(destination, entry.FileName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Unsafe patch entry path: " + entry.FileName);
                }

                int total = zip.Count;
                int done  = 0;
                foreach (ZipEntry entry in zip)
                {
                    entry.Extract(destination, ExtractExistingFileAction.OverwriteSilently);
                    done++;
                    if (progress != null && total > 0)
                        progress(done * 100 / total);
                }
            }
        }
    }
}

