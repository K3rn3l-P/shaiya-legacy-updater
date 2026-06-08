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
        public string SahKeyHex;
        public string LaunchToken;  // token effimero dal server, passato via SDEV_AUTH_KEY
        public byte? LogInType;
        private bool _launchingGame;
        private bool _newsLoaded;
        private bool _rankingLoaded;
        private bool _bossesLoaded;
        private string _activeTab = "News";
        private System.Windows.Threading.DispatcherTimer _bossTimer;
        private readonly List<(long nextSpawnUtc, TextBlock timerBlock)> _bossTimerTargets
            = new List<(long, TextBlock)>();

        public MainWindow()
        {
            InitializeComponent();
            AppConfig.Load();
            MW = this;
            LinkWebsite.Link = AppConfig.WebsiteUrl;
            LinkRegister.Link = AppConfig.RegisterUrl;
            // Patching starts only after login (so SahCrypt key from login response is available).

            LinkAnotherAccount = new iMenuItem
            {
                Text = "Log in using another account",
                Link = "Updater:LogInNewAccount",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 18.0,
                Width = 270
            };
            RefreshLogInView();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private static SolidColorBrush Cb(byte a, byte r, byte g, byte b) { return new SolidColorBrush(Color.FromArgb(a, r, g, b)); }

        private void ButtonMinimize_MouseEnter(object sender, MouseEventArgs e) { ButtonMinimize.Fill = Cb(0x26, 0xE8, 0xC8, 0x81); }
        private void ButtonMinimize_MouseLeave(object sender, MouseEventArgs e) { ButtonMinimize.Fill = new SolidColorBrush(Colors.Transparent); }
        private void ButtonMinimize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { ButtonMinimize.Fill = Cb(0x44, 0xE8, 0xC8, 0x81); }
        private void ButtonMinimize_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { ButtonMinimize.Fill = new SolidColorBrush(Colors.Transparent); WindowState = WindowState.Minimized; }
        private void ButtonClose_MouseEnter(object sender, MouseEventArgs e) { ButtonClose.Fill = Cb(0x26, 0xF4, 0x6A, 0x6A); }
        private void ButtonClose_MouseLeave(object sender, MouseEventArgs e) { ButtonClose.Fill = new SolidColorBrush(Colors.Transparent); }
        private void ButtonClose_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { ButtonClose.Fill = Cb(0x44, 0xF4, 0x6A, 0x6A); }
        private void ButtonClose_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { ButtonClose.Fill = new SolidColorBrush(Colors.Transparent); Close(); }
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
            SetActiveTab(_activeTab);
        }

        private void TabBtn_Click(object sender, MouseButtonEventArgs e)
        {
            SetActiveTab((string)((FrameworkElement)sender).Tag);
        }

        private void SetActiveTab(string tab)
        {
            _activeTab = tab;
            SetTabStyle(TabBtnNews, TabTxtNews, tab == "News");
            SetTabStyle(TabBtnRanking, TabTxtRanking, tab == "Ranking");
            SetTabStyle(TabBtnBosses, TabTxtBosses, tab == "Bosses");
            NewsScrollViewer.Visibility   = tab == "News"    ? Visibility.Visible : Visibility.Collapsed;
            RankingScrollViewer.Visibility = tab == "Ranking" ? Visibility.Visible : Visibility.Collapsed;
            BossesScrollViewer.Visibility  = tab == "Bosses"  ? Visibility.Visible : Visibility.Collapsed;
            if (tab == "News"    && !_newsLoaded)    LoadNews();
            if (tab == "Ranking" && !_rankingLoaded) LoadRankings();
            if (tab == "Bosses"  && !_bossesLoaded)  LoadBosses();
        }

        private void BtnRefreshTab_MouseEnter(object sender, MouseEventArgs e)
        {
            BtnRefreshTab.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8C881"));
        }

        private void BtnRefreshTab_MouseLeave(object sender, MouseEventArgs e)
        {
            BtnRefreshTab.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DE8C881"));
        }

        private void BtnRefreshTab_Click(object sender, MouseButtonEventArgs e)
        {
            if (_activeTab == "News")    { _newsLoaded    = false; LoadNews();     }
            else if (_activeTab == "Ranking") { _rankingLoaded = false; LoadRankings(); }
            else if (_activeTab == "Bosses")  { _bossesLoaded  = false; LoadBosses();   }
        }

        private static void SetTabStyle(Border btn, TextBlock txt, bool active)
        {
            btn.Background   = active ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2DE8C881"))
                                      : new SolidColorBrush(Colors.Transparent);
            btn.BorderBrush  = active ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DE8C881"))
                                      : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AE8C881"));
            txt.Foreground   = active ? (Brush)new BrushConverter().ConvertFrom("#E8C881")
                                      : (Brush)new BrushConverter().ConvertFrom("#7BE8C881");
            txt.FontWeight   = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private void LoadNews()
        {
            _newsLoaded = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string rawUrl = string.IsNullOrEmpty(AppConfig.NewsUrl) ? "api/news.php" : AppConfig.NewsUrl;
                    string url = AppConfig.CombineUrl(AppConfig.BaseUrl, rawUrl);
                    var resp = JsonHttpClient.Get<WebNewsResponse>(url);
                    Dispatcher.Invoke(() => PopulateNews(resp));
                }
                catch
                {
                    Dispatcher.Invoke(() => PopulateNews(null));
                }
            });
        }

        private void PopulateNews(WebNewsResponse resp)
        {
            NewsPanel.Children.Clear();
            if (resp == null || !resp.ok || resp.news == null || resp.news.Count == 0)
            {
                NewsPanel.Children.Add(MakeNewsPlaceholder("No news available."));
                return;
            }
            foreach (var item in resp.news)
                NewsPanel.Children.Add(BuildNewsCard(item));
        }

        private static TextBlock MakeNewsPlaceholder(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9BE0E0E0")),
                FontFamily = new FontFamily("pack://application:,,,/resources/fonts/#Inter"),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 0)
            };
        }

        private static FrameworkElement BuildNewsCard(WebNewsItem item)
        {
            var gold = (Brush)new BrushConverter().ConvertFrom("#E8C881");
            var grayDate = (Brush)new BrushConverter().ConvertFrom("#99E0E0E0");
            var bodyColor = (Brush)new BrushConverter().ConvertFrom("#C0E0E0E0");
            var cinzel = new FontFamily("pack://application:,,,/resources/fonts/#Cinzel");
            var inter = new FontFamily("pack://application:,,,/resources/fonts/#Inter");

            // header: title left, date right
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = item.title ?? "",
                Foreground = gold,
                FontFamily = cinzel,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);

            var dateBlock = new TextBlock
            {
                Text = item.date ?? "",
                Foreground = grayDate,
                FontFamily = inter,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(dateBlock, 1);

            headerGrid.Children.Add(titleBlock);
            headerGrid.Children.Add(dateBlock);

            var panel = new StackPanel();
            panel.Children.Add(headerGrid);

            if (!string.IsNullOrWhiteSpace(item.preview))
            {
                panel.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Height = 1,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2DE8C881")),
                    Margin = new Thickness(0, 8, 0, 8)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = item.preview,
                    Foreground = bodyColor,
                    FontFamily = inter,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99121216")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DE8C881")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 12, 16, 12),
                Child = panel
            };
        }

        // ── RANKINGS ──────────────────────────────────────────────────────────

        private void LoadRankings()
        {
            _rankingLoaded = true;
            RankingRows.Children.Clear();
            RankingRows.Children.Add(MakeNewsPlaceholder("Loading..."));
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string url = AppConfig.CombineUrl(AppConfig.BaseUrl, "api/rankings.php");
                    var resp = JsonHttpClient.Get<RankingsResponse>(url);
                    Dispatcher.Invoke(() => PopulateRankings(resp));
                }
                catch { Dispatcher.Invoke(() => PopulateRankings(null)); }
            });
        }

        private void PopulateRankings(RankingsResponse resp)
        {
            RankingRows.Children.Clear();
            if (resp == null || !resp.ok || resp.rankings == null || resp.rankings.Count == 0)
            { RankingRows.Children.Add(MakeNewsPlaceholder("No rankings data.")); return; }

            RankingRows.Children.Add(BuildRankingRow(null));
            RankingRows.Children.Add(new System.Windows.Shapes.Rectangle
            { Height = 1, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2DE8C881")), Margin = new Thickness(0, 2, 0, 4) });

            foreach (var item in resp.rankings)
                RankingRows.Children.Add(BuildRankingRow(item));
        }

        private static FrameworkElement BuildRankingRow(RankingItem item)
        {
            bool isHeader = item == null;
            var cinzel = new FontFamily("pack://application:,,,/resources/fonts/#Cinzel");
            var inter  = new FontFamily("pack://application:,,,/resources/fonts/#Inter");
            var gold   = (Brush)new BrushConverter().ConvertFrom("#E8C881");
            var hdrClr = (Brush)new BrushConverter().ConvertFrom("#7BE8C881");
            var body   = (Brush)new BrushConverter().ConvertFrom("#C8E0E0E0");

            var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            int[] widths = { 36, 170, 100, 44, 80, 150, 60, 40 };
            foreach (int w in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w) });

            string[] values = isHeader
                ? new[] { "#", "Name", "Class", "Lv", "Kills", "Guild", "Side", "●" }
                : new[] { item.rank.ToString(), item.name, item.className, item.level.ToString(),
                          item.kills.ToString("N0"), item.guild ?? "",
                          item.country == 0 ? "AoL" : "UoF",
                          item.online ? "●" : "" };

            for (int i = 0; i < values.Length; i++)
            {
                Brush fg = isHeader ? hdrClr : (i == 0 ? gold : body);
                if (!isHeader)
                {
                    if (i == 6) fg = item.country == 0
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6AAFFF"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF7070"));
                    if (i == 7 && item.online) fg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
                }

                var tb = new TextBlock
                {
                    Text = values[i],
                    FontFamily = isHeader ? cinzel : inter,
                    FontSize = isHeader ? 10 : 12,
                    Foreground = fg,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(0, 2, 4, 2)
                };
                Grid.SetColumn(tb, i);
                grid.Children.Add(tb);
            }
            return grid;
        }

        // ── BOSSES ────────────────────────────────────────────────────────────

        private void LoadBosses()
        {
            _bossesLoaded = true;
            BossesPanel.Children.Clear();
            BossesPanel.Children.Add(MakeNewsPlaceholder("Loading..."));
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string url = AppConfig.CombineUrl(AppConfig.BaseUrl, "api/bosses.php");
                    var resp = JsonHttpClient.Get<BossesResponse>(url);
                    Dispatcher.Invoke(() => PopulateBosses(resp));
                }
                catch { Dispatcher.Invoke(() => PopulateBosses(null)); }
            });
        }

        private void PopulateBosses(BossesResponse resp)
        {
            _bossTimer?.Stop();
            _bossTimerTargets.Clear();
            BossesPanel.Children.Clear();

            if (resp == null || !resp.ok || resp.bosses == null || resp.bosses.Count == 0)
            { BossesPanel.Children.Add(MakeNewsPlaceholder("No boss data.")); return; }

            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var boss in resp.bosses)
            {
                var (card, timerBlock) = BuildBossCard(boss, nowUtc);
                BossesPanel.Children.Add(card);
                if (timerBlock != null)
                    _bossTimerTargets.Add((nowUtc + boss.secondsUntilSpawn, timerBlock));
            }

            if (_bossTimerTargets.Count > 0)
            {
                _bossTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromSeconds(1) };
                _bossTimer.Tick += BossTimer_Tick;
                _bossTimer.Start();
            }
        }

        private void BossTimer_Tick(object sender, EventArgs e)
        {
            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var green = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
            var gold  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8C881"));

            for (int i = _bossTimerTargets.Count - 1; i >= 0; i--)
            {
                var (nextSpawnUtc, timerBlock) = _bossTimerTargets[i];
                long remaining = nextSpawnUtc - nowUtc;
                if (remaining <= 0)
                {
                    timerBlock.Text = "ALIVE";
                    timerBlock.Foreground = green;
                    _bossTimerTargets.RemoveAt(i);
                }
                else
                {
                    int h = (int)(remaining / 3600);
                    int m = (int)((remaining % 3600) / 60);
                    int s = (int)(remaining % 60);
                    timerBlock.Text = $"{h:D2}h {m:D2}m {s:D2}s";
                    timerBlock.Foreground = gold;
                }
            }
        }

        private static (Border card, TextBlock timerBlock) BuildBossCard(BossItem boss, long nowUtc)
        {
            var gold   = (Brush)new BrushConverter().ConvertFrom("#E8C881");
            var gray   = (Brush)new BrushConverter().ConvertFrom("#99E0E0E0");
            var body   = (Brush)new BrushConverter().ConvertFrom("#C0E0E0E0");
            var green  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
            var cinzel = new FontFamily("pack://application:,,,/resources/fonts/#Cinzel");
            var inter  = new FontFamily("pack://application:,,,/resources/fonts/#Inter");

            bool alive = boss.secondsUntilSpawn <= 0;

            // Header: name left, timer right
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = boss.name ?? "",
                Foreground = gold, FontFamily = cinzel, FontSize = 13, FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameBlock, 0);

            TextBlock timerBlock = new TextBlock
            {
                FontFamily = cinzel, FontSize = 13, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
            };
            if (alive)
            {
                timerBlock.Text = "ALIVE";
                timerBlock.Foreground = green;
            }
            else
            {
                int h = boss.secondsUntilSpawn / 3600;
                int m = (boss.secondsUntilSpawn % 3600) / 60;
                int s = boss.secondsUntilSpawn % 60;
                timerBlock.Text = $"{h:D2}h {m:D2}m {s:D2}s";
                timerBlock.Foreground = gold;
            }
            Grid.SetColumn(timerBlock, 1);
            header.Children.Add(nameBlock);
            header.Children.Add(timerBlock);

            // Sub-line: Lv + map + killer
            string sub = $"Lv {boss.level}  ·  {boss.mapName}";
            if (!string.IsNullOrEmpty(boss.lastKilledBy))
                sub += $"  ·  Last kill: {boss.lastKilledBy}";

            var subBlock = new TextBlock
            {
                Text = sub, Foreground = gray, FontFamily = inter, FontSize = 11,
                Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis
            };

            var panel = new StackPanel();
            panel.Children.Add(header);
            panel.Children.Add(subBlock);

            var card = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99121216")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DE8C881")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(14, 10, 14, 10),
                Child = panel
            };

            return (card, alive ? null : timerBlock);
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

                // data.sah stays encrypted (ESAH) — sdev-client.dll decrypts at runtime via SDEV_SAH_KEY.
                // EnsureDataSahReadyForLaunch() is intentionally skipped.

                ProcessStartInfo psi = new ProcessStartInfo(gamePath, AppConfig.GameArguments)
                {
                    WorkingDirectory = AppPaths.BaseDirectory,
                    UseShellExecute = false   // required to pass EnvironmentVariables
                };

                // Ensure data.safb sidecar is current before game.exe loads the VFS.
                if (AppConfig.SahCryptEnabled && !string.IsNullOrWhiteSpace(MW.SahKeyHex))
                    SafbSidecar.Regenerate(
                        AppPaths.InBase("data.sah"),
                        MW.SahKeyHex,
                        AppPaths.InBase("data.safb"));

                // Pass SAH decryption key to the child process (sdev-client reads and zeros it at DllMain).
                // Key is received from server only after successful authentication — never stored locally.
                if (AppConfig.SahCryptEnabled && !string.IsNullOrWhiteSpace(MW.SahKeyHex))
                    psi.EnvironmentVariables["SDEV_SAH_KEY"] = MW.SahKeyHex;

                // Token effimero pre-computato dal server (HMAC "client:"+window, ClientSecret).
                // Client NON ha il segreto — solo il token già pronto, valido ~10 min.
                // sdev-login.dll verifica lato server con lo stesso ClientSecret da GMSecurity.ini.
                if (!string.IsNullOrWhiteSpace(MW.LaunchToken))
                    psi.EnvironmentVariables["SDEV_AUTH_KEY"] = MW.LaunchToken;

                if (!VerifyClientDllIntegrity(AppPaths.BaseDirectory))
                    throw new InvalidOperationException("Client integrity check failed. Please use Repair to restore the client files.");

                Process process = Process.Start(psi);
                if (process == null) throw new InvalidOperationException("Game process could not be started.");

                launchedProcess = process;
                processId = process.Id;
                AppLogger.Debug("launch", "Game process started. pid=" + processId);
                gameWindow = GameWindowController.HideUntilReady(processId);
                try { TryInjectCredentials(processId, out success); }
                finally { MW.TempPw = null; MW.LaunchToken = null; MW.SahKeyHex = null; }
            }
            catch (Exception ex)
            {
                ExceptionManager.Submit(ex);
                AppLogger.Debug("launch", "LaunchGameWorker exception. pid=" + processId + " msg=" + ex.Message);
                if (processId != 0)
                {
                    try { Process.GetProcessById(processId).Kill(); } catch { }
                }
                string uiMsg = ex is InvalidOperationException ? ex.Message : "Please check updater.log and try again.";
                ShowLaunchFailed("Launch failed", uiMsg);
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
            LinkRepair.IsEnabled = false;
            LinkLogIn.IsEnabled = false;
            LinkRegister.IsEnabled = false;
            BtnRefreshTab.IsEnabled = false;
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
                    MW.LinkRepair.IsEnabled = true;
                    MW.LinkLogIn.IsEnabled = true;
                    MW.LinkRegister.IsEnabled = true;
                    MW.BtnRefreshTab.IsEnabled = true;
                    MW.CanStartGame();
                }));
            }
            catch { }
        }

        // Verifica SHA256 di sdev.dll contro l'entry nell'array files[] del manifest.
        // Se il manifest non ha l'entry (o sha256 è vuoto), il check è skippato (opt-in).
        // Ritorna false solo se l'hash è configurato E non corrisponde.
        private static bool VerifyClientDllIntegrity(string gameDir)
        {
            const string dllName = "sdev.dll";
            var manifest = Updater.Manifest;
            if (manifest == null)
            {
                AppLogger.Debug("integrity", "Manifest null — skip check.");
                return true;
            }
            if (manifest.files == null || manifest.files.Length == 0)
            {
                AppLogger.Debug("integrity", "Manifest files[] vuoto — skip check.");
                return true;
            }

            var entry = System.Array.Find(manifest.files, f =>
                string.Equals(f.file, dllName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.path, dllName, StringComparison.OrdinalIgnoreCase));

            if (entry == null || string.IsNullOrEmpty(entry.sha256))
            {
                AppLogger.Debug("integrity", dllName + " non in manifest files[] — skip check.");
                return true;
            }

            var dllPath = System.IO.Path.Combine(gameDir, dllName);
            if (!System.IO.File.Exists(dllPath))
            {
                AppLogger.Warn(dllName + " non trovato — integrity check fallito.");
                return false;
            }

            string actual;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs = System.IO.File.OpenRead(dllPath))
                actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();

            bool ok = string.Equals(actual, entry.sha256.ToLowerInvariant(), StringComparison.Ordinal);
            AppLogger.Debug("integrity", dllName + " check=" + (ok ? "OK" : "FAIL") + " expected=" + entry.sha256 + " actual=" + actual);
            if (!ok) AppLogger.Warn(dllName + " hash mismatch — client files corrotti.");
            return ok;
        }

        // ─── SETTINGS PANEL ──────────────────────────────────────────────────────

        private sealed class ConfigSetting
        {
            public string Section;
            public string Key;
            public Func<string> GetValue;
        }

        private readonly List<ConfigSetting> _configSettings = new List<ConfigSetting>();
        private bool _settingsBuilt;
        private string _setActiveTab = "Video";

        private static readonly SolidColorBrush _setActiveBg      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2DE8C881"));
        private static readonly SolidColorBrush _setActiveBorder   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4DE8C881"));
        private static readonly SolidColorBrush _setActiveFg      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8C881"));
        private static readonly SolidColorBrush _setInactiveBg    = new SolidColorBrush(Colors.Transparent);
        private static readonly SolidColorBrush _setInactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AE8C881"));
        private static readonly SolidColorBrush _setInactiveFg    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7BE8C881"));
        private static readonly SolidColorBrush _setLabelFg       = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B0E0E0E0"));
        private static readonly SolidColorBrush _setSecFg         = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7BE8C881"));
        private static readonly SolidColorBrush _setDivFill       = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2DE8C881"));
        private static readonly FontFamily _setFontInter  = new FontFamily("pack://application:,,,/resources/fonts/#Inter");
        private static readonly FontFamily _setFontCinzel = new FontFamily("pack://application:,,,/resources/fonts/#Cinzel");

        private void BtnOpenSettings_MouseEnter(object sender, MouseEventArgs e) { ((TextBlock)sender).Foreground = _setActiveFg; }
        private void BtnOpenSettings_MouseLeave(object sender, MouseEventArgs e) { ((TextBlock)sender).Foreground = _setInactiveFg; }
        private void BtnSettingsClose_MouseEnter(object sender, MouseEventArgs e) { ((TextBlock)sender).Foreground = _setActiveFg; }
        private void BtnSettingsClose_MouseLeave(object sender, MouseEventArgs e) { ((TextBlock)sender).Foreground = _setInactiveFg; }
        private void BtnSettingsSave_MouseEnter(object sender, MouseEventArgs e) { ((Border)sender).Background = _setActiveBg; }
        private void BtnSettingsSave_MouseLeave(object sender, MouseEventArgs e) { ((Border)sender).Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AE8C881")); }

        private void BtnOpenSettings_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_settingsBuilt) { BuildSettingsContent(); _settingsBuilt = true; }
            SettingsOverlay.Visibility = Visibility.Visible;
            ApplySetTab(_setActiveTab);
        }

        private void BtnSettingsOverlay_Close(object sender, MouseButtonEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void SetTab_Click(object sender, MouseButtonEventArgs e)
        {
            ApplySetTab((string)((FrameworkElement)sender).Tag);
        }

        private void ApplySetTab(string tab)
        {
            _setActiveTab = tab;
            StyleSetTab(SetTabVideo,     SetTabVideoTxt,     tab == "Video");
            StyleSetTab(SetTabSound,     SetTabSoundTxt,     tab == "Sound");
            StyleSetTab(SetTabAdvanced,  SetTabAdvancedTxt,  tab == "Advanced");
            StyleSetTab(SetTabInterface, SetTabInterfaceTxt, tab == "Interface");
            SetScrollVideo.Visibility     = tab == "Video"     ? Visibility.Visible : Visibility.Collapsed;
            SetScrollSound.Visibility     = tab == "Sound"     ? Visibility.Visible : Visibility.Collapsed;
            SetScrollAdvanced.Visibility  = tab == "Advanced"  ? Visibility.Visible : Visibility.Collapsed;
            SetScrollInterface.Visibility = tab == "Interface" ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void StyleSetTab(Border btn, TextBlock txt, bool active)
        {
            btn.Background  = active ? _setActiveBg    : _setInactiveBg;
            btn.BorderBrush = active ? _setActiveBorder : _setInactiveBorder;
            txt.Foreground  = active ? (Brush)_setActiveFg : (Brush)_setInactiveFg;
            txt.FontWeight  = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private void BtnSettingsSave_Click(object sender, MouseButtonEventArgs e) { SaveSettings(); }

        private void SaveSettings()
        {
            string path = AppPaths.InBase(AppConfig.ClientConfigFile);
            if (!File.Exists(path)) { MessageDialog.Display("Settings", "CONFIG.INI not found at:\n" + path); return; }
            try
            {
                var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in _configSettings) changes[s.Section + "::" + s.Key] = s.GetValue();
                ConfigIniSaver.Save(path, changes);
                SettingsOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) { MessageDialog.Display("Settings Error", "Could not save CONFIG.INI:\n" + ex.Message); }
        }

        private void BuildSettingsContent()
        {
            string path = AppPaths.InBase(AppConfig.ClientConfigFile);
            IniFile ini = IniFile.Load(path);

            // ── VIDEO ────────────────────────────────────────────────────────
            string sizeX = ini.Get("VIDEO", "SIZE_X", "1920");
            string sizeY = ini.Get("VIDEO", "SIZE_Y", "1080");
            string resKey = sizeX + "x" + sizeY;

            string[] resOpts   = { "1280x720", "1366x768", "1600x900", "1920x1080", "2560x1440", "3840x2160" };
            string[] resLabels = { "1280 × 720", "1366 × 768", "1600 × 900", "1920 × 1080", "2560 × 1440", "3840 × 2160" };
            bool resFound = false;
            foreach (string r in resOpts) if (string.Equals(r, resKey, StringComparison.OrdinalIgnoreCase)) { resFound = true; break; }
            if (!resFound) resKey = "1920x1080";

            string[] resRef;
            StackPanel resWidget = MakeChoiceWidget(resOpts, resLabels, resKey, out resRef);
            _configSettings.Add(new ConfigSetting { Section = "VIDEO", Key = "SIZE_X", GetValue = () => resRef[0].Split('x')[0] });
            _configSettings.Add(new ConfigSetting { Section = "VIDEO", Key = "SIZE_Y", GetValue = () => resRef[0].Split('x')[1] });

            SetPanelVideo.Children.Add(SetSectionHeader("Display"));
            SetPanelVideo.Children.Add(SetRow("Resolution", resWidget));
            SetPanelVideo.Children.Add(SetToggleRow("Fullscreen",      ini.GetBool("VIDEO","FULLSCREEN",  false), "VIDEO","FULLSCREEN"));

            SetPanelVideo.Children.Add(SetSectionHeader("Graphics"));
            SetPanelVideo.Children.Add(SetChoiceRow("Texture Quality", new[]{"LOW","MEDIUM","HIGH"}, null,       ini.Get("VIDEO","TEXTURE","HIGH"),              "VIDEO","TEXTURE"));
            SetPanelVideo.Children.Add(SetChoiceRow("View Range",      new[]{"LOW","MEDIUM","HIGH"}, null,       ini.Get("VIDEO","RANGE","HIGH"),                "VIDEO","RANGE"));
            SetPanelVideo.Children.Add(SetChoiceRow("Glow Level",      new[]{"0","1","2","3"},       null,       ini.GetInt("VIDEO","GLOW_LEVEL",2).ToString(),  "VIDEO","GLOW_LEVEL"));
            SetPanelVideo.Children.Add(SetChoiceRow("Gamma",           new[]{"1","2","3","4","5"},   null,       ini.GetInt("VIDEO","GAMMA",4).ToString(),       "VIDEO","GAMMA"));

            SetPanelVideo.Children.Add(SetSectionHeader("Visual"));
            SetPanelVideo.Children.Add(SetToggleRow("Water Effects",  ini.GetBool("VIDEO","WATER",     true),  "VIDEO","WATER"));
            SetPanelVideo.Children.Add(SetToggleRow("Shadow",         ini.GetBool("VIDEO","SHADOW",    true),  "VIDEO","SHADOW"));
            SetPanelVideo.Children.Add(SetToggleRow("Show Helmet",    ini.GetBool("VIDEO","HELMET",    true),  "VIDEO","HELMET"));
            SetPanelVideo.Children.Add(SetToggleRow("Show Cloak",     ini.GetBool("VIDEO","CLOAK",     true),  "VIDEO","CLOAK"));
            SetPanelVideo.Children.Add(SetToggleRow("Weapon Effects", ini.GetBool("VIDEO","WEAPONEFF", true),  "VIDEO","WEAPONEFF"));

            // ── SOUND ────────────────────────────────────────────────────────
            SetPanelSound.Children.Add(SetSectionHeader("Volume"));
            SetPanelSound.Children.Add(SetChoiceRow("BGM",           new[]{"0","1","2","3","4","5"}, null, ini.GetInt("SOUND","VOL_BGM",2).ToString(),    "SOUND","VOL_BGM"));
            SetPanelSound.Children.Add(SetChoiceRow("Sound Effects", new[]{"0","1","2","3","4","5"}, null, ini.GetInt("SOUND","VOL_EFFECT",2).ToString(), "SOUND","VOL_EFFECT"));
            SetPanelSound.Children.Add(SetChoiceRow("World",         new[]{"0","1","2","3","4","5"}, null, ini.GetInt("SOUND","VOL_WORLD",2).ToString(),  "SOUND","VOL_WORLD"));

            SetPanelSound.Children.Add(SetSectionHeader("Voice"));
            SetPanelSound.Children.Add(SetToggleRow("Character Voices", ini.GetBool("SOUND","VOICE_CHAR",    true), "SOUND","VOICE_CHAR"));
            SetPanelSound.Children.Add(SetToggleRow("NPC Voices",       ini.GetBool("SOUND","VOICE_NPC",     true), "SOUND","VOICE_NPC"));
            SetPanelSound.Children.Add(SetToggleRow("Danger Alert",     ini.GetBool("SOUND","DANGER_DETECT", true), "SOUND","DANGER_DETECT"));

            // ── ADVANCED ─────────────────────────────────────────────────────
            SetPanelAdvanced.Children.Add(SetSectionHeader("Visual Enhancements"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Show Pets",        ini.GetBool("ADVANCED","PETS",      true), "ADVANCED","PETS"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Show Wings",       ini.GetBool("ADVANCED","WINGS",     true), "ADVANCED","WINGS"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Show Costumes",    ini.GetBool("ADVANCED","COSTUMES",  true), "ADVANCED","COSTUMES"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Visual Effects",   ini.GetBool("ADVANCED","EFFECTS",   true), "ADVANCED","EFFECTS"));
            SetPanelAdvanced.Children.Add(SetToggleRow("FPS Boost",        ini.GetBool("ADVANCED","FPS_BOOST", true), "ADVANCED","FPS_BOOST"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Character Titles", ini.GetBool("ADVANCED","TITLES",    true), "ADVANCED","TITLES"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Colour Effects",   ini.GetBool("ADVANCED","COLOUR",    true), "ADVANCED","COLOUR"));

            SetPanelAdvanced.Children.Add(SetSectionHeader("Font"));
            SetPanelAdvanced.Children.Add(SetChoiceRow("Size",
                new[]{"-8","-10","-11","-12","-14","-16"},
                new[]{"8pt","10pt","11pt","12pt","14pt","16pt"},
                ini.Get("FONT","HEIGHT","-12"), "FONT","HEIGHT"));
            SetPanelAdvanced.Children.Add(SetChoiceRow("Weight",
                new[]{"400","700"}, new[]{"Normal","Bold"},
                ini.Get("FONT","WEIGHT","400"), "FONT","WEIGHT"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Italic",
                ini.GetBool("FONT","ITALIC", false), "FONT","ITALIC", numericBool: true));
            SetPanelAdvanced.Children.Add(SetChoiceRow("Face",
                new[]{"Tahoma","Arial","Verdana","Calibri"}, null,
                ini.Get("FONT","FACENAME","Tahoma"), "FONT","FACENAME"));

            SetPanelAdvanced.Children.Add(SetSectionHeader("Game"));
            SetPanelAdvanced.Children.Add(SetChoiceRow("UI Mode", new[]{"EP4","EP6","EP8"}, null, ini.Get("ADVANCED","UI","EP4"), "ADVANCED","UI"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Auto-claim Rewards",       ini.GetBool("IMGUI","REWARD_AUTOCLAIM",       false), "IMGUI","REWARD_AUTOCLAIM"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Skill Bar Lock",           ini.GetBool("VIDEO","SKILLWNDLOCK",           false), "VIDEO","SKILLWNDLOCK"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Allow Multiple Instances", ini.GetBool("APPLICATION","ALLOW_MULTIPROCESS", true), "APPLICATION","ALLOW_MULTIPROCESS"));

            SetPanelAdvanced.Children.Add(SetSectionHeader("Union Party"));
            SetPanelAdvanced.Children.Add(SetToggleRow("Show Union Window",
                ini.GetBool("UNIONPARTY","UNION_WINODW_VISIBLE", true), "UNIONPARTY","UNION_WINODW_VISIBLE"));
            SetPanelAdvanced.Children.Add(SetChoiceRow("Window Opacity",
                new[]{"25","50","73","100","150","200","255"},
                new[]{"10%","20%","29%","39%","59%","78%","100%"},
                ini.GetInt("UNIONPARTY","ALPAH_VALUE", 73).ToString(), "UNIONPARTY","ALPAH_VALUE"));

            SetPanelAdvanced.Children.Add(SetSectionHeader("Launcher"));
            string skipRaw = ini.Get("ADVANCED", "SKIPSERVERSELECTION", "0");
            bool skipVal = skipRaw == "1" || string.Equals(skipRaw, "TRUE", StringComparison.OrdinalIgnoreCase);
            SetPanelAdvanced.Children.Add(SetToggleRow("Skip Server Selection", skipVal, "ADVANCED","SKIPSERVERSELECTION", numericBool: true));

            // ── INTERFACE ([USER] section) ────────────────────────────────────
            SetPanelInterface.Children.Add(SetSectionHeader("Display"));
            SetPanelInterface.Children.Add(SetToggleRow("Show My Name",       ini.GetBool("USER","MY_NAME",       true),  "USER","MY_NAME"));
            SetPanelInterface.Children.Add(SetToggleRow("Show Player Names",  ini.GetBool("USER","USER_NAME",     true),  "USER","USER_NAME"));
            SetPanelInterface.Children.Add(SetToggleRow("Show Monster Names", ini.GetBool("USER","MONSTER_NAME",  true),  "USER","MONSTER_NAME"));
            SetPanelInterface.Children.Add(SetToggleRow("Show Interface",     ini.GetBool("USER","SHOW_INTERFACE",true),  "USER","SHOW_INTERFACE"));
            SetPanelInterface.Children.Add(SetToggleRow("Chat Bubbles",       ini.GetBool("USER","TALK_BALLOON",  true),  "USER","TALK_BALLOON"));
            SetPanelInterface.Children.Add(SetToggleRow("Hide Damage Numbers",ini.GetBool("USER","HIDE_DAMAGE",   false), "USER","HIDE_DAMAGE"));

            SetPanelInterface.Children.Add(SetSectionHeader("Mouse"));
            SetPanelInterface.Children.Add(SetToggleRow("Use Mouse",              ini.GetBool("USER","USE_MOUSE",        true),  "USER","USE_MOUSE"));
            SetPanelInterface.Children.Add(SetToggleRow("Inventory Mouse",        ini.GetBool("USER","INV_MOUSE",        false), "USER","INV_MOUSE"));
            SetPanelInterface.Children.Add(SetToggleRow("Invert Scroll Direction", ini.GetBool("USER","INV_MOUSE_UPDOWN", false), "USER","INV_MOUSE_UPDOWN"));
            SetPanelInterface.Children.Add(SetChoiceRow("Mouse Sensitivity", new[]{"1","2","3","4","5"}, null,
                ini.GetInt("USER","MOUSE_SENSITIVITY",3).ToString(), "USER","MOUSE_SENSITIVITY"));

            SetPanelInterface.Children.Add(SetSectionHeader("Gameplay"));
            SetPanelInterface.Children.Add(SetToggleRow("Screen Swing",    ini.GetBool("USER","SCREEN_SWING", true),  "USER","SCREEN_SWING"));
            SetPanelInterface.Children.Add(SetToggleRow("Water Effects",   ini.GetBool("USER","WATER",        true),  "USER","WATER"));
            SetPanelInterface.Children.Add(SetToggleRow("Shadow",          ini.GetBool("USER","SHADOW",       false), "USER","SHADOW"));
            SetPanelInterface.Children.Add(SetToggleRow("Use Filter",      ini.GetBool("USER","USE_FILTER",   true),  "USER","USE_FILTER"));
            SetPanelInterface.Children.Add(SetToggleRow("Warning Messages",ini.GetBool("USER","WARNING_MSG",  true),  "USER","WARNING_MSG"));
            SetPanelInterface.Children.Add(SetToggleRow("Reopen UI",       ini.GetBool("USER","UI_REOPEN",    true),  "USER","UI_REOPEN"));

            SetPanelInterface.Children.Add(SetSectionHeader("Social"));
            SetPanelInterface.Children.Add(SetToggleRow("Reject Duel Requests",  ini.GetBool("USER","REJECT_FIGHT", false), "USER","REJECT_FIGHT"));
            SetPanelInterface.Children.Add(SetToggleRow("Reject Party Requests", ini.GetBool("USER","REJECT_PARTY", false), "USER","REJECT_PARTY"));
            SetPanelInterface.Children.Add(SetToggleRow("Reject Trade Requests", ini.GetBool("USER","REJECT_TRADE", false), "USER","REJECT_TRADE"));
        }

        private FrameworkElement SetSectionHeader(string title)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 10, 0, 4) };
            sp.Children.Add(new TextBlock
            {
                Text = title.ToUpperInvariant(),
                Foreground = _setSecFg,
                FontFamily = _setFontCinzel,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold
            });
            sp.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Height = 1,
                Margin = new Thickness(0, 4, 0, 0),
                Fill = _setDivFill
            });
            return sp;
        }

        private static FrameworkElement SetRow(string label, UIElement control)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = new TextBlock
            {
                Text = label,
                Foreground = _setLabelFg,
                FontFamily = _setFontInter,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(control, 1);
            g.Children.Add(lbl);
            g.Children.Add(control);
            return g;
        }

        private FrameworkElement SetToggleRow(string label, bool value, string section, string key, bool numericBool = false)
        {
            var cb = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            _configSettings.Add(new ConfigSetting
            {
                Section = section,
                Key = key,
                GetValue = () => numericBool
                    ? (cb.IsChecked == true ? "1" : "0")
                    : (cb.IsChecked == true ? "TRUE" : "FALSE")
            });
            return SetRow(label, cb);
        }

        private FrameworkElement SetChoiceRow(string label, string[] opts, string[] labels, string current, string section, string key)
        {
            string[] valRef;
            StackPanel widget = MakeChoiceWidget(opts, labels, current, out valRef);
            _configSettings.Add(new ConfigSetting { Section = section, Key = key, GetValue = () => valRef[0] });
            return SetRow(label, widget);
        }

        private static StackPanel MakeChoiceWidget(string[] opts, string[] labels, string current, out string[] valRef)
        {
            string[] vr = { current };
            valRef = vr;

            var btnArr = new Border[opts.Length];
            var txtArr = new TextBlock[opts.Length];
            var sp = new StackPanel { Orientation = Orientation.Horizontal };

            for (int i = 0; i < opts.Length; i++)
            {
                bool sel = string.Equals(opts[i], current, StringComparison.OrdinalIgnoreCase);
                var b = new Border
                {
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0),
                    Padding = new Thickness(9, 3, 9, 3),
                    Background  = sel ? _setActiveBg    : _setInactiveBg,
                    BorderBrush = sel ? _setActiveBorder : _setInactiveBorder,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                var t = new TextBlock
                {
                    Text = labels != null ? labels[i] : opts[i],
                    FontFamily = _setFontInter,
                    FontSize = 10,
                    Foreground = sel ? (Brush)_setActiveFg : (Brush)_setInactiveFg,
                    VerticalAlignment = VerticalAlignment.Center
                };
                b.Child = t;
                btnArr[i] = b;
                txtArr[i] = t;
                sp.Children.Add(b);
            }

            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                string optVal = opts[i];
                btnArr[i].MouseLeftButtonUp += (s, e) =>
                {
                    vr[0] = optVal;
                    for (int j = 0; j < btnArr.Length; j++)
                    {
                        bool active = j == idx;
                        btnArr[j].Background  = active ? _setActiveBg    : _setInactiveBg;
                        btnArr[j].BorderBrush = active ? _setActiveBorder : _setInactiveBorder;
                        txtArr[j].Foreground  = active ? (Brush)_setActiveFg : (Brush)_setInactiveFg;
                    }
                };
            }

            return sp;
        }
    }
}

