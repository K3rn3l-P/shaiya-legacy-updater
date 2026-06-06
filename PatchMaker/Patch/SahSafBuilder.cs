using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PatchMaker
{
    // Builds update.sah + update.saf from a data/ directory tree.
    // Format matches Shaiya SAH read/write logic in Updater/SAH.cs.
    public static class SahSafBuilder
    {
        public static void Build(string dataDir, string outputSahPath, string outputSafPath)
        {
            if (!Directory.Exists(dataDir))
                throw new DirectoryNotFoundException(L.ErrDataDirNotFound(dataDir));

            var safStream = new MemoryStream();
            var root = new SahFolder("");
            WalkDir(dataDir, dataDir, root, safStream);

            byte[] sahBytes = WriteSah(root);
            File.WriteAllBytes(outputSahPath, sahBytes);
            File.WriteAllBytes(outputSafPath, safStream.ToArray());
        }

        private static void WalkDir(string baseDir, string currentDir, SahFolder folder, MemoryStream saf)
        {
            // Files first (sorted for determinism)
            string[] files = Directory.GetFiles(currentDir);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string filePath in files)
            {
                byte[] data = File.ReadAllBytes(filePath);
                ulong start = (ulong)saf.Position;
                saf.Write(data, 0, data.Length);
                folder.Files.Add(new SahFile
                {
                    Name    = Path.GetFileName(filePath),
                    Start   = start,
                    Length  = (uint)data.Length,
                    Version = 0,
                });
            }

            // Subfolders (sorted)
            string[] dirs = Directory.GetDirectories(currentDir);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            foreach (string subDir in dirs)
            {
                var sub = new SahFolder(Path.GetFileName(subDir));
                folder.Folders.Add(sub);
                WalkDir(baseDir, subDir, sub, saf);
            }
        }

        private static byte[] WriteSah(SahFolder root)
        {
            int totalFiles = CountFiles(root);
            var buf = new List<byte>(1024 * 64);

            // Header (51 bytes)
            buf.AddRange(Encoding.ASCII.GetBytes("SAH"));
            buf.AddRange(BitConverter.GetBytes(0));           // 4 zeros
            buf.AddRange(BitConverter.GetBytes(totalFiles));  // file count
            buf.AddRange(new byte[40]);                       // padding

            // Root folder: name = one null byte (len=1)
            buf.AddRange(BitConverter.GetBytes(1));
            buf.Add(0);

            // Root files
            buf.AddRange(BitConverter.GetBytes(root.Files.Count));
            foreach (var f in root.Files)
                WriteFileToBuf(f, buf);

            // Root subfolders
            buf.AddRange(BitConverter.GetBytes(root.Folders.Count));
            foreach (var sub in root.Folders)
                WriteFolderToBuf(sub, buf);

            // Trailing 8 zeros
            buf.AddRange(new byte[8]);

            return buf.ToArray();
        }

        private static void WriteFolderToBuf(SahFolder folder, List<byte> buf)
        {
            buf.AddRange(BitConverter.GetBytes(folder.Name.Length + 1));
            buf.AddRange(Encoding.ASCII.GetBytes(folder.Name));
            buf.Add(0); // null terminator

            buf.AddRange(BitConverter.GetBytes(folder.Files.Count));
            foreach (var f in folder.Files)
                WriteFileToBuf(f, buf);

            buf.AddRange(BitConverter.GetBytes(folder.Folders.Count));
            foreach (var sub in folder.Folders)
                WriteFolderToBuf(sub, buf);
        }

        private static void WriteFileToBuf(SahFile f, List<byte> buf)
        {
            buf.AddRange(BitConverter.GetBytes(f.Name.Length + 1));
            buf.AddRange(Encoding.ASCII.GetBytes(f.Name));
            buf.Add(0);
            buf.AddRange(BitConverter.GetBytes(f.Start));
            buf.AddRange(BitConverter.GetBytes(f.Length));
            buf.AddRange(BitConverter.GetBytes(f.Version));
        }

        private static int CountFiles(SahFolder folder)
        {
            int n = folder.Files.Count;
            foreach (var sub in folder.Folders)
                n += CountFiles(sub);
            return n;
        }

        public static string DescribeLayout(string dataDir)
        {
            if (!Directory.Exists(dataDir)) return L.ErrDataNotFound_Describe;
            var sb = new StringBuilder();
            DescribeDir(dataDir, dataDir, sb, "  ");
            return sb.Length > 0 ? sb.ToString() : L.LabelEmpty;
        }

        private static void DescribeDir(string baseDir, string dir, StringBuilder sb, string indent)
        {
            foreach (string f in Directory.GetFiles(dir))
            {
                string rel = f.Substring(baseDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                sb.AppendLine(indent + rel + "  [" + new FileInfo(f).Length / 1024 + " KB]");
            }
            foreach (string d in Directory.GetDirectories(dir))
            {
                sb.AppendLine(indent + Path.GetFileName(d) + "/");
                DescribeDir(baseDir, d, sb, indent + "  ");
            }
        }
    }

    internal class SahFolder
    {
        public string Name;
        public List<SahFile> Files   = new List<SahFile>();
        public List<SahFolder> Folders = new List<SahFolder>();
        public SahFolder(string name) { Name = name; }
    }

    internal class SahFile
    {
        public string Name;
        public ulong  Start;
        public uint   Length;
        public uint   Version;
    }
}
