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
        public string SahKeyHex { get; set; } = "";
        public string Language  { get; set; } = "It";

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
                        case "SahKeyHex":      s.SahKeyHex      = val; break;
                        case "Language":       s.Language       = val; break;
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
                    "SahKeyHex="      + SahKeyHex,
                    "Language="       + Language,
                });
            }
            catch { }
        }

        public const string DefaultGmSecurityServerPath = "C:/ShaiyaServer/PSM_Client/Bin/Data/GMSecurity.ini";

        public static string ParseKeyFromIniContent(string content)
        {
            if (content == null) return null;
            foreach (string line in content.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("SahKeyHex", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("SahCryptKeyHex", StringComparison.OrdinalIgnoreCase))
                {
                    int eq = trimmed.IndexOf('=');
                    if (eq >= 0) return trimmed.Substring(eq + 1).Trim();
                }
            }
            return null;
        }

        public static string ReadKeyFromGmSecurityIni(string iniPath)
        {
            if (!File.Exists(iniPath)) return null;
            return ParseKeyFromIniContent(File.ReadAllText(iniPath));
        }
    }
}
