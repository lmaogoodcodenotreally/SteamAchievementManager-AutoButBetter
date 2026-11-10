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
    private const int STATS_TIMEOUT_MS = 15000;

    // per-phase timings, printed as one "TIMING k=ms ..." stdout line that SAM records per appid
    private static readonly System.Diagnostics.Stopwatch phaseWatch = new();
    private static readonly System.Text.StringBuilder timing = new();

    private static void Mark(string phase)
    {
        timing.Append($" {phase}={phaseWatch.ElapsedMilliseconds}");
        phaseWatch.Restart();
    }

        private static async Task<int> Main(string[] args)
        {
            timing.Append($" startup={(long) (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds}");
            phaseWatch.Start();

            var parsedAppId = "unknown";
            try
            {
                // The helper can be invoked in two ways:
                // - Passed a token like "testapp_{appid}.exe" as the first argument
                // - Launched directly as testapp_{appid}.exe (no args)
                // --relock sets every achievement back to locked instead of unlocked
                var relock = args?.Contains("--relock") == true;
                // --prefetch only makes Steam download/cache the stats schema, nothing is changed
                var prefetch = args?.Contains("--prefetch") == true;
                string token = args?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a) && !a.StartsWith("--"));
                if (token == null)
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
                Mark("init");

                var steamPath = API.Steam.GetInstallPath();
                var path = Path.Combine(steamPath, "appcache", "stats", $"UserGameStatsSchema_{appId}.bin");
                timing.Append($" schema_on_disk={(File.Exists(path) ? 1 : 0)}");

                // Steam downloads a missing schema while serving this request, so wait for its answer instead of a blind delay
                int? statsResult = null;
                var received = client.CreateAndRegisterCallback<API.UserStatsReceived>();
                received.OnRun += r => { if ((uint) (r.GameId & 0xFFFFFF) == appId) statsResult = r.Result; };

                client.SteamUserStats.RequestCurrentStats();
                var requestWatch = System.Diagnostics.Stopwatch.StartNew();
                while (statsResult == null && requestWatch.ElapsedMilliseconds < STATS_TIMEOUT_MS)
                {
                    client.RunCallbacks(false);
                    await Task.Delay(10);
                }
                // the schema file can land on disk just after the callback for a fresh download
                var answeredAt = requestWatch.ElapsedMilliseconds;
                while (statsResult == 1 && !File.Exists(path) && requestWatch.ElapsedMilliseconds < answeredAt + 1000)
                {
                    await Task.Delay(50);
                }
                Mark("request");
                timing.Append($" stats_result={statsResult?.ToString() ?? "timeout"}");

                // exit 2 = Steam refused this game's stats (e.g. no license) -> SAM skips it for good;
                // exit 6 = no answer / schema still missing -> transient, retried on a later run
                if (statsResult != null && statsResult != 1)
                {
                    log.Warn($"Steam refused stats for game {appId} (result {statsResult})");
                    return 2;
                }

                if (!File.Exists(path))
                {
                    log.Warn($"Stats schema not available for game {appId}");
                    return 6;
                }

                if (prefetch)
                {
                    return 0;
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

                var bits = stats.Children
                    .Where(s => s.Valid)
                    .SelectMany(s => s.Children ?? Enumerable.Empty<API.Types.KeyValue>())
                    .SelectMany(child => (child.Name.ToLowerInvariant() == "bits") ? (child.Children ?? Enumerable.Empty<API.Types.KeyValue>()) : Enumerable.Empty<API.Types.KeyValue>())
                    .Where(bit => !string.IsNullOrEmpty(bit["name"].AsString(string.Empty)))
                    .ToList();

                // permission bit 2 = only the game's server may set it; Steam rejects client changes
                var isProtected = bits.ToLookup(bit => (bit["permission"].AsInteger(0) & 2) != 0);
                var achievementIds = isProtected[false].Select(bit => bit["name"].AsString(string.Empty)).Distinct().ToList();
                Mark("schema");
                timing.Append($" achievements={achievementIds.Count} protected={isProtected[true].Count()}");

                if (!achievementIds.Any())
                {
                    // exit 5 = every achievement is protected -> SAM skips it for good
                    return isProtected[true].Any() ? 5 : 0;
                }

                // Steam returns false for achievements it won't let the client change (protection not flagged in the schema)
                var rejected = achievementIds.Count(id => !client.SteamUserStats.SetAchievement(id, !relock));
                Mark("set");
                timing.Append($" set_rejected={rejected}");

                if (rejected == achievementIds.Count)
                {
                    return 5;
                }

                client.SteamUserStats.StoreStats();
                Mark("store");
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
        finally
        {
            Console.WriteLine("TIMING" + timing);
        }
    }
}
