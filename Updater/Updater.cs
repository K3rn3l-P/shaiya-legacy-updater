using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;

namespace Shaiya_Duff_Updater
{
    public static class Updater
    {
        public static bool Ready;
        public static bool FinishedUpdating;
        public static bool DownloadingUpdater;
        public static int Local_ClientVersion;
        public static int Server_ClientVersion;
        public static int Server_UpdaterVersion;

        private static Thread _thread;
        private static LauncherManifest _manifest;

        public static LauncherManifest Manifest => _manifest;

        public static void Start()
        {
            _thread = new Thread(StartUpdater) { IsBackground = true };
            _thread.Start();
        }

        private static LauncherManifest DownloadManifest()
        {
            string json;
            using (TimeoutWebClient wc = CreateWebClient())
                json = wc.DownloadString(AppConfig.ManifestUrl);

            if (!string.IsNullOrEmpty(AppConfig.ManifestSha256))
            {
                string actual = HashUtil.Sha256String(json);
                if (!string.Equals(actual, AppConfig.ManifestSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Manifest integrity check failed.");
            }

            return JsonHttpClient.FromJson<LauncherManifest>(json);
        }

        private static void StartUpdater()
        {
            try
            {
                AppConfig.Load();
                Local_ClientVersion = VersionStore.ReadClientVersion();
                _manifest = DownloadManifest();
                if (_manifest == null) _manifest = new LauncherManifest { clientVersion = Local_ClientVersion, launcherVersion = AppConfig.LocalLauncherVersion };
                Server_ClientVersion = _manifest.clientVersion;
                Server_UpdaterVersion = _manifest.launcherVersion;
                if (_manifest.maintenance)
                {
                    string msg = string.IsNullOrEmpty(_manifest.maintenanceMessage) ? AppConfig.MaintenanceMessage : _manifest.maintenanceMessage;
                    throw new InvalidOperationException(msg);
                }
                Ready = true;
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                Server_ClientVersion = Local_ClientVersion;
                Server_UpdaterVersion = AppConfig.LocalLauncherVersion;
                Ready = true;
            }
        }

        public static void Continue()
        {
            _thread = new Thread(ContinueUpdater) { IsBackground = true };
            _thread.Start();
        }

        private static void ContinueUpdater()
        {
            try
            {
                AppConfig.Load();
                if (_manifest == null)
                {
                    try { _manifest = DownloadManifest(); }
                    catch { _manifest = new LauncherManifest { clientVersion = Local_ClientVersion, launcherVersion = AppConfig.LocalLauncherVersion }; }
                }

                if (NeedsLauncherUpdate(_manifest))
                {
                    UpdateUpdater(_manifest);
                    return;
                }

                if (Local_ClientVersion < _manifest.clientVersion || AppConfig.ForceUpdate)
                    UpdateClient(_manifest);

                FinishedUpdating = true;
                Utilities.ExecuteOnMainThread(delegate()
                {
                    if (MainWindow.MW == null) return;
                    MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                    MainWindow.MW.TextBlockUpdaterStatus.Text = "";
                    MainWindow.MW.CanStartGame();
                });
            }
            catch (PatchKeyExpiredException ex)
            {
                AppConfig.PatchCryptKeyHex = string.Empty;
                string staleDir = AppPaths.InBase("patch");
                try { if (Directory.Exists(staleDir)) Directory.Delete(staleDir, true); } catch { }
                AppLogger.Debug("patch", "Patch key expired (server rotation). Old key cleared, patch dir purged. NTSTATUS=0xC000A002.");
                Utilities.ExecuteOnMainThread(delegate()
                {
                    if (MainWindow.MW != null)
                    {
                        MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                        MainWindow.MW.TextBlockUpdaterStatus.Text = "Update failed.";
                    }
                    MessageDialog.Display("Update Error", ex.Message);
                });
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                Utilities.ExecuteOnMainThread(delegate()
                {
                    if (MainWindow.MW != null)
                    {
                        MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                        MainWindow.MW.TextBlockUpdaterStatus.Text = "Update failed.";
                    }
                    MessageDialog.Display("Update Error", ex.Message);
                });
            }
        }

        public static void Repair()
        {
            Thread thread = new Thread(delegate()
            {
                try
                {
                    AppConfig.Load();
                    LauncherManifest manifest = _manifest ?? JsonHttpClient.Get<LauncherManifest>(AppConfig.ManifestUrl);
                    if (!AppConfig.RepairEnabled) throw new InvalidOperationException("Repair is disabled by server configuration.");

                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW == null) return;
                        MainWindow.MW.GameStartButton.Visibility = Visibility.Hidden;
                        MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Visible;
                        MainWindow.MW.UpdaterProgressBar.SetProgress(0);
                        MainWindow.MW.TextBlockUpdaterStatus.Text = "Starting repair";
                    });

                    RepairService.VerifyAndRepair(manifest, delegate(int percent, string message)
                    {
                        Utilities.ExecuteOnMainThread(delegate()
                        {
                            if (MainWindow.MW == null) return;
                            MainWindow.MW.UpdaterProgressBar.SetProgress((byte)Math.Max(0, Math.Min(100, percent)));
                            MainWindow.MW.TextBlockUpdaterStatus.Text = message;
                        });
                    });

                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW == null) return;
                        MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                        MainWindow.MW.TextBlockUpdaterStatus.Text = "Repair completed.";
                        MainWindow.MW.CanStartGame();
                        MessageDialog.Display("Repair", "Repair completed.");
                    });
                }
                catch (Exception ex)
                {
                    ExceptionManager.Submit(ex);
                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW != null)
                        {
                            MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Hidden;
                            MainWindow.MW.TextBlockUpdaterStatus.Text = "Repair failed.";
                        }
                        MessageDialog.Display("Repair Error", ex.Message);
                    });
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private static bool NeedsLauncherUpdate(LauncherManifest manifest)
        {
            if (!AppConfig.SelfUpdateEnabled) return false;
            if (manifest == null || string.IsNullOrEmpty(manifest.launcherFile)) return false;
            int min = Math.Max(AppConfig.MinLauncherVersion, manifest.minLauncherVersion);
            return manifest.launcherVersion > AppConfig.LocalLauncherVersion || min > AppConfig.LocalLauncherVersion;
        }

        private static void UpdateUpdater(LauncherManifest manifest)
        {
            try
            {
                DownloadingUpdater = true;
                Utilities.ExecuteOnMainThread(delegate()
                {
                    MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Visible;
                    MainWindow.MW.TextBlockUpdaterStatus.Text = "Downloading launcher update";
                });

                string url = AppConfig.CombineUrl(AppConfig.BaseUrl, manifest.launcherFile);
                string newExe = AppPaths.InBase("Updater.exe.new");
                DownloadFileWithRetry(url, newExe);

                if (!string.IsNullOrWhiteSpace(manifest.launcherSha256))
                {
                    string actual = HashUtil.Sha256File(newExe);
                    AppLogger.Debug("updater", "Launcher SHA256 expected=" + manifest.launcherSha256 + " actual=" + actual);
                    if (!string.Equals(actual, manifest.launcherSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(newExe); } catch { }
                        throw new InvalidDataException("Launcher update integrity check failed.");
                    }
                }

                string bat = AppPaths.InBase("apply_launcher_update.bat");
                File.WriteAllText(bat,
                    "@echo off\r\n" +
                    "timeout /t 1 /nobreak >nul\r\n" +
                    "del /f /q \"Updater.exe.bak\" >nul 2>nul\r\n" +
                    "ren \"Updater.exe\" \"Updater.exe.bak\"\r\n" +
                    "ren \"Updater.exe.new\" \"Updater.exe\"\r\n" +
                    "start \"\" \"Updater.exe\"\r\n" +
                    "del /f /q \"%~f0\" >nul 2>nul\r\n");
                Process.Start(new ProcessStartInfo(bat) { WorkingDirectory = AppPaths.BaseDirectory, WindowStyle = ProcessWindowStyle.Hidden });
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DownloadingUpdater = false;
                throw new InvalidOperationException("Launcher update failed: " + ex.Message, ex);
            }
        }

        private static void UpdateClient(LauncherManifest manifest)
        {
            GameProcessGuard.ThrowIfGameRunningForPatch();

            // Purge stale .patch/.download files left by a previous crashed session.
            // Under normal operation the finally block in ApplyPatch deletes everything,
            // but a hard kill (power loss, task kill) can leave orphans.
            string patchStagingDir = AppPaths.InBase("patch");
            if (Directory.Exists(patchStagingDir))
            {
                try { Directory.Delete(patchStagingDir, true); } catch { }
            }

            Utilities.ExecuteOnMainThread(delegate()
            {
                MainWindow.MW.UpdaterProgressBar.Visibility = Visibility.Visible;
                MainWindow.MW.TextBlockUpdaterStatus.Text = "Preparing patches";
            });

            PatchManifestItem[] patches = manifest.patches ?? new PatchManifestItem[0];
            int totalPatches = manifest.clientVersion - Local_ClientVersion;
            int patchIndex   = 0;
            for (int version = Local_ClientVersion + 1; version <= manifest.clientVersion; version++)
            {
                patchIndex++;
                PatchManifestItem item = patches.FirstOrDefault(p => p.version == version);
                if (item == null) throw new FileNotFoundException("Patch manifest entry missing for version " + version + ".");

                VersionStore.StartPatch(Local_ClientVersion, item);
                try
                {
                    ApplyPatch(item, patchIndex, totalPatches);
                    Local_ClientVersion = version;
                    VersionStore.CommitPatch(Local_ClientVersion, item);
                }
                catch (Exception ex)
                {
                    VersionStore.FailPatch(Local_ClientVersion, item, ex.Message);
                    throw;
                }
            }
        }

        private static void ApplyPatch(PatchManifestItem item, int patchIndex, int totalPatches)
        {
            string tempRoot = AppPaths.InBase(Path.Combine("patch", "tmp_" + item.version.ToString("D4")));
            string downloadDir = AppPaths.InBase("patch");
            Directory.CreateDirectory(downloadDir);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            Directory.CreateDirectory(tempRoot);

            string patchFile   = PatchPathUtil.GetPatchLocalPath(downloadDir, item);
            string tmpDownload = patchFile + ".download";
            string extractDir  = Path.Combine(tempRoot, "extract");
            string patchLabel  = patchIndex + " di " + totalPatches;

            try
            {
                AppLogger.Patch("Patch apply started. Version=" + item.version + " File=" + item.file);
                Utilities.ExecuteOnMainThread(delegate()
                {
                    MainWindow.MW.UpdaterProgressBar.Reset();
                    MainWindow.MW.UpdaterProgressBar.SetStatus("Downloading (" + patchLabel + ")");
                });

                string url = PatchPathUtil.GetPatchUrl(item);
                DownloadPatchWithProgress(url, tmpDownload, patchLabel);
                if (File.Exists(patchFile)) File.Delete(patchFile);
                File.Move(tmpDownload, patchFile);

                ValidatePatchFile(item, patchFile);

                string extractLabel = "Extracting (" + patchLabel + ")";
                Utilities.ExecuteOnMainThread(delegate() { MainWindow.MW.UpdaterProgressBar.SetInstallProgress(0, extractLabel); });
                PatchExtractor.ExtractZipSafe(patchFile, extractDir, delegate(int pct)
                {
                    string lbl = extractLabel;
                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW == null) return;
                        MainWindow.MW.UpdaterProgressBar.SetInstallProgress(pct, lbl);
                    });
                });

                string installLabel = "Installing (" + patchLabel + ")";
                Utilities.ExecuteOnMainThread(delegate() { MainWindow.MW.UpdaterProgressBar.SetInstallProgress(0, installLabel); });
                PatchApplier.ApplyExtractedPatch(extractDir, item, delegate(int pct)
                {
                    string lbl = installLabel;
                    Utilities.ExecuteOnMainThread(delegate()
                    {
                        if (MainWindow.MW == null) return;
                        MainWindow.MW.UpdaterProgressBar.SetInstallProgress(pct, lbl);
                    });
                });

                Utilities.ExecuteOnMainThread(delegate() { MainWindow.MW.UpdaterProgressBar.SetInstallProgress(100, "Patch " + patchLabel + " completata"); });
                AppLogger.Patch("Patch apply completed. Version=" + item.version);
            }
            finally
            {
                try { if (File.Exists(tmpDownload)) File.Delete(tmpDownload); } catch { }
                try { if (File.Exists(patchFile)) File.Delete(patchFile); } catch { }
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
                try { if (Directory.Exists(downloadDir)) Directory.Delete(downloadDir); } catch { }
            }
        }

        private static void ValidatePatchFile(PatchManifestItem item, string patchFile)
        {
            if (item.size > 0)
            {
                long actualSize = new FileInfo(patchFile).Length;
                if (actualSize != item.size)
                    throw new InvalidDataException("Patch size mismatch for " + item.file + ". Expected=" + item.size + " Actual=" + actualSize);
            }

            if (!string.IsNullOrWhiteSpace(item.sha256))
            {
                string actual = HashUtil.Sha256File(patchFile);
                if (!string.Equals(actual, item.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Patch SHA256 mismatch for " + item.file + ". Expected=" + item.sha256 + " Actual=" + actual);
            }

            AppLogger.Patch("Patch validation OK. File=" + item.file + " Size=" + new FileInfo(patchFile).Length);
        }

        private static void DownloadPatchWithProgress(string url, string target, string patchLabel)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= AppConfig.DownloadRetryCount; attempt++)
            {
                try
                {
                    if (File.Exists(target)) File.Delete(target);
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = "LegacyShaiyaUpdater/1.0";
                    req.Timeout   = AppConfig.DownloadTimeoutSeconds * 1000;
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream src = resp.GetResponseStream())
                    using (FileStream dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                    {
                        long total    = resp.ContentLength;
                        long received = 0;
                        byte[] buf    = new byte[65536];
                        int read;
                        long speedBytes  = 0;
                        DateTime speedTs = DateTime.UtcNow;

                        while ((read = src.Read(buf, 0, buf.Length)) > 0)
                        {
                            dst.Write(buf, 0, read);
                            received  += read;
                            speedBytes += read;

                            double elapsed = (DateTime.UtcNow - speedTs).TotalSeconds;
                            if (elapsed >= 0.15)
                            {
                                double bps      = speedBytes / elapsed;
                                speedBytes      = 0;
                                speedTs         = DateTime.UtcNow;
                                long totalSnap  = total;
                                long recvSnap   = received;
                                double bpsSnap  = bps;
                                int pct = totalSnap > 0 ? (int)Math.Min(100, recvSnap * 100 / totalSnap) : 0;
                                Utilities.ExecuteOnMainThread(delegate()
                                {
                                    if (MainWindow.MW == null) return;
                                    MainWindow.MW.UpdaterProgressBar.SetDownloadProgress(
                                        pct,
                                        "Downloading (" + patchLabel + ")",
                                        FormatBytes(recvSnap),
                                        totalSnap > 0 ? FormatBytes(totalSnap) : "?",
                                        FormatSpeed(bpsSnap));
                                });
                            }
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppLogger.PatchWarn("Download attempt " + attempt + " failed: " + url + " - " + ex.Message);
                    try { if (File.Exists(target)) File.Delete(target); } catch { }
                    Thread.Sleep(500);
                }
            }
            throw new InvalidOperationException("Download failed: " + url + " - " + (last == null ? "unknown error" : last.Message), last);
        }

        private static void DownloadFileWithRetry(string url, string target)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= AppConfig.DownloadRetryCount; attempt++)
            {
                try
                {
                    if (File.Exists(target)) File.Delete(target);
                    using (TimeoutWebClient wc = CreateWebClient())
                        wc.DownloadFile(new Uri(url), target);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    AppLogger.PatchWarn("Download attempt " + attempt + " failed: " + url + " - " + ex.Message);
                    try { if (File.Exists(target)) File.Delete(target); } catch { }
                    Thread.Sleep(500);
                }
            }
            throw new InvalidOperationException("Download failed: " + url + " - " + (last == null ? "unknown error" : last.Message), last);
        }

        private static TimeoutWebClient CreateWebClient()
        {
            TimeoutWebClient wc = new TimeoutWebClient(AppConfig.DownloadTimeoutSeconds * 1000);
            wc.Headers[HttpRequestHeader.UserAgent] = "LegacyShaiyaUpdater/1.0";
            return wc;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.0") + " KB";
            double mb = kb / 1024.0;
            return mb.ToString("0.0") + " MB";
        }

        private static string FormatSpeed(double bps)
        {
            if (bps < 1024) return ((int)bps) + " B/s";
            double kbps = bps / 1024.0;
            if (kbps < 1024) return kbps.ToString("0.0") + " KB/s";
            return (kbps / 1024.0).ToString("0.0") + " MB/s";
        }
    }
}

