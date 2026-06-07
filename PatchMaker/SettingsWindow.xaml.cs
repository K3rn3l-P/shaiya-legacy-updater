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
            ChkEncrypt.IsChecked      = _settings.EncryptSah;
            ChkEncryptPatch.IsChecked = _settings.EncryptPatch;
            TbSahKey.Text             = _settings.SahKeyHex;
            TbPatchKey.Text           = _settings.PatchKeyHex;
            TbSshPatchesPath.Text     = _settings.SshPatchesPath;
            TbSshHtdocsPath.Text      = _settings.SshHtdocsPath;
            TbSahKey.IsEnabled        = _settings.EncryptSah;
            BtnReadKey.IsEnabled      = _settings.EncryptSah;
            TbPatchKey.IsEnabled      = _settings.EncryptPatch;
            BtnReadPatchKey.IsEnabled = _settings.EncryptPatch;
        }

        private void ChkEncrypt_Changed(object sender, RoutedEventArgs e)
        {
            bool on = ChkEncrypt.IsChecked == true;
            TbSahKey.IsEnabled   = on;
            BtnReadKey.IsEnabled = on;
        }

        private void ChkEncryptPatch_Changed(object sender, RoutedEventArgs e)
        {
            bool on = ChkEncryptPatch.IsChecked == true;
            TbPatchKey.IsEnabled      = on;
            BtnReadPatchKey.IsEnabled = on;
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
                    var (sah, patch) = PatchMakerSettings.ParseKeysFromIniContent(content);
                    if (sah != null) TbSahKey.Text = sah;
                    if (patch != null) TbPatchKey.Text = patch;
                    if (sah != null || patch != null)
                    {
                        SetStatus(L.KeyReadFromServer(sah?.Length ?? 0, patch?.Length ?? 0));
                        return;
                    }
                    SetStatus(L.ErrKeyNotFoundServer);
                    return;
                }
                SetStatus(L.SshReadFailed);
            }

            var dlg = new OpenFileDialog { Filter = "INI files|*.ini|All files|*.*" };
            if (dlg.ShowDialog() != true) return;
            string fileContent = File.ReadAllText(dlg.FileName);
            var (sahLocal, patchLocal) = PatchMakerSettings.ParseKeysFromIniContent(fileContent);
            if (sahLocal == null && patchLocal == null) { SetStatus(L.ErrKeyNotFoundLocal(dlg.FileName)); return; }
            if (sahLocal != null) TbSahKey.Text = sahLocal;
            if (patchLocal != null) TbPatchKey.Text = patchLocal;
            SetStatus(L.KeyReadFromFile(Path.GetFileName(dlg.FileName), (sahLocal ?? patchLocal).Length));
        }

        private void BtnReadPatchKey_Click(object sender, RoutedEventArgs e)
        {
            if (_ssh != null && _ssh.IsConnected)
            {
                string remotePath = PatchMakerSettings.DefaultGmSecurityServerPath;
                SetStatus(L.ReadingGmSec(remotePath));
                string content = null;
                try { content = _ssh.ReadTextFile(remotePath); } catch (Exception ex) { SetStatus(L.Err(ex.Message)); return; }
                if (content != null)
                {
                    string patch = PatchMakerSettings.ParseKeysFromIniContent(content).patchKey;
                    if (patch != null) { TbPatchKey.Text = patch; SetStatus(L.KeyReadFromServer(patch.Length)); return; }
                    SetStatus(L.ErrKeyNotFoundServer);
                    return;
                }
                SetStatus(L.SshReadFailed);
            }

            var dlg = new OpenFileDialog { Filter = "INI files|*.ini|All files|*.*" };
            if (dlg.ShowDialog() != true) return;
            string patchKey = PatchMakerSettings.ParseKeysFromIniContent(File.ReadAllText(dlg.FileName)).patchKey;
            if (patchKey == null) { SetStatus(L.ErrKeyNotFoundLocal(dlg.FileName)); return; }
            TbPatchKey.Text = patchKey;
            SetStatus(L.KeyReadFromFile(Path.GetFileName(dlg.FileName), patchKey.Length));
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.EncryptSah     = ChkEncrypt.IsChecked == true;
            _settings.EncryptPatch   = ChkEncryptPatch.IsChecked == true;
            _settings.SahKeyHex      = TbSahKey.Text.Trim();
            _settings.PatchKeyHex    = TbPatchKey.Text.Trim();
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
