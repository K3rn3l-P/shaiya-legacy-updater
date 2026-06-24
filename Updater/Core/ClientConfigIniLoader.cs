using System;
using System.IO;

namespace Shaiya_Duff_Updater
{
    public static class ClientConfigIniLoader
    {
        public static void ApplyOverrides()
        {
            // SKIPSERVERSELECTION is intentionally no longer honored: the game must ALWAYS go through
            // server selection. CONFIG.INI is left untouched; this loader is kept as a no-op hook in
            // case client-side overrides are needed again in the future.
            string path = AppPaths.InBase(AppConfig.ClientConfigFile);
            AppLogger.Debug("config", "Client config lookup path=" + path + " (SKIPSERVERSELECTION ignored; server selection is always shown).");
        }
    }
}

