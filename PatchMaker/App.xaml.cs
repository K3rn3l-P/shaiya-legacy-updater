using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PatchMaker
{
    public partial class App : Application
    {
        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patchmaker.log");

        public static void WriteLog(string category, string text)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {text}{Environment.NewLine}";
                File.AppendAllText(LogPath, line, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            WriteLog("STARTUP", "PatchMaker avviato. Log: " + LogPath);

            DispatcherUnhandledException += (s, ex) =>
            {
                string full = ex.Exception.ToString();
                WriteLog("CRASH-UI", full);
                MessageBox.Show("Errore UI (vedi patchmaker.log):\n\n" + ex.Exception.Message,
                    "PatchMaker — Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                string full = ex.ExceptionObject is Exception exc ? exc.ToString() : ex.ExceptionObject?.ToString() ?? "?";
                WriteLog("CRASH-BG", full);
                try
                {
                    MessageBox.Show("Crash background (vedi patchmaker.log):\n\n" +
                        (ex.ExceptionObject is Exception e2 ? e2.Message : full),
                        "PatchMaker — Crash", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            };
        }
    }
}
