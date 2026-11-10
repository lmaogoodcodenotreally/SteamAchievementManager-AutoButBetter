using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace SAM.Managers;

/// <summary>
/// Makes Steam cache missing stats schemas in the background (helper in --prefetch mode, nothing is changed),
/// so Unlock All finds them on disk. Also owns the claim list that stops two helpers running the same app.
/// </summary>
public static class SchemaPrefetcher
{
    // few lanes: each one shows as a running game in Steam
    private const int CONCURRENCY = 2;
    private const int HELPER_TIMEOUT_MS = 30000;

    private static readonly ILog log = LogManager.GetLogger(nameof(SchemaPrefetcher));
    private static readonly ConcurrentDictionary<uint, byte> claimed = new();
    private static readonly string[] TIMING_PHASES = { "startup", "init", "schema_on_disk", "request", "stats_result", "schema", "achievements", "protected", "set", "set_rejected", "store" };
    private static readonly object timingFileLock = new();
    private static CancellationTokenSource _cts;

    public static bool IsCached(uint appId) =>
        File.Exists(Path.Join(SteamClientManager.SteamInstallPath, "appcache", "stats", $"UserGameStatsSchema_{appId}.bin"));

    public static bool TryClaim(uint appId) => claimed.TryAdd(appId, 0);

    public static void Release(uint appId) => claimed.TryRemove(appId, out _);

    public static void Start(IEnumerable<uint> appIds)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var queue = new ConcurrentQueue<uint>(appIds.Where(id => !IsCached(id)));
        if (queue.IsEmpty) return;

        log.Info($"Prefetching {queue.Count} missing stats schema(s)...");

        for (var i = 0; i < CONCURRENCY; i++)
        {
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && queue.TryDequeue(out var appId))
                {
                    // an unlock worker may have taken it, or it got cached meanwhile
                    if (IsCached(appId) || !TryClaim(appId)) continue;

                    try
                    {
                        await PrefetchAsync(appId);
                    }
                    catch (Exception e)
                    {
                        log.Warn($"Schema prefetch failed for app {appId}. {e.Message}", e);
                    }
                    finally
                    {
                        Release(appId);
                    }
                }
            }, token);
        }
    }

    private static async Task PrefetchAsync(uint appId)
    {
        var watch = Stopwatch.StartNew();
        using var proc = SAM.Core.SAMHelper.UnlockAll(appId, "--prefetch");
        var spawnMs = watch.ElapsedMilliseconds;
        if (proc == null) return;

        var output = proc.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(HELPER_TIMEOUT_MS);
        var exitCode = -1;
        try
        {
            await proc.WaitForExitAsync(timeout.Token);
            exitCode = proc.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(); } catch { }
        }

        RecordTiming("prefetch", appId, null, 1, exitCode, spawnMs, watch.ElapsedMilliseconds - spawnMs, 0, exitCode == -1 ? null : await output);
    }

    // one CSV row per helper run: logs\unlock_timings.csv (phase columns are ms, from the helper's TIMING line)
    public static void RecordTiming(string mode, uint appId, string name, int attempt, int exitCode, long spawnMs, long helperMs, long steamWaitMs, string helperOutput)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "logs", "unlock_timings.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var phases = (helperOutput ?? string.Empty)
                .Split('\n')
                .FirstOrDefault(l => l.StartsWith("TIMING"))?
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => p.Split('='))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => p[1]) ?? new Dictionary<string, string>();

            var header = "time,mode,appid,name,attempt,exit,spawn_ms,helper_ms,steam_wait_ms," + string.Join(",", TIMING_PHASES);
            var row = string.Join(",", new object[]
            {
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), mode, appId, $"\"{name?.Replace("\"", "'")}\"",
                attempt, exitCode, spawnMs, helperMs, steamWaitMs
            }.Concat(TIMING_PHASES.Select(p => phases.TryGetValue(p, out var v) ? v : string.Empty)));

            // prefetch lanes and unlock workers write concurrently
            lock (timingFileLock)
            {
                File.AppendAllText(path, (File.Exists(path) ? string.Empty : header + Environment.NewLine) + row + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            log.Warn($"Failed to record helper timing for app {appId}.", ex);
        }
    }
}
