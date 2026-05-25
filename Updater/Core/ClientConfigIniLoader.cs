using System;
using System.IO;

namespace Shaiya_Duff_Updater
{
    public static class ClientConfigIniLoader
    {
        public static void ApplyOverrides()
        {
            string path = AppPaths.InBase(AppConfig.ClientConfigFile);
            AppLogger.Debug("config", "Client config lookup path=" + path);
            if (!File.Exists(path))
            {
                AppLogger.Info("Client config file not found: " + path);
                return;
            }

            try
            {
                IniFile ini = IniFile.Load(path);
                bool oldSkip = AppConfig.SkipServerSelectionFromClientConfig;
                AppConfig.SkipServerSelectionFromClientConfig = ini.GetBool("ADVANCED", "SKIPSERVERSELECTION", AppConfig.SkipServerSelectionFromClientConfig);
                AppLogger.Info("Client config loaded: SKIPSERVERSELECTION=" + (AppConfig.SkipServerSelectionFromClientConfig ? "1" : "0"));
                AppLogger.Debug("config", "Client config applied. skipServerSelection old=" + (oldSkip ? "1" : "0") + " new=" + (AppConfig.SkipServerSelectionFromClientConfig ? "1" : "0"));
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Client CONFIG.INI could not be parsed. " + ex.Message);
            }
        }
    }
}

