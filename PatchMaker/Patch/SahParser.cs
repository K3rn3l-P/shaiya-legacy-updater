using System;
using System.Collections.Generic;
using System.Text;

namespace PatchMaker
{
    public class SahEntry
    {
        public string Path   { get; set; }
        public uint   Size   { get; set; }
        public ulong  Offset { get; set; }
    }

    // Parses binary SAH archive index into a flat file list.
    // Format mirrors SahSafBuilder.WriteSah / WriteFolderToBuf / WriteFileToBuf.
    public static class SahParser
    {
        private const int HeaderSize = 51; // "SAH" + 4 zeros + 4 totalFiles + 40 padding

        public static List<SahEntry> Parse(byte[] sah)
        {
            if (sah == null || sah.Length < HeaderSize)
                throw new InvalidOperationException("SAH troppo corto.");
            if (sah[0] != 'S' || sah[1] != 'A' || sah[2] != 'H')
                throw new InvalidOperationException("Magic SAH mancante.");

            var result = new List<SahEntry>();
            int pos = HeaderSize;

            // Root folder header: nameLen=1, name="\0"
            int nameLen = ReadInt32(sah, ref pos);
            pos += nameLen; // skip name bytes (null-only for root)

            // Root files then root subfolders
            ReadFiles(sah, ref pos, "", result);
            ReadFolders(sah, ref pos, "", result);

            return result;
        }

        private static void ReadFiles(byte[] sah, ref int pos, string folderPath, List<SahEntry> result)
        {
            int count = ReadInt32(sah, ref pos);
            for (int i = 0; i < count; i++)
            {
                int nameLen = ReadInt32(sah, ref pos);
                string name = Encoding.ASCII.GetString(sah, pos, nameLen - 1); // exclude null
                pos += nameLen;
                ulong offset  = ReadUInt64(sah, ref pos);
                uint  size    = ReadUInt32(sah, ref pos);
                ReadUInt32(sah, ref pos); // version — unused

                string path = string.IsNullOrEmpty(folderPath) ? name : folderPath + "\\" + name;
                result.Add(new SahEntry { Path = path, Size = size, Offset = offset });
            }
        }

        private static void ReadFolders(byte[] sah, ref int pos, string parentPath, List<SahEntry> result)
        {
            int count = ReadInt32(sah, ref pos);
            for (int i = 0; i < count; i++)
            {
                int nameLen = ReadInt32(sah, ref pos);
                string name = Encoding.ASCII.GetString(sah, pos, nameLen - 1);
                pos += nameLen;

                string folderPath = string.IsNullOrEmpty(parentPath) ? name : parentPath + "\\" + name;

                ReadFiles(sah, ref pos, folderPath, result);
                ReadFolders(sah, ref pos, folderPath, result);
            }
        }

        private static int   ReadInt32 (byte[] b, ref int p) { var v = BitConverter.ToInt32 (b, p); p += 4; return v; }
        private static uint  ReadUInt32(byte[] b, ref int p) { var v = BitConverter.ToUInt32(b, p); p += 4; return v; }
        private static ulong ReadUInt64(byte[] b, ref int p) { var v = BitConverter.ToUInt64(b, p); p += 8; return v; }
    }
}
