using System;
using System.IO;

namespace PatchMaker
{
    public class PatchMakerSettings
    {
        private static readonly string SettingsFile = Path.Combine(
            Path.GetDirectoryName(typeof(PatchMakerSettings).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory,
            "patchmaker.ini");

        public string SshHost { get; set; } = "10.10.5.91";
        public string SshUser { get; set; } = "administrator";
        public string SshPassword { get; set; } = "";
        public string SshPatchesPath { get; set; } = "/C:/xampp/htdocs/Updater/Patches";
        public string SshHtdocsPath { get; set; } = "/C:/xampp/htdocs/Updater";
        public string WorkspacePath { get; set; } = "";
        public string SahKeyHex    { get; set; } = "";
        public string PatchKeyHex  { get; set; } = "";
        public bool   EncryptSah   { get; set; } = true;
        public bool   EncryptPatch { get; set; } = true;
        public string Language     { get; set; } = "It";
        public string GmSecurityIniPath  { get; set; } = DefaultGmSecurityServerPath;
        public string PsLoginServiceName { get; set; } = "ps_login";

        public string ResolvedWorkspace =>
            string.IsNullOrWhiteSpace(WorkspacePath)
                ? Path.Combine(Path.GetDirectoryName(typeof(PatchMakerSettings).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory, "workspace")
                : WorkspacePath;

        public static PatchMakerSettings Load()
        {
            var s = new PatchMakerSettings();
            if (!File.Exists(SettingsFile)) return s;
            try
            {
                foreach (string line in File.ReadAllLines(SettingsFile))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 1) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "SshHost":        s.SshHost        = val; break;
                        case "SshUser":        s.SshUser        = val; break;
                        case "SshPassword":    s.SshPassword    = val; break;
                        case "SshPatchesPath": s.SshPatchesPath = val; break;
                        case "SshHtdocsPath":  s.SshHtdocsPath  = val; break;
                        case "WorkspacePath":  s.WorkspacePath  = val; break;
                        case "SahKeyHex":   s.SahKeyHex   = val; break;
                        case "PatchKeyHex": s.PatchKeyHex = val; break;
                        case "EncryptSah":   s.EncryptSah   = !val.Equals("False", StringComparison.OrdinalIgnoreCase); break;
                        case "EncryptPatch": s.EncryptPatch = !val.Equals("False", StringComparison.OrdinalIgnoreCase); break;
                        case "Language":         s.Language         = val; break;
                        case "GmSecurityIniPath":  s.GmSecurityIniPath  = val; break;
                        case "PsLoginServiceName": s.PsLoginServiceName = val; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(SettingsFile, new[]
                {
                    "SshHost="        + SshHost,
                    "SshUser="        + SshUser,
                    "SshPassword="    + SshPassword,
                    "SshPatchesPath=" + SshPatchesPath,
                    "SshHtdocsPath="  + SshHtdocsPath,
                    "WorkspacePath="  + WorkspacePath,
                    "SahKeyHex="   + SahKeyHex,
                    "PatchKeyHex=" + PatchKeyHex,
                    "EncryptSah="   + EncryptSah,
                    "EncryptPatch=" + EncryptPatch,
                    "Language="          + Language,
                    "GmSecurityIniPath=" + GmSecurityIniPath,
                    "PsLoginServiceName="+ PsLoginServiceName,
                });
            }
            catch { }
        }

        public const string DefaultGmSecurityServerPath = "C:/ShaiyaServer/PSM_Client/Bin/Data/GMSecurity.ini";

        // Ritorna (sahKeyHex, patchKeyHex) letti dal contenuto di GMSecurity.ini.
        public static (string sahKey, string patchKey) ParseKeysFromIniContent(string content)
        {
            string sah = null, patch = null;
            if (content == null) return (null, null);
            foreach (string line in content.Split('\n'))
            {
                string trimmed = line.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq < 1) continue;
                string k = trimmed.Substring(0, eq).Trim();
                string v = trimmed.Substring(eq + 1).Trim();
                if (k.Equals("SahKeyHex", StringComparison.OrdinalIgnoreCase) ||
                    k.Equals("SahCryptKeyHex", StringComparison.OrdinalIgnoreCase))
                    sah = v;
                else if (k.Equals("PatchKeyHex", StringComparison.OrdinalIgnoreCase))
                    patch = v;
            }
            return (sah, patch);
        }

        public static string ParseKeyFromIniContent(string content)
        {
            return ParseKeysFromIniContent(content).sahKey;
        }

        public static string ReadKeyFromGmSecurityIni(string iniPath)
        {
            if (!File.Exists(iniPath)) return null;
            return ParseKeyFromIniContent(File.ReadAllText(iniPath));
        }
    }
}
