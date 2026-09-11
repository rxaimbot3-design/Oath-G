using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AshtronV7.Services
{
    public class LoggerService
    {
        private static readonly Lazy<LoggerService> _instance = new(() => new LoggerService());
        public static LoggerService Instance => _instance.Value;

        private readonly string _logPath;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly object _lock = new();
        private bool _isWriting;

        private LoggerService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = Path.Combine(appData, "AshtronV7", "Logs");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, $"AshtronV7_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            
            Log($"Log file: {_logPath}");
        }

        public void Log(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            _logQueue.Enqueue(entry);
            FlushAsync();
            System.Diagnostics.Debug.WriteLine(entry);
        }

        public void LogError(string message, Exception ex = null)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}";
            if (ex != null)
            {
                entry += $"\n  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}";
            }
            _logQueue.Enqueue(entry);
            FlushAsync();
            System.Diagnostics.Debug.WriteLine(entry);
        }

        public void LogWarning(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss.fff}] WARNING: {message}";
            _logQueue.Enqueue(entry);
            FlushAsync();
            System.Diagnostics.Debug.WriteLine(entry);
        }

        private async void FlushAsync()
        {
            if (_isWriting) return;
            _isWriting = true;

            try
            {
                var entries = new StringBuilder();
                while (_logQueue.TryDequeue(out var entry))
                {
                    entries.AppendLine(entry);
                }

                if (entries.Length > 0)
                {
                    await File.AppendAllTextAsync(_logPath, entries.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logger error: {ex.Message}");
            }
            finally
            {
                _isWriting = false;
            }
        }

        public string GetLogPath() => _logPath;
    }
}