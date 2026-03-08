using System;
using System.IO;

namespace DChemist.Utils
{
    /// <summary>
    /// Central, static logger. Writes to both Debug output and a daily rolling log file.
    /// All repositories, ViewModels, and App startup should use this.
    /// </summary>
    public static class AppLogger
    {
        private static readonly string _logDir;
        private static readonly object _lock = new();

        static AppLogger()
        {
            _logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "D. Chemist", "logs");

            try { Directory.CreateDirectory(_logDir); }
            catch { /* If we can't create the log dir, we still continue */ }
        }

        private static string LogFilePath =>
            Path.Combine(_logDir, $"app_{DateTime.Now:yyyyMMdd}.log");

        public static void LogInfo(string message)    => Write("INFO ", message, null);
        public static void LogWarning(string message) => Write("WARN ", message, null);
        public static void LogError(string message, Exception? ex = null) => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] [{level}] {message}";
            if (ex != null)
                line += $"\n          Exception: {ex.GetType().Name}: {ex.Message}" +
                        $"\n          StackTrace: {ex.StackTrace}";

            System.Diagnostics.Debug.WriteLine(line);

            try
            {
                lock (_lock)
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch { /* Never throw from the logger itself */ }
        }
    }
}
