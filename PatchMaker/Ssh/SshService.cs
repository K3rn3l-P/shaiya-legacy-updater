using Renci.SshNet;
using System;
using System.IO;
using System.Text;

namespace PatchMaker
{
    public class SshService : IDisposable
    {
        private SftpClient _sftp;
        private readonly Action<string> _log;
        private readonly PatchMakerSettings _settings;

        public bool IsConnected => _sftp != null && _sftp.IsConnected;

        public SshService(PatchMakerSettings settings, Action<string> log)
        {
            _settings = settings;
            _log = log;
        }

        // Converte "C:/path" → "/C:/path" per OpenSSH Windows (SFTP root = /C:, /D:, ...)
        private static string NormalizeSftpPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('\\', '/');
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
                return "/" + path;
            return path;
        }

        public void Connect()
        {
            Disconnect();
            App.WriteLog("SSH", $"Connect() → host={_settings.SshHost} user={_settings.SshUser}");

            var auth = new PasswordAuthenticationMethod(_settings.SshUser, _settings.SshPassword);
            var connInfo = new ConnectionInfo(_settings.SshHost, _settings.SshUser, auth);
            App.WriteLog("SSH", "ConnectionInfo creato");

            _sftp = new SftpClient(connInfo);
            _sftp.ErrorOccurred += (s, ex) =>
            {
                string full = ex.Exception?.ToString() ?? "null";
                App.WriteLog("SSH-ERROR", full);
                Log(L.SshErrorOccurred(ex.Exception?.Message ?? "null"));
            };
            App.WriteLog("SSH", "SftpClient creato, chiamo Connect()...");
            _sftp.Connect();
            App.WriteLog("SSH", "Connect() completato OK");
            Log(L.SshConnectedFmt(_settings.SshHost, _settings.SshUser));
        }

        public void Disconnect()
        {
            try { _sftp?.Disconnect(); _sftp?.Dispose(); } catch { }
            _sftp = null;
        }

        public int GetNextPatchNumber()
        {
            EnsureConnected();
            int max = 0;
            try
            {
                string patchesPath = NormalizeSftpPath(_settings.SshPatchesPath);
                foreach (var f in _sftp.ListDirectory(patchesPath))
                {
                    string name = f.Name;
                    if (name.Length >= 4 && int.TryParse(name.Substring(0, 4), out int n))
                        if (n > max) max = n;
                }
            }
            catch (Exception ex) { Log(L.SshPatchesListWarn(ex.Message)); }
            return max + 1;
        }

        public void UploadPatch(string localPatchPath)
        {
            EnsureConnected();
            string fileName   = Path.GetFileName(localPatchPath);
            string remotePath = NormalizeSftpPath(_settings.SshPatchesPath).TrimEnd('/') + "/" + fileName;

            Log(L.SshUploadFmt(fileName, remotePath));
            using (FileStream fs = File.OpenRead(localPatchPath))
                _sftp.UploadFile(fs, remotePath, true);
            Log(L.SshUploadDone);
        }

        public string ReadTextFile(string remotePath)
        {
            if (!IsConnected) return null;
            remotePath = NormalizeSftpPath(remotePath);
            try
            {
                using (var s = _sftp.OpenRead(remotePath))
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                Log(L.ReadFileFailed(remotePath, ex.Message));
                return null;
            }
        }

        public void Dispose() => Disconnect();

        private void EnsureConnected()
        {
            if (!IsConnected) throw new InvalidOperationException(L.SshNotConnected);
        }

        private void Log(string msg) => _log?.Invoke(msg);
    }
}
