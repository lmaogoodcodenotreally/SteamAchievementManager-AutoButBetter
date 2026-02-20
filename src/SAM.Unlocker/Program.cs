using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using SAM.API;
using SAM.Core;

namespace SAM.Unlocker;

internal static class Program
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        private static async Task<int> Main(string[] args)
        {
            var parsedAppId = "unknown";
            try
            {
                // The helper can be invoked in two ways:
                // - Passed a token like "testapp_{appid}.exe" as the first argument
                // - Launched directly as testapp_{appid}.exe (no args)
                string token = null;
                if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                {
                    token = args[0];
                }
                else
                {
                    // fallback to our own exe path
                    token = Environment.GetCommandLineArgs().Length > 0 ? Environment.GetCommandLineArgs()[0] : null;
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.WriteLine("Unable to determine helper token for app id extraction.");
                    return 1;
                }

                var fileName = Path.GetFileNameWithoutExtension(token);
                var m = System.Text.RegularExpressions.Regex.Match(fileName ?? string.Empty, @"testapp_(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success || !uint.TryParse(m.Groups[1].Value, out var appId) || appId == 0)
                {
                    Console.WriteLine("Invalid or missing app id in helper filename/arg.");
                    return 1;
                }
                parsedAppId = m.Groups[1].Value;

            using (var client = new API.Client())
            {
                client.Initialize(appId);

                var steamPath = API.Steam.GetInstallPath();
                var path = Path.Combine(steamPath, "appcache", "stats", $"UserGameStatsSchema_{appId}.bin");
                if (!File.Exists(path))
                {
                    log.Warn($"Stats schema not found for game {appId}");
                    return 2;
                }

                var kv = API.Types.KeyValue.LoadAsBinary(path);
                if (kv == null)
                {
                    log.Warn($"Failed to load stats schema for game {appId}");
                    return 3;
                }

                var stats = kv[appId.ToString()]["stats"];
                if (!stats.Valid || stats.Children == null)
                {
                    log.Warn($"Invalid stats in schema for game {appId}");
                    return 4;
                }

                var achievementIds = stats.Children
                    .Where(s => s.Valid)
                    .SelectMany(s => s.Children ?? Enumerable.Empty<API.Types.KeyValue>())
                    .SelectMany(child => (child.Name.ToLowerInvariant() == "bits") ? (child.Children ?? Enumerable.Empty<API.Types.KeyValue>()) : Enumerable.Empty<API.Types.KeyValue>())
                    .Select(bit => bit["name"].AsString(string.Empty))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                if (!achievementIds.Any())
                {
                    return 0;
                }

                client.SteamUserStats.RequestCurrentStats();
                await Task.Delay(500);

                foreach (var id in achievementIds)
                {
                    client.SteamUserStats.SetAchievement(id, true);
                }

                client.SteamUserStats.StoreStats();
            }
            return 0;
        }
        catch (Exception e)
        {
            try
            {
                log.Error("Unlocker failed", e);

                // Try writing a per-app error log in the current directory to aid diagnostics
                var exePath = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                var fileNameErr = Path.Combine(exePath, $"testapp_{parsedAppId}_error.log");
                try
                {
                    File.AppendAllText(fileNameErr, DateTime.UtcNow.ToString("o") + " - Unhandled exception:\n" + e.ToString() + "\n\n");
                }
                catch { }
            }
            catch { }

            return 99;
        }
    }
}
