using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;

namespace Shaiya_Duff_Updater
{
    public static class GameTcpPortTransitionWatcher
    {
        public static bool WaitForLoginToGameTransition(int processId, int timeoutMs)
        {
            int waitMs = Math.Max(1000, timeoutMs);
            int pollMs = 200;
            int elapsed = 0;
            bool sawLoginPort = false;

            AppLogger.Debug("tcp", "TCP wait start. pid=" + processId + " timeoutMs=" + waitMs + " loginPort=" + AppConfig.LoginTcpPort + " gamePort=" + AppConfig.GameTcpPort);

            while (elapsed <= waitMs)
            {
                if (!IsProcessAlive(processId))
                {
                    AppLogger.Debug("tcp", "TCP wait stop: process is not alive. elapsedMs=" + elapsed);
                    return false;
                }

                TcpConnectionInformation[] connections;
                try
                {
                    connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("TCP connection scan failed: " + ex.Message);
                    return false;
                }

                bool loginConnected = false;
                bool gameConnected = false;
                for (int i = 0; i < connections.Length; i++)
                {
                    TcpConnectionInformation c = connections[i];
                    if (c.State != TcpState.Established) continue;

                    int remotePort = c.RemoteEndPoint.Port;
                    if (remotePort == AppConfig.LoginTcpPort) loginConnected = true;
                    if (remotePort == AppConfig.GameTcpPort) gameConnected = true;
                }

                if (loginConnected && !sawLoginPort)
                {
                    sawLoginPort = true;
                    AppLogger.Info("TCP login connection detected on port " + AppConfig.LoginTcpPort + ". Waiting transition to " + AppConfig.GameTcpPort + ".");
                }

                if (gameConnected)
                {
                    AppLogger.Info("TCP game connection detected on port " + AppConfig.GameTcpPort + ". Waiting " + AppConfig.TcpPortTransitionDelayMs + "ms before reveal.");
                    Thread.Sleep(Math.Max(0, AppConfig.TcpPortTransitionDelayMs));
                    AppLogger.Debug("tcp", "TCP wait success. elapsedMs=" + elapsed + " loginSeen=" + (sawLoginPort ? "1" : "0"));
                    return true;
                }

                Thread.Sleep(pollMs);
                elapsed += pollMs;
            }

            AppLogger.Warn("TCP login->game port transition not detected within timeout. TimeoutMs=" + waitMs + " LoginSeen=" + (sawLoginPort ? "1" : "0"));
            AppLogger.Debug("tcp", "TCP wait timeout. elapsedMs=" + elapsed + " loginSeen=" + (sawLoginPort ? "1" : "0"));
            return false;
        }

        public static bool WaitForLoginToGameTransition(int processId)
        {
            return WaitForLoginToGameTransition(processId, AppConfig.TcpPortTransitionTimeoutMs);
        }

        private static bool IsProcessAlive(int processId)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }
}

