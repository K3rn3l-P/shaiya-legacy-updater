using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Shaiya_Duff_Updater
{
    public partial class MainWindow : Window
    {
        public static MainWindow MW;
        private iMenuItem LinkAnotherAccount;
        public static List<string> Accounts = new List<string>();
        public string UserID;
        public string TempPw;
        public byte? LogInType;
        private bool _launchingGame;

        public MainWindow()
        {
            InitializeComponent();
            AppConfig.Load();
            MW = this;
            LinkWebsite.Link = AppConfig.WebsiteUrl;
            LinkRegister.Link = AppConfig.RegisterUrl;
            Updater.Continue();

            LinkAnotherAccount = new iMenuItem
            {
                Text = "Log in using another account",
                Link = "Updater:LogInNewAccount",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 18.0
            };
            RefreshLogInView();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private ImageBrush Brush(string uri) { return new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Resources/" + uri))); }

        private void ButtonMinimize_MouseEnter(object sender, MouseEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize_hover.jpg"); }
        private void ButtonMinimize_MouseLeave(object sender, MouseEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize.png"); }
        private void ButtonMinimize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize_active.jpg"); }
        private void ButtonMinimize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { ButtonMinimize.Fill = Brush("button_minimize.png"); WindowState = WindowState.Minimized; }
        private void ButtonClose_MouseEnter(object sender, MouseEventArgs e) { ButtonClose.Fill = Brush("button_close_hover.jpg"); }
        private void ButtonClose_MouseLeave(object sender, MouseEventArgs e) { ButtonClose.Fill = Brush("button_close.png"); }
        private void ButtonClose_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { ButtonClose.Fill = Brush("button_close_active.jpg"); }
        private void ButtonClose_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { ButtonClose.Fill = Brush("button_close.png"); Close(); }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_launchingGame) e.Cancel = true;
        }

        public void ShowLatestNews()
        {
            DoubleAnimation a = new DoubleAnimation { From = 0.0, To = 960.0, Duration = new Duration(TimeSpan.FromMilliseconds(400.0)) };
            Storyboard.SetTarget(a, GridSlide);
            Storyboard.SetTargetProperty(a, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            Storyboard sb = new Storyboard();
            sb.Children.Add(a);
            sb.Begin();
            LinkLogIn.IsSelected = false;
            LinkLatestNews.IsSelected = true;
        }

        public void ShowLogIn()
        {
            DoubleAnimation a = new DoubleAnimation { From = 960.0, To = 0.0, Duration = new Duration(TimeSpan.FromMilliseconds(400.0)) };
            Storyboard.SetTarget(a, GridSlide);
            Storyboard.SetTargetProperty(a, new PropertyPath("RenderTransform.(TranslateTransform.X)"));
            Storyboard sb = new Storyboard();
            sb.Children.Add(a);
            sb.Begin();
            LinkLogIn.IsSelected = true;
            LinkLatestNews.IsSelected = false;
        }

        public void RefreshExistingAccounts()
        {
            AccountRectangles.Children.Clear();
            if (Accounts.Count <= 0) return;
            foreach (string account in Accounts) AccountRectangles.Children.Add(new Account(account));
            AccountRectangles.Children.Add(LinkAnotherAccount);
            LogInButton.Margin = new Thickness(0.0, 210.0, 0.0, 0.0);
            LinkSavedAccounts.Visibility = Visibility.Visible;
            LinkAnotherAccount.Visibility = Accounts.Count >= 8 ? Visibility.Hidden : Visibility.Visible;
        }

        public void RefreshLogInView(bool savedAccounts = true)
        {
            if (savedAccounts && Accounts.Count > 0)
            {
                RefreshExistingAccounts();
                GridSavedAccounts.Visibility = Visibility.Visible;
                GridNewAccount.Visibility = Visibility.Hidden;
            }
            else
            {
                GridSavedAccounts.Visibility = Visibility.Hidden;
                GridNewAccount.Visibility = Visibility.Visible;
                TextBoxUserID.Focus();
            }
        }

        private void GridNewAccount_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) LogInIdPassword();
        }

        private void LogInButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { LogInIdPassword(); }

        private void LogInIdPassword()
        {
            string userId = TextBoxUserID.Text.Trim();
            string password = TextBoxPassword.Password;
            bool saveAccount = CheckBoxSaveAccount.IsChecked == true;
            if (userId.Length == 0 || password.Length == 0) return;
            UserID = userId;
            UpdaterManager.LogInIdPassword(userId, password, saveAccount);
        }

        public void LogInId(string userId)
        {
            UserID = userId;
            UpdaterManager.LogInId(userId);
        }

        public void LogOut()
        {
            GameStartButton.Visibility = Visibility.Hidden;
            LinkLogIn.Text = "Log In";
            LinkLogIn.Link = "Updater:LogIn";
            LinkLogIn.IsSelected = false;
            LinkLogIn.Cursor = Cursors.Hand;
            LinkRegister.Text = "Sign Up";
            LinkRegister.Link = AppConfig.RegisterUrl;
            ShowLogIn();
            if (LogInType.GetValueOrDefault() == 0 && LogInType.HasValue) GridNewAccount.Visibility = Visibility.Visible;
            else GridSavedAccounts.Visibility = Visibility.Visible;
            UserID = null;
            TempPw = null;
            RefreshLogInView();
        }

        public void StartRepair()
        {
            if (MessageDialog.Display("Repair", "This will verify client files and re-download broken/missing files. Continue?", "YesNo") != "Yes") return;
            Updater.Repair();
        }

        public void CanStartGame()
        {
            if (UserID == null || !Updater.FinishedUpdating) return;
            GameStartButton.Visibility = Visibility.Visible;
        }

        private void GameStartButton_Click(object sender, MouseButtonEventArgs e)
        {
            string gamePath = Path.Combine(AppPaths.BaseDirectory, AppConfig.GameExecutable);
            if (!File.Exists(gamePath))
            {
                MessageDialog.Display("Error", AppConfig.GameExecutable + " was not found.");
                return;
            }

            if (GameProcessGuard.IsGameRunning())
            {
                MessageDialog.Display("Error", AppConfig.GameExecutable + " is already running.");
                return;
            }

            _launchingGame = true;

            if (AppConfig.HideLoginScreens)
            {
                ShowGameLaunchPanel(AppConfig.LoginOverlayText, AppConfig.LoginOverlaySubText);
                AppLogger.Info("Launcher launch panel opened. Game window will stay hidden until character selection is ready.");
            }
            else
            {
                GameStartButton.Visibility = Visibility.Hidden;
                AppLogger.Info("Launcher launch panel disabled by launcher.ini.");
            }

            Thread launchThread = new Thread(delegate()
            {
                LaunchGameWorker(gamePath);
            });
            launchThread.Name = "GameLaunchFlow";
            launchThread.IsBackground = true;
            launchThread.Start();
        }

        private static void LaunchGameWorker(string gamePath)
        {
            int processId = 0;
            Process launchedProcess = null;
            GameWindowController gameWindow = null;
            bool success = false;

            try
            {
                AppLogger.Debug("launch", "LaunchGameWorker start. gamePath=" + gamePath);
                SetGameLaunchStatus("Starting game...", AppConfig.LoginOverlaySubText);

                EnsureDataSahReadyForLaunch();

                ProcessStartInfo psi = new ProcessStartInfo(gamePath, AppConfig.GameArguments)
                {
                    WorkingDirectory = AppPaths.BaseDirectory
                };

                Process process = Process.Start(psi);
                if (process == null) throw new InvalidOperationException("Game process could not be started.");

                launchedProcess = process;
                processId = process.Id;
                AppLogger.Debug("launch", "Game process started. pid=" + processId);
                gameWindow = GameWindowController.HideUntilReady(processId);
                TryInjectCredentials(processId, out success);
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                AppLogger.Debug("launch", "LaunchGameWorker exception. pid=" + processId + " msg=" + ex.Message);
                if (processId != 0)
                {
                    try { Process.GetProcessById(processId).Kill(); } catch { }
                }
                ShowLaunchFailed("Launch failed", "Please check updater.log and try again.");
            }
            finally
            {
                AppLogger.Debug("launch", "LaunchGameWorker finally. success=" + (success ? "1" : "0") + " pid=" + processId);
                if (success)
                {
                    try { if (gameWindow != null) gameWindow.Restore(); } catch { }
                    WaitForGameExitAndReprotectDataSah(launchedProcess, processId);
                    Environment.Exit(0);
                }
                else
                {
                    try { if (gameWindow != null) gameWindow.Restore(); } catch { }
                    ResetGameLaunchPanel();
                }
            }
        }

        private static void WaitForGameExitAndReprotectDataSah(Process launchedProcess, int processId)
        {
            try
            {
                Utilities.ExecuteOnMainThread(delegate()
                {
                    if (MW != null) MW.Hide();
                });
            }
            catch { }

            try
            {
                if (launchedProcess == null && processId != 0)
                {
                    try { launchedProcess = Process.GetProcessById(processId); } catch { }
                }

                if (launchedProcess != null)
                {
                    AppLogger.Debug("launch", "Waiting game exit before re-protect. pid=" + launchedProcess.Id);
                    launchedProcess.WaitForExit();
                    AppLogger.Debug("launch", "Game exited. pid=" + launchedProcess.Id + " exitCode=" + launchedProcess.ExitCode);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Wait for game exit failed: " + ex.Message);
            }

            TryProtectDataSahAfterGameExit();
        }

        private static void TryProtectDataSahAfterGameExit()
        {
            if (!AppConfig.SahCryptEnabled) return;
            if (!AppConfig.SahCryptProtectDataSahAfterGameExit) return;

            string dataSah = AppPaths.InBase("data.sah");
            if (!File.Exists(dataSah)) return;

            try
            {
                SahCryptState before = SahCrypt.GetState(dataSah);
                AppLogger.Debug("launch", "data.sah state before post-exit protect=" + before);
                if (before != SahCryptState.PlainSah) return;

                bool changed = SahCrypt.ProtectIfNeeded(dataSah, null);
                SahCryptState after = SahCrypt.GetState(dataSah);
                AppLogger.Info("data.sah protected after game exit. changed=" + (changed ? "1" : "0") + " stateAfter=" + after);
                AppLogger.Debug("launch", "data.sah post-exit protect completed. stateAfter=" + after);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("data.sah post-exit protect failed: " + ex.Message);
            }
        }

        private static void EnsureDataSahReadyForLaunch()
        {
            if (!AppConfig.SahCryptEnabled) return;

            string dataSah = AppPaths.InBase("data.sah");
            if (!File.Exists(dataSah)) return;

            SahCryptState before = SahCrypt.GetState(dataSah);
            AppLogger.Debug("launch", "data.sah state before launch=" + before);

            if (before == SahCryptState.EncryptedEsah || before == SahCryptState.LegacyRolSah)
            {
                try
                {
                    bool changed = SahCrypt.UnlockIfNeeded(dataSah, null);
                    SahCryptState after = SahCrypt.GetState(dataSah);
                    AppLogger.Info("data.sah normalized for launch. changed=" + (changed ? "1" : "0") + " stateAfter=" + after);
                    if (after != SahCryptState.PlainSah)
                        throw new InvalidOperationException("data.sah was not converted to plain SAH.");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Unable to prepare data.sah for launch. Verify [SahCrypt] key configuration. " + ex.Message, ex);
                }
            }
        }

        private static void TryInjectCredentials(int processId, out bool success)
        {
            bool autoSelected = false;
            success = false;
            LoginFlowPatchBackup loginFlowBackup = new LoginFlowPatchBackup();
            bool loginFlowPatched = false;

            AppLogger.Debug("launch", "TryInjectCredentials start. pid=" + processId);
            Thread.Sleep(2000);
            int pointer = 0;
            for (int i = 0; i < 150; i++)
            {
                pointer = BitConverter.ToInt32(MemoryManager.ReadMemory(processId, GameOffsets.LoginPointer, 4), 0);
                if (pointer != 0) break;
                Thread.Sleep(200);
            }
            if (pointer == 0) throw new InvalidOperationException("Game login buffer pointer was not initialized.");

            AppLogger.Debug("launch", "Login pointer initialized. address=0x" + pointer.ToString("X8"));

            int netState = 0;
            for (int i = 0; i < 150; i++)
            {
                netState = BitConverter.ToInt32(MemoryManager.ReadMemory(processId, GameOffsets.SelectServerGlobalStatePointer, 4), 0);
                if (netState != 0) break;
                Thread.Sleep(200);
            }
            if (netState == 0) throw new InvalidOperationException("Network state pointer (DAT_022eed30) was not initialized.");
            AppLogger.Debug("launch", "Network state ready. address=0x" + netState.ToString("X8"));

            int loginSocket = 0;
            for (int i = 0; i < 50; i++)
            {
                loginSocket = BitConverter.ToInt32(MemoryManager.ReadMemory(processId, netState + 4, 4), 0);
                if (loginSocket != 0) break;
                Thread.Sleep(200);
            }
            AppLogger.Debug("launch", "Login socket. value=0x" + loginSocket.ToString("X8") + " ready=" + (loginSocket != 0 ? "1" : "0"));

            SetGameLaunchStatus("Logging in...", "Connecting with your account.");
            InjectCredentials(processId, pointer, out loginFlowBackup, out loginFlowPatched);
            AppLogger.Debug("launch", "Credentials injected.");

            if (AppConfig.SkipServerSelectionFromClientConfig)
            {
                AppLogger.Info("SKIPSERVERSELECTION=1 detected in CONFIG.INI. Skipping launcher auto-select hook.");
                AppLogger.Debug("launch", "Branch=SkipServerSelectionFromClientConfig.");

                int holdMs = Math.Max(0, AppConfig.LoginFlowPatchRestoreDelayMs);
                if (holdMs > 0)
                {
                    AppLogger.Debug("launch", "Holding login flow patches for " + holdMs + "ms before skip-mode TCP wait.");
                    Thread.Sleep(holdMs);
                }

                bool skipTcpReady = GameTcpPortTransitionWatcher.WaitForLoginToGameTransition(processId, AppConfig.SkipServerSelectionTcpTimeoutMs);
                AppLogger.Debug("launch", "Skip-mode TCP wait finished. detected=" + (skipTcpReady ? "1" : "0"));

                if (!skipTcpReady && AppConfig.AutoSelectRetryWhenServerListMissing)
                {
                    int retryDelay = Math.Max(0, AppConfig.AutoSelectRetryDelayMs);
                    AppLogger.Warn("Skip-mode recovery triggered after TCP timeout. RetryDelayMs=" + retryDelay);
                    if (retryDelay > 0) Thread.Sleep(retryDelay);

                    try
                    {
                        if (Process.GetProcessById(processId).HasExited)
                        {
                            AppLogger.Warn("Skip-mode recovery skipped because the game process already exited.");
                        }
                        else
                        {
                            ReinjectCredentialsAndPatches(processId, pointer);
                            loginFlowPatched = true;
                            AppLogger.Debug("launch", "Skip-mode recovery reinjection completed.");

                            if (holdMs > 0)
                            {
                                AppLogger.Debug("launch", "Holding login flow patches for " + holdMs + "ms before skip-mode recovery TCP wait.");
                                Thread.Sleep(holdMs);
                            }

                            skipTcpReady = GameTcpPortTransitionWatcher.WaitForLoginToGameTransition(processId, AppConfig.SkipServerSelectionTcpTimeoutMs);
                            AppLogger.Debug("launch", "Skip-mode recovery TCP wait finished. detected=" + (skipTcpReady ? "1" : "0"));
                        }
                    }
                    catch
                    {
                        AppLogger.Warn("Skip-mode recovery skipped because the game process is no longer available.");
                    }
                }

                if (skipTcpReady)
                {
                    TryRestoreLoginFlowPatches(processId, loginFlowBackup, loginFlowPatched, "skip-server-selection-tcp-ready");
                    loginFlowPatched = false;
                    SetGameLaunchStatus("Loading characters...", "Almost ready.");
                }
                else
                {
                    AppLogger.Warn("Skip-mode did not reach character selection within timeout. Falling back to manual login visibility.");
                    SetGameLaunchStatus("Manual login required", "Please continue in the game window.");
                    Thread.Sleep(AppConfig.LoginOverlayFallbackRevealDelayMs);
                }
            }
            else
            {
                try
                {
                    SetGameLaunchStatus("Selecting server...", "Preparing character list.");
                    autoSelected = GameServerSelectAutomation.AutoSelectFirstServer(processId);
                    AppLogger.Debug("launch", "Auto-select attempted. result=" + (autoSelected ? "1" : "0"));

                    if (!autoSelected && ShouldRetryAutoSelectBecauseServerListMissing())
                    {
                        int retryDelay = Math.Max(0, AppConfig.AutoSelectRetryDelayMs);
                        AppLogger.Warn("Auto-select recovery triggered. Reason=" + GameServerSelectAutomation.LastDecisionReason + " Attempts=" + GameServerSelectAutomation.LastDecisionAttemptCount + " RetryDelayMs=" + retryDelay);
                        if (retryDelay > 0) Thread.Sleep(retryDelay);

                        ReinjectCredentialsAndPatches(processId, pointer);
                        AppLogger.Debug("launch", "Auto-select recovery reinjection completed.");

                        autoSelected = GameServerSelectAutomation.AutoSelectFirstServer(processId);
                        AppLogger.Debug("launch", "Auto-select recovery attempt result=" + (autoSelected ? "1" : "0"));
                    }
                }
                catch (Exception autoEx)
                {
                    // Auto server selection is optional. Never kill the game for this step;
                    // the player can still select the server manually.
                    AppLogger.Warn("Auto server select failed without terminating game: " + autoEx.Message);
                }

                if (autoSelected)
                {
                    bool tcpTransitionDetected = GameTcpPortTransitionWatcher.WaitForLoginToGameTransition(processId, AppConfig.TcpPortTransitionTimeoutMs);
                    AppLogger.Debug("launch", "TCP wait after auto-select finished. detected=" + (tcpTransitionDetected ? "1" : "0"));
                    if (tcpTransitionDetected)
                    {
                        TryRestoreLoginFlowPatches(processId, loginFlowBackup, loginFlowPatched, "autoselect-success-tcp-ready");
                        loginFlowPatched = false;
                        SetGameLaunchStatus("Loading characters...", "Almost ready.");
                    }
                    else
                    {
                        AppLogger.Warn("TCP transition was not detected after auto-select. Using time-based reveal fallback.");
                        TryRestoreLoginFlowPatches(processId, loginFlowBackup, loginFlowPatched, "tcp-timeout-after-autoselect");
                        loginFlowPatched = false;
                        SetGameLaunchStatus("Loading characters...", "Almost ready.");
                        Thread.Sleep(AppConfig.LoginOverlayRevealDelayMs);
                    }
                }
                else
                {
                    string manualReason = GameServerSelectAutomation.LastDecisionReason;
                    int manualAttempts = GameServerSelectAutomation.LastDecisionAttemptCount;
                    AppLogger.Warn("Switching to manual server selection. Reason=" + manualReason + " Attempts=" + manualAttempts);
                    AppLogger.Debug("launch", "Branch=ManualServerSelectionRequired reason=" + manualReason + " attempts=" + manualAttempts);
                    SetGameLaunchStatus("Manual server selection required", "Please continue in the game window.");
                    Thread.Sleep(AppConfig.LoginOverlayFallbackRevealDelayMs);
                }
            }

            success = true;
            AppLogger.Debug("launch", "TryInjectCredentials completed successfully.");
        }

        private struct LoginFlowPatchBackup
        {
            public byte[] Patch1;
            public byte[] Patch2;
            public byte[] Patch3;
            public bool Captured;
        }

        private static void InjectCredentials(int processId, int pointer, out LoginFlowPatchBackup backup, out bool loginFlowPatched)
        {
            backup = CaptureLoginFlowPatchBackup(processId);
            loginFlowPatched = false;

            MemoryManager.WriteFixedAscii(processId, pointer + GameOffsets.UserIdOffsetFromPointer, MW.UserID, GameOffsets.UserIdBufferLength);
            MemoryManager.WriteFixedAscii(processId, pointer + GameOffsets.TempPasswordOffsetFromPointer, MW.TempPw, GameOffsets.TempPasswordBufferLength);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow1, GameOffsets.PatchLoginFlow1Bytes);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow2, GameOffsets.PatchLoginFlow2Bytes);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow3, GameOffsets.PatchLoginFlow3Bytes);
            loginFlowPatched = true;
            AppLogger.Debug("launch", "Login flow patch bytes written.");
        }

        private static void ReinjectCredentialsAndPatches(int processId, int pointer)
        {
            MemoryManager.WriteFixedAscii(processId, pointer + GameOffsets.UserIdOffsetFromPointer, MW.UserID, GameOffsets.UserIdBufferLength);
            MemoryManager.WriteFixedAscii(processId, pointer + GameOffsets.TempPasswordOffsetFromPointer, MW.TempPw, GameOffsets.TempPasswordBufferLength);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow1, GameOffsets.PatchLoginFlow1Bytes);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow2, GameOffsets.PatchLoginFlow2Bytes);
            MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow3, GameOffsets.PatchLoginFlow3Bytes);
        }

        private static bool ShouldRetryAutoSelectBecauseServerListMissing()
        {
            if (!AppConfig.AutoSelectRetryWhenServerListMissing) return false;
            string reason = GameServerSelectAutomation.LastDecisionReason ?? string.Empty;
            return reason.IndexOf("server list pointer is 0", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static LoginFlowPatchBackup CaptureLoginFlowPatchBackup(int processId)
        {
            LoginFlowPatchBackup backup = new LoginFlowPatchBackup();
            try
            {
                backup.Patch1 = MemoryManager.ReadMemory(processId, GameOffsets.PatchLoginFlow1, GameOffsets.PatchLoginFlow1Bytes.Length);
                backup.Patch2 = MemoryManager.ReadMemory(processId, GameOffsets.PatchLoginFlow2, GameOffsets.PatchLoginFlow2Bytes.Length);
                backup.Patch3 = MemoryManager.ReadMemory(processId, GameOffsets.PatchLoginFlow3, GameOffsets.PatchLoginFlow3Bytes.Length);
                backup.Captured = true;
                AppLogger.Debug("launch", "Login flow patch backup captured.");
            }
            catch (Exception ex)
            {
                backup.Captured = false;
                AppLogger.Warn("Login flow patch backup capture failed: " + ex.Message);
            }
            return backup;
        }

        private static void TryRestoreLoginFlowPatches(int processId, LoginFlowPatchBackup backup, bool loginFlowPatched, string reason)
        {
            if (!loginFlowPatched || !backup.Captured) return;

            try
            {
                MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow1, backup.Patch1);
                MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow2, backup.Patch2);
                MemoryManager.WriteMemory(processId, GameOffsets.PatchLoginFlow3, backup.Patch3);
                AppLogger.Info("Login flow patches restored for fallback path. Reason=" + reason);
                AppLogger.Debug("launch", "Login flow patches restored successfully. reason=" + reason);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Login flow patch restore failed: " + ex.Message + " Reason=" + reason);
            }
        }

        private void ShowGameLaunchPanel(string title, string subtitle)
        {
            LinkWebsite.IsEnabled = false;
            LinkLatestNews.IsEnabled = false;
            LinkRepair.IsEnabled = false;
            LinkLogIn.IsEnabled = false;
            LinkRegister.IsEnabled = false;
            GameStartButton.Visibility = Visibility.Hidden;
            GameLaunchPanel.Visibility = Visibility.Visible;
            SetGameLaunchStatusOnUi(title, subtitle);
        }

        private static void SetGameLaunchStatus(string title, string subtitle)
        {
            try
            {
                if (MW == null) return;
                MW.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (MW != null) MW.SetGameLaunchStatusOnUi(title, subtitle);
                }));
            }
            catch { }
        }

        private void SetGameLaunchStatusOnUi(string title, string subtitle)
        {
            if (!string.IsNullOrWhiteSpace(title)) GameLaunchTitle.Text = title;
            if (subtitle != null) GameLaunchSubText.Text = subtitle;
        }

        private static void ShowLaunchFailed(string title, string subtitle)
        {
            try
            {
                if (MW == null) return;
                MW.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (MW == null) return;
                    MW.SetGameLaunchStatusOnUi(title, subtitle);
                    MessageDialog.Display("Error", subtitle);
                }));
            }
            catch { }
        }

        private static void ResetGameLaunchPanel()
        {
            try
            {
                if (MW == null) return;
                MW.Dispatcher.BeginInvoke(new Action(delegate()
                {
                    if (MW == null) return;
                    MW._launchingGame = false;
                    MW.GameLaunchPanel.Visibility = Visibility.Collapsed;
                    MW.LinkWebsite.IsEnabled = true;
                    MW.LinkLatestNews.IsEnabled = true;
                    MW.LinkRepair.IsEnabled = true;
                    MW.LinkLogIn.IsEnabled = true;
                    MW.LinkRegister.IsEnabled = true;
                    MW.CanStartGame();
                }));
            }
            catch { }
        }
    }
}

