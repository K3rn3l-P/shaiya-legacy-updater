using System;
using System.Collections.Generic;
using System.Text;

namespace PatchMaker
{
    internal static class SahMerger
    {
        public static SahFolder Parse(byte[] data)
        {
            if (data.Length < 60 || Encoding.ASCII.GetString(data, 0, 3) != "SAH")
                throw new InvalidOperationException("Not a valid plain SAH binary");
            int offset = 56;
            return ReadFolderContent("", data, ref offset);
        }

        public static byte[] Serialize(SahFolder root)
        {
            int total = 0;
            var content = new List<byte>();
            WriteFilesAndFolders(root, content, ref total);

            var buf = new List<byte>();
            buf.AddRange(Encoding.ASCII.GetBytes("SAH"));
            buf.AddRange(BitConverter.GetBytes(0));
            buf.AddRange(BitConverter.GetBytes(total));
            buf.AddRange(new byte[40]);
            buf.AddRange(BitConverter.GetBytes(1));
            buf.Add(0x00);
            buf.AddRange(content);
            buf.AddRange(new byte[8]);
            return buf.ToArray();
        }

        public static void Merge(SahFolder master, SahFolder patch, ref ulong safSize)
        {
            MergeFolders(master, patch, ref safSize);
        }

        // ─── private ─────────────────────────────────────────────────

        private static SahFolder ReadFolderContent(string name, byte[] data, ref int offset)
        {
            var folder = new SahFolder(name);
            int fc = BitConverter.ToInt32(data, offset); offset += 4;
            for (int i = 0; i < fc; i++)
            {
                int fnLen = BitConverter.ToInt32(data, offset); offset += 4;
                string fn = Encoding.ASCII.GetString(data, offset, fnLen).TrimEnd('\0'); offset += fnLen;
                ulong start  = BitConverter.ToUInt64(data, offset); offset += 8;
                uint  len    = BitConverter.ToUInt32(data, offset); offset += 4;
                uint  ver    = BitConverter.ToUInt32(data, offset); offset += 4;
                folder.Files.Add(new SahFile { Name = fn, Start = start, Length = len, Version = ver });
            }
            int dc = BitConverter.ToInt32(data, offset); offset += 4;
            for (int i = 0; i < dc; i++)
                folder.Folders.Add(ReadFolderNode(data, ref offset));
            return folder;
        }

        private static SahFolder ReadFolderNode(byte[] data, ref int offset)
        {
            int nameLen = BitConverter.ToInt32(data, offset); offset += 4;
            string name = Encoding.ASCII.GetString(data, offset, nameLen).TrimEnd('\0'); offset += nameLen;
            return ReadFolderContent(name, data, ref offset);
        }

        private static void WriteFilesAndFolders(SahFolder folder, List<byte> buf, ref int total)
        {
            total += folder.Files.Count;
            buf.AddRange(BitConverter.GetBytes(folder.Files.Count));
            foreach (var f in folder.Files) WriteFileEntry(f, buf);
            buf.AddRange(BitConverter.GetBytes(folder.Folders.Count));
            foreach (var sub in folder.Folders) WriteFolderNode(sub, buf, ref total);
        }

        private static void WriteFileEntry(SahFile f, List<byte> buf)
        {
            string name = f.Name + "\0";
            buf.AddRange(BitConverter.GetBytes(name.Length));
            buf.AddRange(Encoding.ASCII.GetBytes(name));
            buf.AddRange(BitConverter.GetBytes(f.Start));
            buf.AddRange(BitConverter.GetBytes(f.Length));
            buf.AddRange(BitConverter.GetBytes(f.Version));
        }

        private static void WriteFolderNode(SahFolder folder, List<byte> buf, ref int total)
        {
            string name = folder.Name + "\0";
            buf.AddRange(BitConverter.GetBytes(name.Length));
            buf.AddRange(Encoding.ASCII.GetBytes(name));
            WriteFilesAndFolders(folder, buf, ref total);
        }

        private static void MergeFolders(SahFolder master, SahFolder patch, ref ulong safSize)
        {
            foreach (SahFile pf in patch.Files)
            {
                SahFile mf = master.Files.Find(x => string.Equals(x.Name, pf.Name, StringComparison.OrdinalIgnoreCase));
                if (mf != null)
                {
                    if (mf.Length < pf.Length)
                    {
                        mf.Start  = safSize;
                        safSize  += pf.Length;
                    }
                    mf.Length = pf.Length;
                }
                else
                {
                    master.Files.Add(new SahFile { Name = pf.Name, Start = safSize, Length = pf.Length, Version = 0 });
                    safSize += pf.Length;
                }
            }
            foreach (SahFolder pFolder in patch.Folders)
            {
                SahFolder mFolder = master.Folders.Find(x => string.Equals(x.Name, pFolder.Name, StringComparison.OrdinalIgnoreCase));
                if (mFolder == null)
                {
                    mFolder = new SahFolder(pFolder.Name);
                    master.Folders.Add(mFolder);
                }
                MergeFolders(mFolder, pFolder, ref safSize);
            }
        }
    }
}
