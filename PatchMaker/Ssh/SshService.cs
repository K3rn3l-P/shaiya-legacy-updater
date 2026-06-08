using Renci.SshNet;
using System;
using System.IO;
using System.Text;

namespace PatchMaker
{
    public class SshService : IDisposable
    {
        private SftpClient _sftp;
        private SshClient  _sshExec;
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

            _sshExec = new SshClient(connInfo);
            _sshExec.Connect();

            App.WriteLog("SSH", "Connect() completato OK");
            Log(L.SshConnectedFmt(_settings.SshHost, _settings.SshUser));
        }

        public void Disconnect()
        {
            try { _sftp?.Disconnect(); _sftp?.Dispose(); } catch { }
            _sftp = null;
            try { _sshExec?.Disconnect(); _sshExec?.Dispose(); } catch { }
            _sshExec = null;
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

        public void WriteTextFile(string remotePath, string content)
        {
            EnsureConnected();
            remotePath = NormalizeSftpPath(remotePath);
            byte[] bytes = Encoding.ASCII.GetBytes(content);
            using (var ms = new MemoryStream(bytes))
                _sftp.UploadFile(ms, remotePath, true);
        }

        public string RunCommand(string cmd)
        {
            if (_sshExec == null || !_sshExec.IsConnected)
                throw new InvalidOperationException(L.SshNotConnected);
            var result = _sshExec.RunCommand(cmd);
            return result.Result + result.Error;
        }

        public byte[] DownloadBytes(string remotePath)
        {
            if (!IsConnected) return null;
            remotePath = NormalizeSftpPath(remotePath);
            try
            {
                using (var ms = new MemoryStream())
                {
                    _sftp.DownloadFile(remotePath, ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Log(L.ReadFileFailed(remotePath, ex.Message));
                return null;
            }
        }

        public void UploadBytes(string remotePath, byte[] data)
        {
            EnsureConnected();
            remotePath = NormalizeSftpPath(remotePath);
            using (var ms = new MemoryStream(data))
                _sftp.UploadFile(ms, remotePath, true);
        }

        public void UpdateSahToken(byte[] patchPlainSah, string iniRemotePath, string psLoginServiceName)
        {
            string masterSahPath     = NormalizeSftpPath(_settings.MasterSahPath);
            string masterSafSizePath = NormalizeSftpPath(_settings.MasterSafSizePath);

            // Download master SAH + SAF size
            byte[] masterBin = DownloadBytes(masterSahPath);
            if (masterBin == null)
                throw new InvalidOperationException("Cannot download data_master.sah: " + masterSahPath);
            string safSizeText = ReadTextFile(masterSafSizePath);
            if (safSizeText == null)
                throw new InvalidOperationException("Cannot read data_master.saf_size: " + masterSafSizePath);
            ulong safSize = ulong.Parse(safSizeText.Trim());

            // Parse + merge
            SahFolder masterTree = SahMerger.Parse(masterBin);
            SahFolder patchTree  = SahMerger.Parse(patchPlainSah);
            SahMerger.Merge(masterTree, patchTree, ref safSize);

            // Serialize + upload new master
            byte[] newMasterBin = SahMerger.Serialize(masterTree);
            UploadBytes(masterSahPath, newMasterBin);
            WriteTextFile(masterSafSizePath, safSize.ToString());

            // Calculate correct token = SHA256(merged_master)[0:8]
            string token;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(newMasterBin);
                token = BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
            }
            Log(L.SahTokenUpdating(token));

            // Update GMSecurity.ini
            string current = ReadTextFile(iniRemotePath);
            if (current == null)
                throw new InvalidOperationException("Cannot read GMSecurity.ini: " + iniRemotePath);
            var lines = current.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var updated = new System.Collections.Generic.List<string>();
            bool found = false;
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith("SahTokenHex=", StringComparison.OrdinalIgnoreCase))
                { updated.Add("SahTokenHex=" + token); found = true; }
                else
                { updated.Add(line.TrimEnd('\r')); }
            }
            if (!found) updated.Add("SahTokenHex=" + token);
            WriteTextFile(iniRemotePath, string.Join("\r\n", updated) + "\r\n");

            RunCommand("sc.exe stop " + psLoginServiceName);
            System.Threading.Thread.Sleep(3000);
            RunCommand("sc.exe start " + psLoginServiceName);

            Log(L.SahTokenUpdated(token));
        }

        public void Dispose() => Disconnect();

        private void EnsureConnected()
        {
            if (!IsConnected) throw new InvalidOperationException(L.SshNotConnected);
        }

        private void Log(string msg) => _log?.Invoke(msg);
    }
}
