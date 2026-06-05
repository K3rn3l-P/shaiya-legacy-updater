using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Shaiya_Duff_Updater
{
    public static class HashUtil
    {
        public static string Sha256File(string file)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(file))
            {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string Sha256String(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}

