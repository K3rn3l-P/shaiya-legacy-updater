using System;
using System.IO;
using System.Net;

namespace Shaiya_Duff_Updater
{
    public static class AppConfig
    {
        private static bool _loaded;

        // Only this bootstrap URL is compiled into the launcher.
        // All update/auth/link/SahCrypt behavior is loaded from server-side launcher.ini.
        public static string BaseUrl = "https://duff.pinto-lime.ts.net/Updater/";

        public static string ConfigFile = "launcher.php";
        public static string ManifestFile = "manifest.json";
        public static string WebsiteUrl = "https://duff.pinto-lime.ts.net/";
        public static string RegisterUrl = "https://duff.pinto-lime.ts.net/register";
        public static string DiscordUrl = "";
        public static string NewsUrl = "";

        public static string GameExecutable = "game.exe";
        public static string GameArguments = "start game";
        public static bool RequireOriginalClientFiles = false;

        // LocalLauncherVersion is intentionally compiled. The server should use MinLauncherVersion/launcherVersion.
        public static int LocalLauncherVersion = 1;
        public static int MinLauncherVersion = 1;
        public static bool ForceUpdate = false;
        public static bool RepairEnabled = true;
        public static bool SelfUpdateEnabled = true;
        public static bool BlockPatchWhenGameRunning = true;

        public static bool AutoSelectServer = true;
        public static int AutoSelectServerDelayMs = 3000;
        public static int AutoSelectServerIndex = 0;
        public static int AutoSelectServerRetries = 30;
        public static int AutoSelectServerRetryDelayMs = 500;
        public static int AutoSelectServerObjectMissingAssumeSkipAttempts = 6;

        // Client CONFIG.INI override. When enabled, game skips server selection screen itself.
        public static string ClientConfigFile = "CONFIG.INI";
        public static bool SkipServerSelectionFromClientConfig = false;

        // TCP transition detection defaults (login -> game).
        public static int LoginTcpPort = 30800;
        public static int GameTcpPort = 30810;
        public static int TcpPortTransitionDelayMs = 2000;
        public static int TcpPortTransitionTimeoutMs = 30000;
        public static int SkipServerSelectionTcpTimeoutMs = 8000;

        // Keeps login/server-select screens out of sight while the normal game flow runs.
        // The loading state is drawn inside the launcher UI; no separate overlay window is created.
        public static bool HideLoginScreens = true;
        public static string LoginOverlayText = "Connecting to the server...";
        public static string LoginOverlaySubText = "Preparing character selection. Please wait.";
        public static int LoginOverlayRevealDelayMs = 6000;
        public static int LoginOverlayFallbackRevealDelayMs = 1000;
        public static int SkipServerSelectionRevealDelayMs = 2000;
        public static int LoginFlowPatchRestoreDelayMs = 2000;
        public static bool AutoSelectRetryWhenServerListMissing = true;
        public static int AutoSelectRetryDelayMs = 1500;

        // Keeps the real game window out of sight while the launcher loading panel is shown.
        // Default mode is Offscreen because fully hiding/minimizing some D3D clients can break device init.
        public static bool HideGameWindowUntilReady = true;
        public static string GameWindowHideMode = "Offscreen"; // Offscreen or Hide
        public static int GameWindowHideWaitMs = 8000;
        public static int GameWindowOffscreenX = -32000;
        public static int GameWindowOffscreenY = -32000;

        public static int DownloadRetryCount = 3;
        public static int DownloadTimeoutSeconds = 30;
        public static bool KeepSuccessfulBackups = false;
        public static string MaintenanceMessage = "Server is under maintenance.";

        public static string ManifestSha256 = "";

        public static bool SahCryptEnabled = true;
        public static bool SahCryptUnlockPatchSah = true;
        public static bool SahCryptProtectDataSahAfterPatch = true;
        public static bool SahCryptProtectDataSahAfterGameExit = true;

