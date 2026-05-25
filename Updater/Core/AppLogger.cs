using System;
using System.Diagnostics;
using System.IO;

namespace Shaiya_Duff_Updater
{
    public static class AppLogger
    {
        private static readonly object Sync = new object();

        private enum LogLevel
        {
            Debug = 0,
            Info = 1,
            Warn = 2,
            Error = 3
        }

#if DEBUG
        private static readonly LogLevel MinLevel = LogLevel.Debug;
#else
        private static readonly LogLevel MinLevel = LogLevel.Error;
#endif

        public static void Info(string message) { Write("launcher", "INFO", LogLevel.Info, message, null); }
        public static void Warn(string message) { Write("launcher", "WARN", LogLevel.Warn, message, null); }
        public static void Error(string message, Exception ex = null) { Write("launcher", "ERROR", LogLevel.Error, message, ex); }

        [Conditional("DEBUG")]
        public static void Debug(string message)
        {
            Write("launcher", "DEBUG", LogLevel.Debug, message, null);
        }

        [Conditional("DEBUG")]
        public static void Debug(string category, string message)
        {
            string prefix = string.IsNullOrWhiteSpace(category) ? string.Empty : "[" + category + "] ";
            Write("launcher", "DEBUG", LogLevel.Debug, prefix + message, null);
        }

        public static void Login(string message) { Write("login", "INFO", LogLevel.Info, message, null); }
        public static void Patch(string message) { Write("patch", "INFO", LogLevel.Info, message, null); }
        public static void PatchWarn(string message) { Write("patch", "WARN", LogLevel.Warn, message, null); }
        public static void PatchError(string message, Exception ex = null) { Write("patch", "ERROR", LogLevel.Error, message, ex); }
        public static void SahCrypt(string message) { Write("sahcrypt", "INFO", LogLevel.Info, message, null); }
        public static void SahCryptError(string message, Exception ex = null) { Write("sahcrypt", "ERROR", LogLevel.Error, message, ex); }

        private static void Write(string category, string level, LogLevel logLevel, string message, Exception ex)
        {
            try
            {
                if (logLevel < MinLevel) return;

                if (string.IsNullOrWhiteSpace(category)) category = "launcher";
                foreach (char c in Path.GetInvalidFileNameChars()) category = category.Replace(c, '_');

                string dir = AppPaths.InBase("logs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, category + "_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

                string safeMessage = message;
                string safeException = string.Empty;

#if DEBUG
                safeException = ex == null ? string.Empty : ex.ToString() + Environment.NewLine;
#else
                // Release: keep logs minimal and generic for end-user environments.
                if (logLevel == LogLevel.Error)
                {
                    safeMessage = "Operation failed. See support documentation.";
                    if (ex != null)
                        safeException = "ExceptionType=" + ex.GetType().Name + Environment.NewLine;
                }
                else
                {
                    safeMessage = "Event.";
                }
#endif

                lock (Sync)
                {
                    File.AppendAllText(file,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + safeMessage + Environment.NewLine +
                        safeException);
                }
            }
            catch { }
        }
    }
}

