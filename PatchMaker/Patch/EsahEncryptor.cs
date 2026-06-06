using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PatchMaker
{
    // Standalone ESAH AES-256-GCM encryptor — key supplied explicitly, no AppConfig dependency.
    // Format matches SahCrypt.cs: 64-byte header (ESAH magic, version, flags, plainSize, nonce, tag) + ciphertext.
    public static class EsahEncryptor
    {
        private const int HeaderSize = 64;
        private const int NonceSize  = 12;
        private const int TagSize    = 16;

        private static readonly byte[] PlainMagic     = Encoding.ASCII.GetBytes("SAH");
        private static readonly byte[] EncryptedMagic = Encoding.ASCII.GetBytes("ESAH");

        public static bool IsPlainSah(byte[] data) =>
            data != null && data.Length >= 3 &&
            data[0] == (byte)'S' && data[1] == (byte)'A' && data[2] == (byte)'H';

        public static bool IsEsah(byte[] data) =>
            data != null && data.Length >= 4 &&
            data[0] == (byte)'E' && data[1] == (byte)'S' && data[2] == (byte)'A' && data[3] == (byte)'H';

        // key must be exactly 32 bytes.
        public static byte[] Encrypt(byte[] plain, byte[] key)
        {
            if (plain == null) throw new ArgumentNullException("plain");
            if (key == null || key.Length != 32) throw new ArgumentException("AES key must be 32 bytes.");
            if (!IsPlainSah(plain)) throw new InvalidOperationException("Input is not a plain SAH file.");

            byte[] nonce = new byte[NonceSize];
            BCryptRandom(nonce);

            byte[] tag;
            byte[] cipher = AesGcmEncrypt(plain, nonce, key, out tag);

            byte[] output = new byte[HeaderSize + cipher.Length];
            Buffer.BlockCopy(EncryptedMagic, 0, output, 0, 4);
            WriteU16(output, 4, 1);                         // version
            WriteU16(output, 6, 1);                         // flags = AES-256-GCM
            WriteU64(output, 8, (ulong)plain.Length);       // plainSize
            Buffer.BlockCopy(nonce, 0, output, 16, NonceSize);
            Buffer.BlockCopy(tag,   0, output, 28, TagSize);
            Buffer.BlockCopy(cipher, 0, output, HeaderSize, cipher.Length);
            return output;
        }

        public static byte[] ParseHexKey(string hex)
        {
            if (hex == null) throw new ArgumentNullException("hex");
            hex = hex.Replace(" ", "").Replace("-", "").Trim();
            if (hex.Length != 64) throw new InvalidOperationException("SahKeyHex must be 64 hex characters.");
            byte[] key = new byte[32];
            for (int i = 0; i < 32; i++)
                key[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return key;
        }

        private static void WriteU16(byte[] buf, int off, ushort v) { buf[off] = (byte)v; buf[off+1] = (byte)(v>>8); }
        private static void WriteU64(byte[] buf, int off, ulong v)  { for (int i=0;i<8;i++) buf[off+i]=(byte)(v>>(8*i)); }

        private static void BCryptRandom(byte[] buf)
        {
            int st = Native.BCryptGenRandom(IntPtr.Zero, buf, buf.Length, Native.BCRYPT_USE_SYSTEM_PREFERRED_RNG);
            if (st < 0) throw new CryptographicException("BCryptGenRandom failed. NTSTATUS=0x" + st.ToString("X8"));
        }

        private static byte[] AesGcmEncrypt(byte[] plain, byte[] nonce, byte[] key, out byte[] tag)
        {
            IntPtr hAlg = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;
            byte[] tagBuf = new byte[TagSize];
            byte[] keyObj;

            try
            {
                Check(Native.BCryptOpenAlgorithmProvider(out hAlg, "AES", null, 0), "OpenAlg");
                byte[] mode = Encoding.Unicode.GetBytes("ChainingModeGCM\0");
                Check(Native.BCryptSetProperty(hAlg, "ChainingMode", mode, mode.Length, 0), "SetGCM");
                int objLen, written;
                Check(Native.BCryptGetProperty(hAlg, "ObjectLength", out objLen, 4, out written, 0), "GetObjLen");
                keyObj = new byte[objLen];
                Check(Native.BCryptGenerateSymmetricKey(hAlg, out hKey, keyObj, objLen, key, key.Length, 0), "GenKey");

                using (var pIn    = new Pin(plain))
                using (var pNonce = new Pin(nonce))
                using (var pTag   = new Pin(tagBuf))
                {
                    var auth = MakeAuth(pNonce.Ptr, NonceSize, pTag.Ptr, TagSize);
                    int cLen;
                    Check(Native.BCryptEncrypt(hKey, pIn.Ptr, plain.Length, ref auth, IntPtr.Zero, 0, IntPtr.Zero, 0, out cLen, 0), "EncSize");
                    byte[] cipher = new byte[cLen];
                    using (var pOut = new Pin(cipher))
                    {
                        auth = MakeAuth(pNonce.Ptr, NonceSize, pTag.Ptr, TagSize);
                        int w;
                        Check(Native.BCryptEncrypt(hKey, pIn.Ptr, plain.Length, ref auth, IntPtr.Zero, 0, pOut.Ptr, cLen, out w, 0), "Enc");
                    }
                    tag = tagBuf;
                    return cipher;
                }
            }
            finally
            {
                if (hKey != IntPtr.Zero) Native.BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) Native.BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }

        private static void Check(int st, string op) { if (st < 0) throw new InvalidOperationException(op + " NTSTATUS=0x" + st.ToString("X8")); }

        private static Native.AUTH_INFO MakeAuth(IntPtr nonce, int nlen, IntPtr tag, int tlen) =>
            new Native.AUTH_INFO
            {
                cbSize       = Marshal.SizeOf(typeof(Native.AUTH_INFO)),
                dwInfoVersion = 1,
                pbNonce      = nonce, cbNonce = nlen,
                pbTag        = tag,   cbTag   = tlen,
            };

        private sealed class Pin : IDisposable
        {
            private GCHandle _h;
            public IntPtr Ptr { get; }
            public Pin(byte[] arr) { if (arr != null && arr.Length > 0) { _h = GCHandle.Alloc(arr, GCHandleType.Pinned); Ptr = _h.AddrOfPinnedObject(); } }
            public void Dispose() { if (_h.IsAllocated) _h.Free(); }
        }

        private static class Native
        {
            public const int BCRYPT_USE_SYSTEM_PREFERRED_RNG = 2;
            [StructLayout(LayoutKind.Sequential)]
            public struct AUTH_INFO
            {
                public int    cbSize, dwInfoVersion;
                public IntPtr pbNonce;   public int cbNonce;
                public IntPtr pbAuthData; public int cbAuthData;
                public IntPtr pbTag;     public int cbTag;
                public IntPtr pbMacContext; public int cbMacContext;
                public int    cbAAD;
                public long   cbData;
                public int    dwFlags;
            }
            [DllImport("bcrypt.dll")] public static extern int BCryptGenRandom(IntPtr hAlg, byte[] buf, int len, int flags);
            [DllImport("bcrypt.dll", CharSet=CharSet.Unicode)] public static extern int BCryptOpenAlgorithmProvider(out IntPtr ph, string alg, string impl, int flags);
            [DllImport("bcrypt.dll")] public static extern int BCryptCloseAlgorithmProvider(IntPtr h, int flags);
            [DllImport("bcrypt.dll")] public static extern int BCryptSetProperty(IntPtr h, [MarshalAs(UnmanagedType.LPWStr)] string prop, byte[] val, int len, int flags);
            [DllImport("bcrypt.dll")] public static extern int BCryptGetProperty(IntPtr h, [MarshalAs(UnmanagedType.LPWStr)] string prop, out int val, int len, out int written, int flags);
            [DllImport("bcrypt.dll")] public static extern int BCryptGenerateSymmetricKey(IntPtr hAlg, out IntPtr phKey, byte[] obj, int objLen, byte[] secret, int sLen, int flags);
            [DllImport("bcrypt.dll")] public static extern int BCryptDestroyKey(IntPtr h);
            [DllImport("bcrypt.dll")] public static extern int BCryptEncrypt(IntPtr hKey, IntPtr pIn, int inLen, ref AUTH_INFO auth, IntPtr iv, int ivLen, IntPtr pOut, int outLen, out int written, int flags);
        }
    }
}
