using Ionic.Zip;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        // ── Custom chrome ─────────────────────────────────────────

        private void DragWindow(object sender, MouseButtonEventArgs e) { try { DragMove(); } catch { } }

        private void BtnMin_Click(object sender, MouseButtonEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, MouseButtonEventArgs e) => Close();

        private static readonly SolidColorBrush _chromHover     = new SolidColorBrush(Color.FromArgb(0x22, 0xE8, 0xC8, 0x81));
        private static readonly SolidColorBrush _chromCloseHover = new SolidColorBrush(Color.FromArgb(0x30, 0xF4, 0x6A, 0x6A));
        private static readonly SolidColorBrush _chromTransp    = new SolidColorBrush(Colors.Transparent);

        private void BtnChromeBtn_MouseEnter(object sender, MouseEventArgs e)      => ((System.Windows.Controls.Border)sender).Background = _chromHover;
        private void BtnChromeBtn_MouseLeave(object sender, MouseEventArgs e)      => ((System.Windows.Controls.Border)sender).Background = _chromTransp;
        private void BtnCloseChromeBtn_MouseEnter(object sender, MouseEventArgs e) => ((System.Windows.Controls.Border)sender).Background = _chromCloseHover;

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
            TbSshHost.Text     = _settings.SshHost;
            TbSshUser.Text     = _settings.SshUser;
            PbSshPass.Password = _settings.SshPassword;
            TbWorkspace.Text   = _settings.ResolvedWorkspace;
        }

        private void SaveSettingsFromUI()
        {
            _settings.SshHost       = TbSshHost.Text.Trim();
            _settings.SshUser       = TbSshUser.Text.Trim();
            _settings.SshPassword   = PbSshPass.Password;
            _settings.WorkspacePath = TbWorkspace.Text.Trim();
            _settings.Save();
        }

        private void BtnSettings_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SaveSettingsFromUI();
            var win = new SettingsWindow(_settings, _ssh) { Owner = this };
            if (win.ShowDialog() == true)
                Log(L.SettingsSaved);
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
                bool   encryptSah  = _settings.EncryptSah && _settings.EncryptPatch;
                string patchKeyHex = encryptSah ? _settings.PatchKeyHex : null;
                string deleteLst   = ChkDeleteLst.IsChecked == true ? TbDeleteLst.Text : null;

                if (string.IsNullOrWhiteSpace(desc)) { Log(L.ErrDescRequired); return; }
                if (encryptSah && string.IsNullOrWhiteSpace(patchKeyHex)) { Log(L.ErrKeyRequired); return; }

                int patchNum = await ResolvePatchNumber();
                Log(L.PatchNumber(patchNum));

                string patchPath = null;
                await RunAsync(() =>
                {
                    var builder = new PatchBuilder(Log);
                    patchPath = builder.Build(workspace, patchNum, desc, patchKeyHex, deleteLst, encryptSah);
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
            await RunAsync(() => { _ssh.UploadPatch(_lastPatchPath); });
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

        // ── Verifica Patch ────────────────────────────────────────

        private async void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            string patchPath = _lastPatchPath;
            if (string.IsNullOrWhiteSpace(patchPath) || !File.Exists(patchPath))
            {
                var dlg = new OpenFileDialog { Filter = "Patch files|*.patch|ZIP files|*.zip|All files|*.*" };
                if (dlg.ShowDialog() != true) return;
                patchPath = dlg.FileName;
            }

            string patchKeyHex = _settings.PatchKeyHex;

            BtnVerify.IsEnabled = false;
            await RunAsync(() => VerifyPatch(patchPath, patchKeyHex));
            BtnVerify.IsEnabled = true;
        }

        private void VerifyPatch(string patchPath, string sahKeyHex)
        {
            Log(L.VerifyStart(Path.GetFileName(patchPath)));

            byte[] key = EsahEncryptor.ParseHexKey(sahKeyHex);

            byte[] esahBytes = null;
            using (var zip = ZipFile.Read(patchPath))
            {
                ZipEntry sahEntry = null;
                foreach (ZipEntry ze in zip.Entries)
                {
                    if (ze.FileName.EndsWith("update.sah", StringComparison.OrdinalIgnoreCase))
                    { sahEntry = ze; break; }
                }
                if (sahEntry == null) { Log(L.VerifyNoSah); return; }

                using (var ms = new MemoryStream())
                {
                    sahEntry.Extract(ms);
                    esahBytes = ms.ToArray();
                }
            }

            byte[] sahBytes;
            if (EsahEncryptor.IsEsah(esahBytes))
            {
                Log(L.VerifyDecrypting);
                sahBytes = EsahDecryptor.Decrypt(esahBytes, key);
            }
            else if (EsahEncryptor.IsPlainSah(esahBytes))
            {
                sahBytes = esahBytes;
            }
            else
            {
                Log(L.VerifyBadMagic);
                return;
            }

            if (!EsahEncryptor.IsPlainSah(sahBytes)) { Log(L.VerifyDecryptFailed); return; }

            List<SahEntry> entries = SahParser.Parse(sahBytes);
            Log(L.VerifyOk(entries.Count));
            foreach (SahEntry entry in entries)
            {
                string sizeStr = entry.Size >= 1024
                    ? $"{entry.Size / 1024} KB"
                    : $"{entry.Size} B";
                Log($"  {entry.Path}  [{sizeStr}]");
            }
        }

        private async void BtnExtract_Click(object sender, RoutedEventArgs e)
        {
            string patchPath = _lastPatchPath;
            if (string.IsNullOrWhiteSpace(patchPath) || !File.Exists(patchPath))
            {
                var dlg = new OpenFileDialog { Filter = "Patch files|*.patch|ZIP files|*.zip|All files|*.*" };
                if (dlg.ShowDialog() != true) return;
                patchPath = dlg.FileName;
            }

            string patchKeyHex = _settings.PatchKeyHex;

            BtnExtract.IsEnabled = false;
            await RunAsync(() => ExtractPatch(patchPath, patchKeyHex));
            BtnExtract.IsEnabled = true;
        }

        private void ExtractPatch(string patchPath, string sahKeyHex)
        {
            string outDir = Path.Combine(
                Path.GetDirectoryName(patchPath),
                Path.GetFileNameWithoutExtension(patchPath));

            Log(L.ExtractStart(Path.GetFileName(patchPath), outDir));
            Directory.CreateDirectory(outDir);

            using (var zip = ZipFile.Read(patchPath))
            {
                foreach (ZipEntry ze in zip.Entries)
                    ze.Extract(outDir, ExtractExistingFileAction.OverwriteSilently);
            }

            string sahFile = Path.Combine(outDir, "update.sah");
            if (!File.Exists(sahFile)) { Log(L.ExtractNoSah); return; }

            byte[] data = File.ReadAllBytes(sahFile);
            if (EsahEncryptor.IsEsah(data))
            {
                Log(L.ExtractDecrypting);
                byte[] key   = EsahEncryptor.ParseHexKey(sahKeyHex);
                byte[] plain = EsahDecryptor.Decrypt(data, key);
                File.WriteAllBytes(sahFile, plain);
                Log(L.ExtractDecryptOk(plain.Length / 1024));
            }
            else if (EsahEncryptor.IsPlainSah(data))
            {
                Log(L.ExtractAlreadyPlain);
            }
            else
            {
                Log(L.VerifyBadMagic);
                return;
            }

            Log(L.ExtractDone(outDir));
        }

        // ── SAH Crypt ─────────────────────────────────────────────

        private void BtnSahBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "SAH Files|*.sah|All files|*.*" };
            if (dlg.ShowDialog() == true)
                TbSahFile.Text = dlg.FileName;
        }

        private void TbSahFile_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void TbSahFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                    TbSahFile.Text = files[0];
            }
        }

        // update.sah (dentro patch) usa PatchKeyHex; data.sah usa SahKeyHex.
        private string ResolveSahCryptKey(string filePath)
        {
            bool isUpdateSah = Path.GetFileName(filePath).Equals("update.sah", StringComparison.OrdinalIgnoreCase);
            return isUpdateSah ? _settings.PatchKeyHex : _settings.SahKeyHex;
        }

        private async void BtnEncryptSah_Click(object sender, RoutedEventArgs e)
        {
            string path = TbSahFile.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            { SetSahStatus(L.ErrSelectSahFirst, true); return; }

            string keyHex = ResolveSahCryptKey(path);
            if (string.IsNullOrWhiteSpace(keyHex))
            { SetSahStatus(L.ErrKeyRequiredSah, true); return; }

            BtnEncryptSah.IsEnabled = BtnDecryptSah.IsEnabled = false;
            await RunAsync(() => EncryptSahFile(path, keyHex));
            BtnEncryptSah.IsEnabled = BtnDecryptSah.IsEnabled = true;
        }

        private async void BtnDecryptSah_Click(object sender, RoutedEventArgs e)
        {
            string path = TbSahFile.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            { SetSahStatus(L.ErrSelectSahFirst, true); return; }

            string keyHex = ResolveSahCryptKey(path);
            if (string.IsNullOrWhiteSpace(keyHex))
            { SetSahStatus(L.ErrKeyRequiredSah, true); return; }

            BtnEncryptSah.IsEnabled = BtnDecryptSah.IsEnabled = false;
            await RunAsync(() => DecryptSahFile(path, keyHex));
            BtnEncryptSah.IsEnabled = BtnDecryptSah.IsEnabled = true;
        }

        private void EncryptSahFile(string path, string keyHex)
        {
            byte[] data = File.ReadAllBytes(path);
            if (!EsahEncryptor.IsPlainSah(data))
            {
                SetSahStatus(EsahEncryptor.IsEsah(data) ? L.SahAlreadyEncrypted : L.SahBadMagic, true);
                return;
            }

            byte[] key       = EsahEncryptor.ParseHexKey(keyHex);
            byte[] encrypted = EsahEncryptor.Encrypt(data, key);
            string backup    = BuildSahBackupPath(path, isEncrypting: true);
            File.WriteAllBytes(backup, data);
            File.WriteAllBytes(path, encrypted);

            string msg = L.SahEncryptOk(Path.GetFileName(path), Path.GetFileName(backup), encrypted.LongLength / 1024);
            Log(msg);
            SetSahStatus(msg, false);
        }

        private void DecryptSahFile(string path, string keyHex)
        {
            byte[] data = File.ReadAllBytes(path);
            if (!EsahEncryptor.IsEsah(data))
            {
                SetSahStatus(EsahEncryptor.IsPlainSah(data) ? L.SahAlreadyPlain : L.SahBadMagic, true);
                return;
            }

            byte[] key   = EsahEncryptor.ParseHexKey(keyHex);
            byte[] plain = EsahDecryptor.Decrypt(data, key);
            if (!EsahEncryptor.IsPlainSah(plain))
            { SetSahStatus(L.SahDecryptFailed, true); return; }

            string backup = BuildSahBackupPath(path, isEncrypting: false);
            File.WriteAllBytes(backup, data);
            File.WriteAllBytes(path, plain);

            string msg = L.SahDecryptOk(Path.GetFileName(path), Path.GetFileName(backup), plain.LongLength / 1024);
            Log(msg);
            SetSahStatus(msg, false);
        }

        private static string BuildSahBackupPath(string path, bool isEncrypting)
        {
            string dir    = Path.GetDirectoryName(path) ?? ".";
            string stem   = Path.GetFileNameWithoutExtension(path);
            string ext    = Path.GetExtension(path);
            string suffix = isEncrypting ? ".dec" : ".enc";
            return Path.Combine(dir, stem + suffix + ext);
        }

        private void SetSahStatus(string msg, bool isError)
        {
            if (!Dispatcher.CheckAccess())
            { Dispatcher.BeginInvoke(new Action(() => SetSahStatus(msg, isError))); return; }
            TbSahCryptStatus.Text       = msg;
            TbSahCryptStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(0xF4, 0x6A, 0x6A))
                : new SolidColorBrush(Color.FromRgb(0x4D, 0xC8, 0x70));
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
