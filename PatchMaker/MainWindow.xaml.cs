using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PatchMaker
{
    public partial class MainWindow : Window
    {
        private PatchMakerSettings _settings;
        private SshService _ssh;
        private string _lastPatchPath;

        public MainWindow()
        {
            InitializeComponent();
            _settings = PatchMakerSettings.Load();

            // Applica lingua salvata prima di popolare UI
            if (_settings.Language == "En")
                L.Switch(L.Lang.En);

            UpdateLangButtons();
            LoadSettingsToUI();
            SetConnStatus(false);
            BtnConnect.Content = L.BtnConnect;
            Log(L.Startup);

            // Aggiorna testi dinamici quando cambia lingua
            L.Changed += OnLangChanged;
        }

        private void OnLangChanged()
        {
            UpdateLangButtons();
            SetConnStatus(_ssh != null && _ssh.IsConnected);
            if (_ssh != null && _ssh.IsConnected)
                BtnConnect.Content = L.BtnDisconnect;
            else
                BtnConnect.Content = L.BtnConnect;
        }

        // ── Language switch ────────────────────────────────────────

        private void BtnLangIt_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SwitchLanguage(L.Lang.It);

        private void BtnLangEn_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SwitchLanguage(L.Lang.En);

        private void SwitchLanguage(L.Lang lang)
        {
            L.Switch(lang);
            _settings.Language = lang == L.Lang.En ? "En" : "It";
            _settings.Save();
        }

        private void UpdateLangButtons()
        {
            bool itActive = L.Current == L.Lang.It;

            BtnLangIt.Background   = itActive ? new SolidColorBrush(Color.FromArgb(0x20, 0xE8, 0xC8, 0x81))
                                               : Brushes.Transparent;
            BtnLangIt.BorderBrush  = itActive ? new SolidColorBrush(Color.FromArgb(0x7B, 0xE8, 0xC8, 0x81))
                                               : new SolidColorBrush(Color.FromArgb(0x15, 0xE8, 0xC8, 0x81));
            TbLangIt.Foreground    = itActive ? new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xC8, 0x81))
                                               : new SolidColorBrush(Color.FromArgb(0x35, 0xE8, 0xC8, 0x81));

            BtnLangEn.Background   = !itActive ? new SolidColorBrush(Color.FromArgb(0x20, 0xE8, 0xC8, 0x81))
                                                : Brushes.Transparent;
            BtnLangEn.BorderBrush  = !itActive ? new SolidColorBrush(Color.FromArgb(0x7B, 0xE8, 0xC8, 0x81))
                                                : new SolidColorBrush(Color.FromArgb(0x15, 0xE8, 0xC8, 0x81));
            TbLangEn.Foreground    = !itActive ? new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xC8, 0x81))
                                                : new SolidColorBrush(Color.FromArgb(0x35, 0xE8, 0xC8, 0x81));
        }

        // ── Settings ──────────────────────────────────────────────

        private void LoadSettingsToUI()
        {
            TbSshHost.Text        = _settings.SshHost;
            TbSshUser.Text        = _settings.SshUser;
            PbSshPass.Password    = _settings.SshPassword;
            TbSshPatchesPath.Text = _settings.SshPatchesPath;
            TbSshHtdocsPath.Text  = _settings.SshHtdocsPath;
            TbWorkspace.Text      = _settings.ResolvedWorkspace;
            TbSahKey.Text         = _settings.SahKeyHex;
        }

        private void SaveSettingsFromUI()
        {
            _settings.SshHost        = TbSshHost.Text.Trim();
            _settings.SshUser        = TbSshUser.Text.Trim();
            _settings.SshPassword    = PbSshPass.Password;
            _settings.SshPatchesPath = TbSshPatchesPath.Text.Trim();
            _settings.SshHtdocsPath  = TbSshHtdocsPath.Text.Trim();
            _settings.WorkspacePath  = TbWorkspace.Text.Trim();
            _settings.SahKeyHex      = TbSahKey.Text.Trim();
            _settings.Save();
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            Log(L.SettingsSaved);
        }

        private async void BtnReadKey_Click(object sender, RoutedEventArgs e)
        {
            if (_ssh != null && _ssh.IsConnected)
            {
                string remotePath = PatchMakerSettings.DefaultGmSecurityServerPath;
                Log(L.ReadingGmSec(remotePath));
                string content = null;
                await RunAsync(() => { content = _ssh.ReadTextFile(remotePath); });
                if (content != null)
                {
                    string k = PatchMakerSettings.ParseKeyFromIniContent(content);
                    if (k != null) { TbSahKey.Text = k; Log(L.KeyReadFromServer(k.Length)); return; }
                    Log(L.ErrKeyNotFoundServer);
                    return;
                }
                Log(L.SshReadFailed);
            }

            var dlg = new OpenFileDialog { Filter = "INI files|*.ini|All files|*.*" };
            if (dlg.ShowDialog() != true) return;
            string key = PatchMakerSettings.ReadKeyFromGmSecurityIni(dlg.FileName);
            if (key == null) { Log(L.ErrKeyNotFoundLocal(dlg.FileName)); return; }
            TbSahKey.Text = key;
            Log(L.KeyReadFromFile(Path.GetFileName(dlg.FileName), key.Length));
        }

        // ── Workspace ─────────────────────────────────────────────

        private void BtnBrowseWorkspace_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    TbWorkspace.Text = dlg.SelectedPath;
            }
        }

        private void BtnCreateWorkspace_Click(object sender, RoutedEventArgs e)
        {
            string ws = TbWorkspace.Text.Trim();
            if (string.IsNullOrWhiteSpace(ws)) { Log(L.WsPathRequired); return; }
            Directory.CreateDirectory(Path.Combine(ws, "data"));
            Directory.CreateDirectory(Path.Combine(ws, "output"));
            Log(L.WsCreated(ws));
            Log(L.WsFilesHint(Path.Combine(ws, "data") + "\\"));
            Log(L.WsExamples);
            Log("       data\\Assets\\General\\Roulette.png");
            Log("       data\\Item\\Item.SData");
            Log(L.WsOutputHint(Path.Combine(ws, "output")));
        }

        private void BtnOpenWorkspace_Click(object sender, RoutedEventArgs e)
        {
            string ws = TbWorkspace.Text.Trim();
            if (string.IsNullOrWhiteSpace(ws)) return;
            if (Directory.Exists(ws)) Process.Start("explorer.exe", ws);
        }

        // ── SSH ───────────────────────────────────────────────────

        private async void BtnTestSsh_Click(object sender, RoutedEventArgs e)
        {
            BtnTest.IsEnabled = false;
            SaveSettingsFromUI();
            await RunAsync(() =>
            {
                using (var svc = new SshService(_settings, Log))
                {
                    svc.Connect();
                    Log(L.SshTestOk);
                }
            });
            BtnTest.IsEnabled = true;
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (_ssh != null && _ssh.IsConnected)
            {
                _ssh.Dispose(); _ssh = null;
                SetConnStatus(false);
                BtnConnect.Content = L.BtnConnect;
                return;
            }

            BtnConnect.IsEnabled = false;
            SaveSettingsFromUI();
            await RunAsync(() =>
            {
                _ssh?.Dispose();
                _ssh = new SshService(_settings, Log);
                _ssh.Connect();
            });

            if (_ssh != null && _ssh.IsConnected)
            {
                SetConnStatus(true);
                BtnConnect.Content = L.BtnDisconnect;
            }
            else
            {
                SetConnStatus(false);
                BtnConnect.Content = L.BtnConnect;
            }
            BtnConnect.IsEnabled = true;
        }

        private void SetConnStatus(bool connected)
        {
            if (connected)
            {
                TbConnStatus.Text       = L.StatusConnected;
                TbConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4D, 0xC8, 0x70));
            }
            else
            {
                TbConnStatus.Text       = L.StatusDisconnected;
                TbConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xBB, 0x44, 0x44));
            }
        }

        // ── Patch build + upload ───────────────────────────────────

        private void ChkDeleteLst_Changed(object sender, RoutedEventArgs e)
        {
            PanelDeleteLst.Visibility = ChkDeleteLst.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnBuild_Click(object sender, RoutedEventArgs e)
        {
            BtnBuild.IsEnabled = false;
            try
            {
                SaveSettingsFromUI();
                string workspace = TbWorkspace.Text.Trim();
                string desc      = TbPatchDesc.Text.Trim();
                string sahKeyHex = TbSahKey.Text.Trim();
                string deleteLst = ChkDeleteLst.IsChecked == true ? TbDeleteLst.Text : null;

                if (string.IsNullOrWhiteSpace(desc))      { Log(L.ErrDescRequired);  return; }
                if (string.IsNullOrWhiteSpace(sahKeyHex)) { Log(L.ErrKeyRequired);   return; }

                int patchNum = await ResolvePatchNumber();
                Log(L.PatchNumber(patchNum));

                string patchPath = null;
                await RunAsync(() =>
                {
                    var builder = new PatchBuilder(Log);
                    patchPath = builder.Build(workspace, patchNum, desc, sahKeyHex, deleteLst);
                });

                if (patchPath != null)
                {
                    _lastPatchPath = patchPath;
                    TbLastPatch.Text = Path.GetFileName(patchPath) + "  (" + new FileInfo(patchPath).Length / 1024 + " KB)";
                    Log(L.PatchReady(Path.GetFileName(patchPath)));
                }
            }
            finally { BtnBuild.IsEnabled = true; }
        }

        private async void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastPatchPath) || !File.Exists(_lastPatchPath))
            {
                Log(L.ErrBuildFirst);
                return;
            }
            if (_ssh == null || !_ssh.IsConnected)
            {
                Log(L.ErrSshFirst);
                return;
            }

            BtnUpload.IsEnabled = false;
            await RunAsync(() => _ssh.UploadPatch(_lastPatchPath));
            BtnUpload.IsEnabled = true;
        }

        private async Task<int> ResolvePatchNumber()
        {
            string raw = TbPatchNum.Text.Trim();
            if (!raw.Equals("auto", StringComparison.OrdinalIgnoreCase) && int.TryParse(raw, out int manual))
                return manual;

            if (_ssh != null && _ssh.IsConnected)
            {
                int next = 1;
                await RunAsync(() => { next = _ssh.GetNextPatchNumber(); });
                Log(L.SshNextPatch(next));
                return next;
            }

            Log(L.SshAutoNotConnected);
            return 1;
        }

        // ── Log & helpers ─────────────────────────────────────────

        private void Log(string msg)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Log(msg)));
                return;
            }
            string ts = DateTime.Now.ToString("HH:mm:ss");
            TbLog.AppendText($"[{ts}] {msg}\n");
            TbLog.ScrollToEnd();
            App.WriteLog("UI", msg);
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e) => TbLog.Clear();

        private async Task RunAsync(Action action)
        {
            try
            {
                await Task.Run(action);
            }
            catch (Exception ex)
            {
                // Logga tutta la catena (inner exception inclusa)
                string full = ex.ToString();
                App.WriteLog("EXCEPTION", full);
                App.WriteLog("LANG-DEBUG", $"L.Current={L.Current} al momento dell'errore");
                // In UI: mostra messaggio + prima inner exception se presente
                string msg = ex.Message;
                if (ex.InnerException != null)
                    msg += " → " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null)
                    msg += " → " + ex.InnerException.InnerException.Message;
                Log(L.Err(msg));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            L.Changed -= OnLangChanged;
            _ssh?.Dispose();
            base.OnClosed(e);
        }
    }
}
