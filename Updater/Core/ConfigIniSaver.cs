using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shaiya_Duff_Updater
{
    public static class ConfigIniSaver
    {
        // changes: key = "SECTION::KEY" (uppercase), value = raw string to write.
        // Existing keys are updated in-place; missing keys are appended at end of their section.
        public static void Save(string path, Dictionary<string, string> changes)
        {
            if (!File.Exists(path) || changes == null || changes.Count == 0) return;

            string[] lines = File.ReadAllLines(path);
            var written  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sb       = new StringBuilder();
            string section = "";

            foreach (string raw in lines)
            {
                string line    = raw.TrimEnd();
                string trimmed = line.Trim();

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    // Leaving current section — append any missing keys for it first.
                    AppendMissing(sb, section, changes, written);
                    section = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    sb.AppendLine(line);
                    continue;
                }

                int eq = trimmed.IndexOf('=');
                if (eq > 0 && !trimmed.StartsWith(";") && !trimmed.StartsWith("#"))
                {
                    string key     = trimmed.Substring(0, eq).Trim();
                    string dictKey = (section + "::" + key).ToUpperInvariant();
                    string newVal;
                    if (changes.TryGetValue(dictKey, out newVal))
                    {
                        sb.AppendLine(key + "=" + newVal);
                        written.Add(dictKey);
                        continue;
                    }
                }

                sb.AppendLine(line);
            }

            // End of file — append missing keys for last section.
            AppendMissing(sb, section, changes, written);

            File.WriteAllText(path, sb.ToString());
        }

        private static void AppendMissing(StringBuilder sb, string section,
            Dictionary<string, string> changes, HashSet<string> written)
        {
            if (string.IsNullOrEmpty(section)) return;
            string prefix = (section + "::").ToUpperInvariant();
            foreach (var kv in changes)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (written.Contains(kv.Key)) continue;
                // Extract original-case key name from the dict key (stored uppercase).
                string keyName = kv.Key.Substring(prefix.Length);
                sb.AppendLine(keyName + "=" + kv.Value);
                written.Add(kv.Key);
            }
        }
    }
}
