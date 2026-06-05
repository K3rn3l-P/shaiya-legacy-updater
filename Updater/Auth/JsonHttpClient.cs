using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web.Script.Serialization;

namespace Shaiya_Duff_Updater
{
    public static class JsonHttpClient
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        // SHA256 hex of the server certificate's public key bytes (GetPublicKey()).
        // Empty string = pinning disabled; only TLS 1.2+ chain validation is enforced.
        // Update this constant whenever the server certificate key changes.
        private const string PinnedCertPubKeyHex = "";

        static JsonHttpClient()
        {
            // Tls (1.0) e Tls11 esclusi: il server accetta solo TLS 1.2+.
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13; }
            catch { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }

            ServicePointManager.ServerCertificateValidationCallback = ValidateCertificate;
        }

        private static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (errors != SslPolicyErrors.None) return false;
            if (string.IsNullOrEmpty(PinnedCertPubKeyHex)) return true;

            byte[] keyBytes = certificate.GetPublicKey();
            using (SHA256 sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(keyBytes)).Replace("-", "").ToLowerInvariant();
                return string.Equals(actual, PinnedCertPubKeyHex, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static T Get<T>(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                return Serializer.Deserialize<T>(json);
            }
        }

        public static T Post<T>(string url, object body)
        {
            string json = Serializer.Serialize(body);
            byte[] data = Encoding.UTF8.GetBytes(json);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "application/json";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;
            req.ContentLength = data.Length;
            using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string response = sr.ReadToEnd();
                return Serializer.Deserialize<T>(response);
            }
        }

        public static string ToJson(object value) { return Serializer.Serialize(value); }
        public static T FromJson<T>(string value) { return Serializer.Deserialize<T>(value); }
    }
}

