using System;
using System.IO;
using System.Linq;

namespace Shaiya_Duff_Updater
{
    public static class PatchApplier
    {
        public static void ApplyExtractedPatch(string extractedDir, PatchManifestItem item)
        {
            ApplyExtractedPatch(extractedDir, item, null);
        }

        public static void ApplyExtractedPatch(string extractedDir, PatchManifestItem item, Action<int> progress)
        {
            string patchName = item == null ? "manual" : (item.version.ToString("D4") + "_" + Path.GetFileName(item.file ?? "patch"));
            using (PatchTransaction tx = new PatchTransaction(patchName))
            {
                try
                {
                    ApplyExtractedPatchInternal(extractedDir, tx, progress);
                    tx.Complete();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public static void ApplyExtractedPatch(string extractedDir)
        {
            ApplyExtractedPatch(extractedDir, null, null);
        }

        private static void ApplyExtractedPatchInternal(string extractedDir, PatchTransaction tx, Action<int> progress)
        {
            string updateSah = Path.Combine(extractedDir, "update.sah");
            string updateSaf = Path.Combine(extractedDir, "update.saf");
            string deleteLst = Path.Combine(extractedDir, "delete.lst");

            bool hasSah = File.Exists(updateSah) && File.Exists(updateSaf);
            if (hasSah)
            {
                if (progress != null) progress(0);
                ApplySahPatch(updateSah, updateSaf, tx);
                if (progress != null) progress(50);
                CopyPatchFiles(extractedDir, AppPaths.BaseDirectory, tx, delegate(int pct) { if (progress != null) progress(50 + pct / 2); });
            }
            else
            {
                CopyPatchFiles(extractedDir, AppPaths.BaseDirectory, tx, progress);
            }

            if (File.Exists(deleteLst))
                ApplyDeleteList(deleteLst, tx);
        }

        private static void ApplyDeleteList(string deleteLstPath, PatchTransaction tx)
        {
            string[] lines = File.ReadAllLines(deleteLstPath);
            foreach (string rawLine in lines)
            {
                string rel = rawLine.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                if (string.IsNullOrWhiteSpace(rel)) continue;

                string target;
                try { target = SafeTarget(AppPaths.BaseDirectory, rel); }
                catch (Exception ex) { AppLogger.PatchWarn("delete.lst skipped unsafe path '" + rel + "': " + ex.Message); continue; }

                if (!File.Exists(target))
                {
                    AppLogger.PatchWarn("delete.lst target not found (skipped): " + rel);
                    continue;
                }

                tx.BackupFile(target);
                File.Delete(target);
                AppLogger.Patch("Deleted (delete.lst): " + rel);
            }
        }

        private static void ApplySahPatch(string updateSah, string updateSaf, PatchTransaction tx)
        {
            string dataSah = AppPaths.InBase("data.sah");
            string dataSaf = AppPaths.InBase("data.saf");
            if (!File.Exists(dataSah) || !File.Exists(dataSaf)) throw new FileNotFoundException("data.sah/data.saf is missing.");

            tx.BackupFile(dataSah);
            tx.BackupFile(dataSaf);

            AppLogger.Patch("SAH patch started. data.sah state=" + SahCrypt.GetState(dataSah) + ", update.sah state=" + SahCrypt.GetState(updateSah));

            if (AppConfig.SahCryptEnabled)
            {
                SahCrypt.UnlockIfNeeded(dataSah, null);

                if (AppConfig.SahCryptUnlockPatchSah)
                    SahCrypt.UnlockIfNeeded(updateSah, null);
            }

            SAH client = new SAH(dataSah);
            SAH patch = new SAH(updateSah);
            if (!client.OK) throw new InvalidDataException("data.sah could not be read.");
            if (!patch.OK) throw new InvalidDataException("update.sah could not be read.");
            client.MergeWithPatch(patch);

            if (AppConfig.SahCryptEnabled && AppConfig.SahCryptProtectDataSahAfterPatch)
            {
                SahCrypt.ProtectIfNeeded(dataSah, null);
                if (SahCrypt.GetState(dataSah) != SahCryptState.EncryptedEsah)
                    throw new InvalidDataException("ProtectDataSahAfterPatch=true, but data.sah is not ESAH encrypted after patch.");
            }

            AppLogger.Patch("SAH patch completed. data.sah state=" + SahCrypt.GetState(dataSah));
        }

        private static void CopyPatchFiles(string source, string destination, PatchTransaction tx, Action<int> progress)
        {
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string rel = GetRelativePath(source, dir);
                Directory.CreateDirectory(SafeTarget(destination, rel));
            }

            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                .Where(f => { string n = Path.GetFileName(f); return !n.Equals("update.sah", StringComparison.OrdinalIgnoreCase) && !n.Equals("update.saf", StringComparison.OrdinalIgnoreCase) && !n.Equals("delete.lst", StringComparison.OrdinalIgnoreCase); })
                .ToArray();
            int total = files.Length;
            int done  = 0;
            foreach (string file in files)
            {
                string rel    = GetRelativePath(source, file);
                string target = SafeTarget(destination, rel);
                tx.BackupFile(target);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                AppLogger.Patch("Copied patch file: " + rel);
                done++;
                if (progress != null && total > 0)
                    progress(done * 100 / total);
            }
        }

        public static string SafeTarget(string destination, string relativePath)
        {
            if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("Patch path is rooted: " + relativePath);
            string root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())) root += Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(Path.Combine(destination, relativePath));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Patch target is outside client folder: " + relativePath);
            return target;
        }

        private static string GetRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendSlash(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendSlash(string path) { return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar; }
    }
}

