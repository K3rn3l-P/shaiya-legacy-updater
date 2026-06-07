using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PatchMaker
{
    public static class EsahDecryptor
    {
        private const int HeaderSize = 64;
        private const int NonceSize  = 12;
        private const int TagSize    = 16;

        // key must be exactly 32 bytes (AES-256).
        // Throws InvalidOperationException with NTSTATUS on tag mismatch or bad input.
        public static byte[] Decrypt(byte[] esah, byte[] key)
        {
            if (esah == null) throw new ArgumentNullException("esah");
            if (key == null || key.Length != 32) throw new ArgumentException("AES key must be 32 bytes.");
            if (esah.Length < HeaderSize ||
                esah[0] != 'E' || esah[1] != 'S' || esah[2] != 'A' || esah[3] != 'H')
                throw new InvalidOperationException("Input non è ESAH (magic mancante).");

            ulong plainSize = BitConverter.ToUInt64(esah, 8);
            byte[] nonce = new byte[NonceSize];
            byte[] tag   = new byte[TagSize];
            Buffer.BlockCopy(esah, 16, nonce, 0, NonceSize);
            Buffer.BlockCopy(esah, 28, tag,   0, TagSize);

            int cipherLen = esah.Length - HeaderSize;
            byte[] cipher = new byte[cipherLen];
            Buffer.BlockCopy(esah, HeaderSize, cipher, 0, cipherLen);

            return AesGcmDecrypt(cipher, nonce, tag, key, (int)plainSize);
        }

        private static byte[] AesGcmDecrypt(byte[] cipher, byte[] nonce, byte[] tag, byte[] key, int plainSize)
        {
            IntPtr hAlg = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;
            byte[] tagBuf = (byte[])tag.Clone();
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

                byte[] plain = new byte[plainSize];
                using (var pIn    = new Pin(cipher))
                using (var pNonce = new Pin(nonce))
                using (var pTag   = new Pin(tagBuf))
                using (var pOut   = new Pin(plain))
                {
                    var auth = MakeAuth(pNonce.Ptr, NonceSize, pTag.Ptr, TagSize);
                    int w;
                    Check(Native.BCryptDecrypt(hKey, pIn.Ptr, cipher.Length, ref auth,
                                               IntPtr.Zero, 0, pOut.Ptr, plain.Length, out w, 0), "Dec");
                }
                return plain;
            }
            finally
            {
                if (hKey != IntPtr.Zero) Native.BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) Native.BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }

        private static void Check(int st, string op)
        {
            if (st < 0) throw new InvalidOperationException(op + " NTSTATUS=0x" + st.ToString("X8"));
        }

        private static Native.AUTH_INFO MakeAuth(IntPtr nonce, int nlen, IntPtr tag, int tlen) =>
            new Native.AUTH_INFO
            {
                cbSize        = Marshal.SizeOf(typeof(Native.AUTH_INFO)),
                dwInfoVersion = 1,
                pbNonce       = nonce, cbNonce = nlen,
                pbTag         = tag,   cbTag   = tlen,
            };

        private sealed class Pin : IDisposable
        {
            private GCHandle _h;
            public IntPtr Ptr { get; }
            public Pin(byte[] arr)
            {
                if (arr != null && arr.Length > 0)
                {
                    _h  = GCHandle.Alloc(arr, GCHandleType.Pinned);
                    Ptr = _h.AddrOfPinnedObject();
                }
            }
            public void Dispose() { if (_h.IsAllocated) _h.Free(); }
        }

        private static class Native
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct AUTH_INFO
            {
                public int    cbSize, dwInfoVersion;
                public IntPtr pbNonce;      public int cbNonce;
                public IntPtr pbAuthData;   public int cbAuthData;
                public IntPtr pbTag;        public int cbTag;
                public IntPtr pbMacContext; public int cbMacContext;
                public int    cbAAD;
                public long   cbData;
                public int    dwFlags;
            }

            [DllImport("bcrypt.dll", CharSet=CharSet.Unicode)]
            public static extern int BCryptOpenAlgorithmProvider(out IntPtr ph, string alg, string impl, int flags);
            [DllImport("bcrypt.dll")]
            public static extern int BCryptCloseAlgorithmProvider(IntPtr h, int flags);
            [DllImport("bcrypt.dll")]
            public static extern int BCryptSetProperty(IntPtr h, [MarshalAs(UnmanagedType.LPWStr)] string prop, byte[] val, int len, int flags);
            [DllImport("bcrypt.dll")]
            public static extern int BCryptGetProperty(IntPtr h, [MarshalAs(UnmanagedType.LPWStr)] string prop, out int val, int len, out int written, int flags);
            [DllImport("bcrypt.dll")]
            public static extern int BCryptGenerateSymmetricKey(IntPtr hAlg, out IntPtr phKey, byte[] obj, int objLen, byte[] secret, int sLen, int flags);
            [DllImport("bcrypt.dll")]
            public static extern int BCryptDestroyKey(IntPtr h);
            [DllImport("bcrypt.dll")]
            public static extern int BCryptDecrypt(IntPtr hKey, IntPtr pIn, int inLen, ref AUTH_INFO auth,
                                                   IntPtr iv, int ivLen, IntPtr pOut, int outLen, out int written, int flags);
        }
    }
}
