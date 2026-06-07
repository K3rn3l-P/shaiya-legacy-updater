using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PatchMaker
{
    public partial class SettingsWindow : Window
    {
        private readonly PatchMakerSettings _settings;
        private readonly SshService _ssh;

        public SettingsWindow(PatchMakerSettings settings, SshService ssh)
        {
            _settings = settings;
            _ssh = ssh;
            InitializeComponent();
            LoadSettingsToUI();
            L.Changed += OnLangChanged;
        }

        private void OnLangChanged() { /* DynamicResource bindings update automatically */ }

        // ── Custom chrome ─────────────────────────────────────────
        private void DragWindow(object sender, MouseButtonEventArgs e) { try { DragMove(); } catch { } }
        private void BtnClose_Click(object sender, MouseButtonEventArgs e) => DialogResult = false;

        private static readonly SolidColorBrush _chromHover      = new SolidColorBrush(Color.FromArgb(0x22, 0xE8, 0xC8, 0x81));
        private static readonly SolidColorBrush _chromCloseHover  = new SolidColorBrush(Color.FromArgb(0x30, 0xF4, 0x6A, 0x6A));
        private static readonly SolidColorBrush _chromTransp      = new SolidColorBrush(Colors.Transparent);

        private void BtnChromeBtn_MouseEnter(object sender, MouseEventArgs e)      => ((System.Windows.Controls.Border)sender).Background = _chromHover;
        private void BtnChromeBtn_MouseLeave(object sender, MouseEventArgs e)      => ((System.Windows.Controls.Border)sender).Background = _chromTransp;
        private void BtnCloseChromeBtn_MouseEnter(object sender, MouseEventArgs e) => ((System.Windows.Controls.Border)sender).Background = _chromCloseHover;

        private void LoadSettingsToUI()
        {
            ChkEncrypt.IsChecked     = _settings.EncryptSah;
            TbSahKey.Text            = _settings.SahKeyHex;
            TbSshPatchesPath.Text    = _settings.SshPatchesPath;
            TbSshHtdocsPath.Text     = _settings.SshHtdocsPath;
            TbSahKey.IsEnabled       = _settings.EncryptSah;
            BtnReadKey.IsEnabled     = _settings.EncryptSah;
        }

        private void ChkEncrypt_Changed(object sender, RoutedEventArgs e)
        {
            bool on = ChkEncrypt.IsChecked == true;
            TbSahKey.IsEnabled   = on;
            BtnReadKey.IsEnabled = on;
        }

        private void BtnReadKey_Click(object sender, RoutedEventArgs e)
        {
            if (_ssh != null && _ssh.IsConnected)
            {
                string remotePath = PatchMakerSettings.DefaultGmSecurityServerPath;
                SetStatus(L.ReadingGmSec(remotePath));
                string content = null;
                try { content = _ssh.ReadTextFile(remotePath); } catch (Exception ex) { SetStatus(L.Err(ex.Message)); return; }
                if (content != null)
                {
                    string k = PatchMakerSettings.ParseKeyFromIniContent(content);
                    if (k != null) { TbSahKey.Text = k; SetStatus(L.KeyReadFromServer(k.Length)); return; }
                    SetStatus(L.ErrKeyNotFoundServer);
                    return;
                }
                SetStatus(L.SshReadFailed);
            }

            var dlg = new OpenFileDialog { Filter = "INI files|*.ini|All files|*.*" };
            if (dlg.ShowDialog() != true) return;
            string key = PatchMakerSettings.ReadKeyFromGmSecurityIni(dlg.FileName);
            if (key == null) { SetStatus(L.ErrKeyNotFoundLocal(dlg.FileName)); return; }
            TbSahKey.Text = key;
            SetStatus(L.KeyReadFromFile(Path.GetFileName(dlg.FileName), key.Length));
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.EncryptSah     = ChkEncrypt.IsChecked == true;
            _settings.SahKeyHex      = TbSahKey.Text.Trim();
            _settings.SshPatchesPath = TbSshPatchesPath.Text.Trim();
            _settings.SshHtdocsPath  = TbSshHtdocsPath.Text.Trim();
            _settings.Save();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void SetStatus(string msg)
        {
            TbStatus.Text = msg;
        }

        protected override void OnClosed(EventArgs e)
        {
            L.Changed -= OnLangChanged;
            base.OnClosed(e);
        }
    }
}