        // These are intentionally empty in the updater. They must be downloaded from server launcher.ini.
        // Supported formats:
        //   [SahCrypt] KeyHex=<64 hex chars>
        //   [SahCrypt] KeyText=<exactly 32 UTF-8 bytes>
        //   [SahCrypt] KeyBase64=<32 raw bytes encoded as base64>
        //   [SahCrypt] Key=<legacy alias; hex if 64 hex chars, otherwise text>
        public static string SahCryptKeyHex = string.Empty;
        public static string SahCryptKeyText = string.Empty;
        public static string SahCryptKeyBase64 = string.Empty;
        public static string SahCryptKey = string.Empty;
        // Chiave AES-256 per patch/update files (separata da SahCryptKeyHex, ruotata ogni notte).
        public static string PatchCryptKeyHex = string.Empty;

        public static string ConfigUrl { get { return CombineUrl(BaseUrl, ConfigFile); } }
        public static string ManifestUrl { get { return CombineUrl(BaseUrl, ManifestFile); } }

        public static void Load()
        {
            if (_loaded) return;

            try
            {
                IniFile ini = DownloadServerIni();

                BaseUrl = EnsureSlash(ini.Get("Server", "BaseUrl", BaseUrl));
                ManifestFile = ini.Get("Server", "ManifestFile", ManifestFile);
                WebsiteUrl = ini.Get("Server", "WebsiteUrl", WebsiteUrl);
                RegisterUrl = ini.Get("Server", "RegisterUrl", RegisterUrl);
                DiscordUrl = ini.Get("Server", "DiscordUrl", DiscordUrl);
                NewsUrl = ini.Get("Server", "NewsUrl", NewsUrl);

                GameExecutable = ini.Get("Game", "Executable", GameExecutable);
                GameArguments = ini.Get("Game", "Arguments", GameArguments);
                RequireOriginalClientFiles = ini.GetBool("Game", "RequireOriginalClientFiles", RequireOriginalClientFiles);
                BlockPatchWhenGameRunning = ini.GetBool("Game", "BlockPatchWhenGameRunning", BlockPatchWhenGameRunning);
                AutoSelectServer = ini.GetBool("Game", "AutoSelectServer", AutoSelectServer);
                AutoSelectServerDelayMs = Math.Max(0, ini.GetInt("Game", "AutoSelectServerDelayMs", AutoSelectServerDelayMs));
                AutoSelectServerIndex = Math.Max(0, ini.GetInt("Game", "AutoSelectServerIndex", AutoSelectServerIndex));
                AutoSelectServerRetries = Math.Max(1, ini.GetInt("Game", "AutoSelectServerRetries", AutoSelectServerRetries));
                AutoSelectServerRetryDelayMs = Math.Max(100, ini.GetInt("Game", "AutoSelectServerRetryDelayMs", AutoSelectServerRetryDelayMs));
                AutoSelectServerObjectMissingAssumeSkipAttempts = Math.Max(1, ini.GetInt("Game", "AutoSelectServerObjectMissingAssumeSkipAttempts", AutoSelectServerObjectMissingAssumeSkipAttempts));
                HideLoginScreens = ini.GetBool("Game", "HideLoginScreens", HideLoginScreens);
                LoginOverlayText = ini.Get("Game", "LoginOverlayText", LoginOverlayText);
                LoginOverlaySubText = ini.Get("Game", "LoginOverlaySubText", LoginOverlaySubText);
                LoginOverlayRevealDelayMs = Math.Max(0, ini.GetInt("Game", "LoginOverlayRevealDelayMs", LoginOverlayRevealDelayMs));
                LoginOverlayFallbackRevealDelayMs = Math.Max(0, ini.GetInt("Game", "LoginOverlayFallbackRevealDelayMs", LoginOverlayFallbackRevealDelayMs));
                SkipServerSelectionRevealDelayMs = Math.Max(0, ini.GetInt("Game", "SkipServerSelectionRevealDelayMs", SkipServerSelectionRevealDelayMs));
                LoginFlowPatchRestoreDelayMs = Math.Max(0, ini.GetInt("Game", "LoginFlowPatchRestoreDelayMs", LoginFlowPatchRestoreDelayMs));
                AutoSelectRetryWhenServerListMissing = ini.GetBool("Game", "AutoSelectRetryWhenServerListMissing", AutoSelectRetryWhenServerListMissing);
                AutoSelectRetryDelayMs = Math.Max(0, ini.GetInt("Game", "AutoSelectRetryDelayMs", AutoSelectRetryDelayMs));
                HideGameWindowUntilReady = ini.GetBool("Game", "HideGameWindowUntilReady", HideGameWindowUntilReady);
                GameWindowHideMode = ini.Get("Game", "GameWindowHideMode", GameWindowHideMode);
                GameWindowHideWaitMs = Math.Max(0, ini.GetInt("Game", "GameWindowHideWaitMs", GameWindowHideWaitMs));
                GameWindowOffscreenX = ini.GetInt("Game", "GameWindowOffscreenX", GameWindowOffscreenX);
                GameWindowOffscreenY = ini.GetInt("Game", "GameWindowOffscreenY", GameWindowOffscreenY);
                LoginTcpPort = Math.Max(1, ini.GetInt("Game", "LoginTcpPort", LoginTcpPort));
                GameTcpPort = Math.Max(1, ini.GetInt("Game", "GameTcpPort", GameTcpPort));
                TcpPortTransitionDelayMs = Math.Max(0, ini.GetInt("Game", "TcpPortTransitionDelayMs", TcpPortTransitionDelayMs));
                TcpPortTransitionTimeoutMs = Math.Max(5000, ini.GetInt("Game", "TcpPortTransitionTimeoutMs", TcpPortTransitionTimeoutMs));
                SkipServerSelectionTcpTimeoutMs = Math.Max(1000, ini.GetInt("Game", "SkipServerSelectionTcpTimeoutMs", SkipServerSelectionTcpTimeoutMs));

                MinLauncherVersion = ini.GetInt("Launcher", "MinLauncherVersion", MinLauncherVersion);
                ForceUpdate = ini.GetBool("Launcher", "ForceUpdate", ForceUpdate);
                RepairEnabled = ini.GetBool("Launcher", "RepairEnabled", RepairEnabled);
                SelfUpdateEnabled = ini.GetBool("Launcher", "SelfUpdateEnabled", SelfUpdateEnabled);
                MaintenanceMessage = ini.Get("Launcher", "MaintenanceMessage", MaintenanceMessage);
                ManifestSha256 = ini.Get("Launcher", "ManifestSha256", ManifestSha256);

                DownloadRetryCount = Math.Max(1, ini.GetInt("Update", "DownloadRetryCount", DownloadRetryCount));
                DownloadTimeoutSeconds = Math.Max(5, ini.GetInt("Update", "DownloadTimeoutSeconds", DownloadTimeoutSeconds));
                KeepSuccessfulBackups = ini.GetBool("Update", "KeepSuccessfulBackups", KeepSuccessfulBackups);

                SahCryptEnabled = ini.GetBool("SahCrypt", "Enabled", SahCryptEnabled);
                SahCryptUnlockPatchSah = ini.GetBool("SahCrypt", "UnlockPatchSah", SahCryptUnlockPatchSah);
                SahCryptProtectDataSahAfterPatch = ini.GetBool("SahCrypt", "ProtectDataSahAfterPatch", SahCryptProtectDataSahAfterPatch);
                SahCryptProtectDataSahAfterGameExit = ini.GetBool("SahCrypt", "ProtectDataSahAfterGameExit", SahCryptProtectDataSahAfterGameExit);
                // SahCrypt keys are NOT loaded from launcher.ini — key comes exclusively from login response.
                // This prevents the AES key from being exposed in the publicly-accessible launcher config.
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Server launcher.ini could not be loaded. Built-in defaults will be used for non-crypt settings. " + ex.Message);
            }

            LoadClientConfigOverrides();
            _loaded = true;
        }

        public static string CombineUrl(string baseUrl, string relative)
        {
            if (string.IsNullOrEmpty(relative)) return EnsureSlash(baseUrl);
            if (relative.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return relative;
            return EnsureSlash(baseUrl) + relative.TrimStart('/');
        }

        private static IniFile DownloadServerIni()
        {
            using (TimeoutWebClient wc = new TimeoutWebClient(DownloadTimeoutSeconds * 1000))
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "LegacyShaiyaUpdater/1.0";
                string text = wc.DownloadString(ConfigUrl);
                AppLogger.Info("Server launcher config loaded from " + ConfigUrl);
                return IniFile.LoadText(text);
            }
        }

        private static void LoadClientConfigOverrides()
        {
            ClientConfigIniLoader.ApplyOverrides();
        }

        private static string EnsureSlash(string url) { return url.EndsWith("/") ? url : url + "/"; }
    }
}

