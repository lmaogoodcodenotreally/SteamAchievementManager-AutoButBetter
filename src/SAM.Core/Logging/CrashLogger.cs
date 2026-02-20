using System;
using System.IO;

namespace SAM.Core.Logging;

public static class CrashLogger
{
    public static void Log(Exception ex, string source = "Unknown")
    {
        try
        {
            // Try primary upload/logs path first
            var baseDir = AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            var primaryDir = Path.Combine(baseDir, "upload", "logs");
            var wrote = TryWriteCrashLog(primaryDir, ex, source);

            if (!wrote)
            {
                // Fallback to LocalAppData\SAM\logs
                try
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var fallbackDir = Path.Combine(localAppData, "SAM", "logs");
                    wrote = TryWriteCrashLog(fallbackDir, ex, source);
                }
                catch
                {
                    // ignore
                }
            }

            if (!wrote)
            {
                // Final fallback to temp directory
                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "SAM_logs");
                    TryWriteCrashLog(tempDir, ex, source);
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            // best-effort only
        }
    }

    private static bool TryWriteCrashLog(string uploadDir, Exception ex, string source)
    {
        try
        {
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            var crashFile = Path.Combine(uploadDir, "crash.log");
            using var fs = new FileStream(crashFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var sw = new StreamWriter(fs);
            sw.WriteLine("----");
            sw.WriteLine($"Timestamp: {DateTime.UtcNow:O}");
            sw.WriteLine($"Source: {source}");
            sw.WriteLine($"Process: {Environment.ProcessPath} (PID: {Environment.ProcessId})");
            sw.WriteLine($"User: {Environment.UserName}");
            sw.WriteLine($"Message: {ex.Message}");
            sw.WriteLine($"Type: {ex.GetType().FullName}");
            sw.WriteLine($"StackTrace:\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                sw.WriteLine("InnerException:");
                sw.WriteLine($"Type: {ex.InnerException.GetType().FullName}");
                sw.WriteLine($"Message: {ex.InnerException.Message}");
                sw.WriteLine($"StackTrace:\n{ex.InnerException.StackTrace}");
            }
            sw.WriteLine();
            sw.Flush();
            fs.Flush(true);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
