using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Mvvm.CodeGenerators;
using DevExpress.Mvvm.Native;
using log4net;
using Microsoft.Win32;
using Newtonsoft.Json;
using SAM.Core;
using SAM.Core.Storage;
using SAM.Helpers;
using SAM.Managers;

namespace SAM.ViewModels;

[GenerateViewModel]
public partial class MenuViewModel
{
    private const string GITHUB_CHANGELOG_URL = @"https://github.com/syntax-tm/SteamAchievementManager/blob/main/CHANGELOG.md";
    private const string GITHUB_ISSUES_URL = @"https://github.com/syntax-tm/SteamAchievementManager/issues";
    private const string GITHUB_URL = @"https://github.com/syntax-tm/SteamAchievementManager";

    private readonly ILog log = LogManager.GetLogger(typeof(MenuViewModel));

    private ObservableHandler<HomeViewModel> _homeHandler;

    [GenerateProperty] private HomeViewModel _homeVm;
    [GenerateProperty] private SteamGameViewModel _gameVm;
    [GenerateProperty] private ApplicationMode _mode;
    [GenerateProperty] private UnlockAllSettings _unlockAllSettings;
    [GenerateProperty] private bool _isUnlocking;
    [GenerateProperty] private double _unlockProgressPercent;
    [GenerateProperty] private string _unlockProgressText = string.Empty;
    [GenerateProperty] private string _loadingCompletedMessage = string.Empty;

    [GenerateProperty]
    private bool _canStartUnlockAll;

    private CancellationTokenSource _loadingMessageCts;

    public bool IsLibrary => _mode == ApplicationMode.Default;
    public bool IsManager => _mode == ApplicationMode.Manager;

    public MenuViewModel(HomeViewModel homeVm)
    {
        _homeVm = homeVm;
        _mode = ApplicationMode.Default;
        _unlockAllSettings = UnlockAllSettings.Load();

        _homeHandler = new ObservableHandler<HomeViewModel>(homeVm)
            .Add(h => h.CurrentVm, OnHomeViewChanged);

        // Ensure Unlock All command updates availability when library loading changes or items collection changes
        if (SteamLibraryManager.DefaultLibrary != null)
        {
            UpdateCanStartUnlockAll();

            SteamLibraryManager.DefaultLibrary.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SteamLibrary.IsLoading) || e.PropertyName == nameof(SteamLibrary.Items))
                {
                    UpdateCanStartUnlockAll();
                    CommandManager.InvalidateRequerySuggested();

                    // Show "Loaded." message when loading completes
                    if (e.PropertyName == nameof(SteamLibrary.IsLoading) && !SteamLibraryManager.DefaultLibrary.IsLoading)
                    {
                        ShowLoadingCompletedMessage();
                        StartSchemaPrefetch();
                    }
                }
            };

            if (!SteamLibraryManager.DefaultLibrary.IsLoading) StartSchemaPrefetch();

            // Also listen to collection changes if Items is already initialized
            if (SteamLibraryManager.DefaultLibrary.Items != null)
            {
                SteamLibraryManager.DefaultLibrary.Items.CollectionChanged += (s, e) =>
                {
                    UpdateCanStartUnlockAll();
                    CommandManager.InvalidateRequerySuggested();
                };
            }
        }
    }

    public MenuViewModel(SteamGameViewModel gameVm)
    {
        _gameVm = gameVm;
        _mode = ApplicationMode.Manager;
    }

    [GenerateCommand]
    public void OpenSteamConsole()
    {
        // Only log ExecCommandLine
        log.Info($"ExecCommandLine: \"C:\\Program Files (x86)\\Steam\\steam.exe\" -- steam://open/console");
        BrowserHelper.OpenSteamConsole();
    }

    [GenerateCommand]
    public void ResetAllSettings()
    {
        try
        {
            const string PROMPT = @"Are you sure you want to reset your app settings?";
            var result = MessageBox.Show(PROMPT, @"Confirm Reset", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);

            if (result != MessageBoxResult.Yes)
            {
                log.Info($"User responded '{result:G}' to reset confirmation. Cancelling...");

                return;
            }

            var currentCachePath = CacheManager.StorageManager.ApplicationStoragePath;
            var appsPath = Path.Join(currentCachePath, "apps");

            var appSettingsFiles = Directory.GetFiles(appsPath, @"*_settings.json", SearchOption.AllDirectories);

            foreach (var file in appSettingsFiles)
            {
                var fileName = Path.GetFileName(file);

                File.Delete(file);

                log.Info($"Deleted app settings file '{fileName}'.");
            }

            var settingsPath = Path.Join(currentCachePath, @"settings");

            if (Directory.Exists(settingsPath))
            {
                Directory.Delete(settingsPath, true);

                log.Info($"Deleted user settings directory '{settingsPath}'.");
            }

            HomeVm?.CurrentVm?.UnHideAll();

            HomeVm?.CurrentVm?.Library?.Items.Where(a => a.IsFavorite).ForEach(a => a.IsFavorite = false);

            log.Info("User settings reset was successful.");
        }
        catch (Exception ex)
        {
            var message = $"An error occurred attempting to reset user settings. {ex.Message}";

            log.Error(message, ex);
        }
    }

    [GenerateCommand]
    public void ViewChangelogOnGitHub()
    {
        BrowserHelper.OpenUrl(GITHUB_CHANGELOG_URL);
    }

    [GenerateCommand]
    public void ViewIssuesOnGitHub()
    {
        BrowserHelper.OpenUrl(GITHUB_ISSUES_URL);
    }

    [GenerateCommand]
    public void ViewOnGitHub()
    {
        BrowserHelper.OpenUrl(GITHUB_URL);
    }

    [GenerateCommand]
    public void ViewLogs()
    {
        const string LOG_DIR_NAME = @"logs";

        try
        {
            var assemblyLocation = AppContext.BaseDirectory;
            var currentPath = Path.GetDirectoryName(assemblyLocation);
            var logPath = Path.Join(currentPath, LOG_DIR_NAME);

            if (!Directory.Exists(logPath))
            {
                throw new DirectoryNotFoundException("Application log directory does not exist.");
            }

            var psi = new ProcessStartInfo(logPath) { UseShellExecute = true, Verb = "open" };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            var message = $"An error occurred attempting to open the log directory. {ex.Message}";

            log.Error(message, ex);
        }
    }

    [GenerateCommand]
    public void ExportApps()
    {
        const string DEFAULT_TITLE = @"Library Export";
        const string DEFAULT_FILENAME = @"apps.json";
        const string DEFAULT_EXT = @"json";
        const string DEFAULT_FILTER = "Json Files (*.json)|*.json|All Files (*.*)|*.*";

        try
        {
            var fd = new SaveFileDialog
            {
                Title = DEFAULT_TITLE,
                FileName = DEFAULT_FILENAME,
                DefaultExt = DEFAULT_EXT,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AddExtension = true,
                OverwritePrompt = true,
                CheckPathExists = true,
                Filter = DEFAULT_FILTER
            };

            var result = fd.ShowDialog();

            if (!result.HasValue || !result.Value) return;

            var apps = SteamLibraryManager.DefaultLibrary?.Items;
            var ids = apps?.Select(a => new { a.Id, a.Name, a.IsHidden, a.IsFavorite, a.GameInfoType }).ToList();
            var json = JsonConvert.SerializeObject(ids, Formatting.Indented);

            File.WriteAllText(fd.FileName, json, Encoding.UTF8);

            log.Info($"Successfully exported app list to '{fd.FileName}'.");
        }
        catch (Exception ex)
        {
            var message = $"An error occurred attempting to export the Steam library. {ex.Message}";

            log.Error(message, ex);
        }
    }

    [GenerateCommand]
    public void Exit()
    {
        Environment.Exit(0);
    }

    [GenerateCommand]
    public async void StartUnlockAll() => await RunAllAsync(false);

    // locks every achievement of every library game again, to reset a test account
    [GenerateCommand]
    public async void RelockAll() => await RunAllAsync(true);

    private async Task RunAllAsync(bool relock)
    {
        var verb = relock ? "Relock" : "Unlock";
        try
        {
            var steamId = SteamHelper.GetCurrentSteamId();
            if (string.IsNullOrEmpty(steamId))
            {
                MessageBox.Show("Could not retrieve Steam ID. Make sure Steam is installed and you are logged in.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveFilePath = SteamHelper.GetSaveFilePath(steamId);
            if (string.IsNullOrEmpty(saveFilePath))
            {
                MessageBox.Show("Failed to create save directory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var unlockedGameIds = new HashSet<string>();

            if (File.Exists(saveFilePath))
            {
                var lines = await File.ReadAllLinesAsync(saveFilePath);
                unlockedGameIds.UnionWith(lines.Where(l => !string.IsNullOrWhiteSpace(l)));
            }

            var library = SteamLibraryManager.DefaultLibrary;
            if (library?.Items == null || !library.Items.Any())
            {
                MessageBox.Show("No games found in library.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var totalGames = library.Items.Count;
            var skipFilePath = SteamHelper.GetSkipFilePath(steamId);
            var skippedGameIds = SteamHelper.LoadSkippedGames(steamId);
            var allGames = library.Items.Where(g => !skippedGameIds.Contains((uint) g.Id)).ToList();
            var ignoredCount = totalGames - allGames.Count;
            var ignoredNote = ignoredCount > 0 ? $"\n\n{ignoredCount} game(s) that can't be unlocked on this account are ignored (Clear Save File re-checks them)." : string.Empty;
            var gamesToUnlock = relock ? allGames : allGames.Where(g => !unlockedGameIds.Contains(g.Id.ToString())).ToList();

            if (!gamesToUnlock.Any())
            {
                MessageBox.Show("All games have already been unlocked!" + ignoredNote, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirmed = relock
                ? MessageBox.Show(
                      $"This will RELOCK (clear) every achievement in all {gamesToUnlock.Count} games on this Steam account.{ignoredNote}\n\nContinue?",
                      "Confirm Relock All", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes
                  && MessageBox.Show(
                      $"Are you sure?\n\nAll achievements in {gamesToUnlock.Count} games will be locked and their original unlock dates will be lost. This cannot be undone.",
                      "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes
                : MessageBox.Show(
                      $"This will unlock achievements for {gamesToUnlock.Count} games (skipping {allGames.Count - gamesToUnlock.Count} already unlocked).{ignoredNote}\n\nContinue?",
                      "Confirm Unlock All", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

            if (!confirmed)
            {
                log.Info($"User cancelled {verb} All operation.");
                return;
            }

            if (relock)
            {
                // relocked games must be processed again by the next Unlock All
                File.Delete(saveFilePath);
            }

            log.Info($"Starting {verb} All for {gamesToUnlock.Count} games...");

            // cancel any in-progress library refresh to avoid concurrent collection modifications
            try
            {
                library.CancelRefresh();
            }
            catch { }

            // Mark library as loading to make the UI resilient to collection changes during unlocks
            var previousLoading = library.IsLoading;
            var monitorCts = new CancellationTokenSource();
            Task monitorTask = null;
            var aborted = false;
            try
            {
                library.IsLoading = true;

                // Steam connection check: consecutive failures indicate Steam client lost connection
                const int STEAM_FAIL_THRESHOLD = 3;
                const int STEAM_POLL_INTERVAL_MS = 5000;
                const int STEAM_RECONNECT_SETTLE_MS = 15000;
                const int MAX_STEAM_WAIT_MINUTES = 30;
                int consecutiveSteamFailures = 0;

                // helpers running at once; each one mostly waits on Steam, so this divides total time
                const int UNLOCK_CONCURRENCY = 3;

                var maxProcessSizeMB = UnlockAllSettings?.MaxProcessSizeMB ?? 0;

                var unlockStartTime = DateTime.UtcNow;
                var processedCount = 0;
                var totalToProcess = gamesToUnlock.Count;

                IsUnlocking = true;
                UnlockProgressPercent = 0;
                UnlockProgressText = $"0% — {totalToProcess} remaining";

                // Start continuous steamwebhelper monitor in background
                if (maxProcessSizeMB > 0)
                {
                    monitorTask = MonitorSteamWebHelpersAsync(maxProcessSizeMB, monitorCts.Token);
                }

                var pending = gamesToUnlock.ToList();

                // cached schemas first so the prefetcher has time to fetch the missing ones;
                // TryClaim inside the predicates ensures a game is never run by two helpers at once
                SteamApp ClaimNextGame()
                {
                    var next = pending.FirstOrDefault(g => SchemaPrefetcher.IsCached((uint) g.Id) && SchemaPrefetcher.TryClaim((uint) g.Id))
                               ?? pending.FirstOrDefault(g => SchemaPrefetcher.TryClaim((uint) g.Id));
                    if (next != null) pending.Remove(next);
                    return next;
                }

                async Task RunWorkerAsync()
                {
                    while (!aborted && pending.Count > 0)
                    {
                        var game = ClaimNextGame();
                        if (game == null)
                        {
                            // the remaining games are being prefetched right now
                            await Task.Delay(100);
                            continue;
                        }

                        const int MAX_RETRIES = 1;
                        var attempts = 0;
                        var succeeded = false;

                        while (attempts <= MAX_RETRIES)
                        {
                            attempts++;
                            var shouldRetry = false;
                            var attemptWatch = Stopwatch.StartNew();
                            long spawnMs = 0, helperMs = 0, steamWaitMs = 0;
                            var exitCode = -1;
                            string helperOutput = null;

                            try
                            {
                                // Spawn a lightweight helper process to unlock this single game.
                                var proc = SAM.Core.SAMHelper.UnlockAll((uint)game.Id, relock ? "--relock" : string.Empty);
                                spawnMs = attemptWatch.ElapsedMilliseconds;

                                if (proc == null)
                                {
                                    log.Error($"Failed to start helper for game {game.Id} ({game.Name})");
                                    break;
                                }

                                var outputTask = proc.StandardOutput.ReadToEndAsync();

                                // healthy helpers take <1s (~16s worst case for a fresh schema download); steamclient's
                                // intermittent pipe assert can hang one for much longer, so give up sooner and retry once
                                var waitTask = proc.WaitForExitAsync();
                                var delayTask = Task.Delay(TimeSpan.FromSeconds(25));

                                var completed = await Task.WhenAny(waitTask, delayTask);
                                helperMs = attemptWatch.ElapsedMilliseconds - spawnMs;

                                if (completed == waitTask)
                                {
                                    exitCode = proc.ExitCode;
                                    helperOutput = await outputTask;
                                }

                                if (completed != waitTask)
                                {
                                    log.Error($"Helper timed out for game {game.Id} (pid {proc.Id})");
                                    try { proc.Kill(); } catch { }
                                    consecutiveSteamFailures = 0;
                                    if (attempts <= MAX_RETRIES) continue;
                                    break;
                                }

                                // ensure process exit code checked after completion
                                if (!proc.HasExited || proc.ExitCode != 0)
                                {
                                    log.Error($"Helper failed for game {game.Id} (exit {proc.ExitCode})");

                                    // 99 (unhandled helper error) and 6 (Steam didn't answer) indicate Steam client issues
                                    if (proc.ExitCode == 99 || proc.ExitCode == 6)
                                    {
                                        consecutiveSteamFailures++;

                                        if (consecutiveSteamFailures >= STEAM_FAIL_THRESHOLD && attempts <= MAX_RETRIES)
                                        {
                                            log.Warn($"{consecutiveSteamFailures} consecutive games failed with exit code {proc.ExitCode}. Possible Steam client connection issue. Waiting for Steam...");

                                            var waitStart = attemptWatch.ElapsedMilliseconds;
                                            var waitedSuccessfully = await WaitForSteamClientAsync(STEAM_POLL_INTERVAL_MS, MAX_STEAM_WAIT_MINUTES, STEAM_RECONNECT_SETTLE_MS);
                                            steamWaitMs = attemptWatch.ElapsedMilliseconds - waitStart;

                                            if (!waitedSuccessfully)
                                            {
                                                if (!aborted)
                                                {
                                                    aborted = true;
                                                    log.Error($"Steam client did not reconnect within {MAX_STEAM_WAIT_MINUTES} minutes. Aborting Unlock All.");
                                                    MessageBox.Show(
                                                        $"Steam client connection lost and did not recover within {MAX_STEAM_WAIT_MINUTES} minutes.\n\n" +
                                                        "The operation has been stopped. Progress was saved — you can resume later.",
                                                        "Steam Connection Lost", MessageBoxButton.OK, MessageBoxImage.Warning);
                                                }
                                                break;
                                            }

                                            log.Info("Steam client reconnected. Retrying current game...");
                                            consecutiveSteamFailures = 0;
                                            shouldRetry = true;
                                        }
                                    }
                                    else if (proc.ExitCode == 2 && attempts <= MAX_RETRIES)
                                    {
                                        // one refusal could be a server hiccup; only a repeated refusal marks the game as unlockable-never
                                        consecutiveSteamFailures = 0;
                                        shouldRetry = true;
                                    }
                                    else
                                    {
                                        consecutiveSteamFailures = 0;

                                        if ((proc.ExitCode == 2 || proc.ExitCode == 5) && skipFilePath != null)
                                        {
                                            var reason = proc.ExitCode == 2 ? "Steam refused stats (no license / not owned?)" : "all achievements protected";
                                            File.AppendAllText(skipFilePath, $"{game.Id}\t{reason}\t{game.Name}{Environment.NewLine}");
                                            log.Warn($"Game {game.Id} ({game.Name}) can't be unlocked: {reason}. It will be ignored from now on.");
                                        }
                                    }

                                    if (!shouldRetry) break;
                                    continue;
                                }

                                // Success
                                succeeded = true;
                                consecutiveSteamFailures = 0;
                                break;
                            }
                            catch (Exception ex)
                            {
                                log.Error($"Failed to unlock game {game.Id} ({game.Name})", ex);
                                consecutiveSteamFailures = 0;
                                break;
                            }
                            finally
                            {
                                SchemaPrefetcher.RecordTiming(relock ? "relock" : "unlock", (uint) game.Id, game.Name, attempts, exitCode, spawnMs, helperMs, steamWaitMs, helperOutput);
                            }
                        }

                        SchemaPrefetcher.Release((uint) game.Id);
                        if (aborted) return;

                        if (succeeded)
                        {
                            if (!relock)
                            {
                                // sync on purpose: workers resume on the UI thread, so appends can't overlap
                                File.AppendAllText(saveFilePath, game.Id.ToString() + Environment.NewLine);
                                unlockedGameIds.Add(game.Id.ToString());
                            }
                            log.Info($"{verb}ed achievements for game {game.Id} ({game.Name})");
                        }

                        processedCount++;
                        UnlockProgressPercent = (double)processedCount / totalToProcess * 100.0;

                        var elapsed = DateTime.UtcNow - unlockStartTime;
                        var remaining = totalToProcess - processedCount;
                        if (processedCount > 0 && remaining > 0)
                        {
                            var avgPerGame = elapsed / processedCount;
                            var eta = avgPerGame * remaining;
                            UnlockProgressText = $"{UnlockProgressPercent:F0}% — ETA: {(int)eta.TotalHours:D2}h {eta.Minutes:D2}m {eta.Seconds:D2}.{eta.Milliseconds:D3}s";
                        }
                        else if (remaining == 0)
                        {
                            UnlockProgressText = "100% — Done";
                        }
                    }
                }

                await Task.WhenAll(Enumerable.Range(0, UNLOCK_CONCURRENCY).Select(_ => RunWorkerAsync()));
            }
            finally
            {
                IsUnlocking = false;
                monitorCts.Cancel();

                if (monitorTask != null)
                {
                    try { await monitorTask; } catch { }
                }

                monitorCts.Dispose();

                try
                {
                    library.IsLoading = previousLoading;
                }
                catch { }
            }

            if (aborted) return;

            MessageBox.Show($"{verb} All completed! Processed {gamesToUnlock.Count} games.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            log.Info($"{verb} All operation completed successfully.");
        }
        catch (Exception ex)
        {
            IsUnlocking = false;
            var message = $"An error occurred during {verb} All operation. {ex.Message}";
            log.Error(message, ex);
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Continuously monitors steamwebhelper.exe processes and kills the largest one
    /// whenever it exceeds the given MB limit. Runs until cancelled.
    /// </summary>
    private async Task MonitorSteamWebHelpersAsync(int maxSizeMB, CancellationToken cancellationToken)
    {
        const string PROCESS_NAME = "steamwebhelper";
        const int POLL_INTERVAL_MS = 3000;
        var limitBytes = (long)maxSizeMB * 1024 * 1024;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var processes = Process.GetProcessesByName(PROCESS_NAME);

                Process biggest = null;
                long biggestSize = 0;

                foreach (var proc in processes)
                {
                    try
                    {
                        var workingSet = proc.WorkingSet64;
                        if (workingSet > biggestSize)
                        {
                            biggestSize = workingSet;
                            biggest = proc;
                        }
                    }
                    catch { }
                }

                if (biggest != null && biggestSize > limitBytes)
                {
                    log.Warn($"steamwebhelper.exe (pid {biggest.Id}) using {biggestSize / (1024 * 1024)} MB exceeds limit of {maxSizeMB} MB. Killing process.");
                    try
                    {
                        biggest.Kill();
                        log.Info($"Killed steamwebhelper.exe (pid {biggest.Id}).");
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to kill steamwebhelper.exe (pid {biggest.Id}).", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error monitoring steamwebhelper.exe processes.", ex);
            }

            try
            {
                await Task.Delay(POLL_INTERVAL_MS, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Polls for the Steam client process to be running. Returns true when Steam is detected,
    /// false if it doesn't appear within the timeout.
    /// </summary>
    private async Task<bool> WaitForSteamClientAsync(int pollIntervalMs, int maxWaitMinutes, int settleDelayMs)
    {
        var deadline = DateTime.UtcNow.AddMinutes(maxWaitMinutes);

        while (DateTime.UtcNow < deadline)
        {
            if (SAM.Core.SAMHelper.IsSteamRunning())
            {
                // Give Steam time to fully initialize its connections and cache after it comes back
                log.Info("Steam process detected. Waiting for client to settle...");
                await Task.Delay(settleDelayMs);

                // Verify it's still running after the settle period
                if (SAM.Core.SAMHelper.IsSteamRunning())
                {
                    return true;
                }
            }

            log.Debug("Steam client not detected. Polling again...");
            await Task.Delay(pollIntervalMs);
        }

        return false;
    }

    [GenerateCommand]
    public void ClearSaveFile()
    {
        try
        {
            var steamId = SteamHelper.GetCurrentSteamId();
            if (string.IsNullOrEmpty(steamId))
            {
                MessageBox.Show("Could not retrieve Steam ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveFilePath = SteamHelper.GetSaveFilePath(steamId);
            if (string.IsNullOrEmpty(saveFilePath))
            {
                MessageBox.Show("Save file path not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var skipFilePath = SteamHelper.GetSkipFilePath(steamId);

            if (!File.Exists(saveFilePath) && (skipFilePath == null || !File.Exists(skipFilePath)))
            {
                MessageBox.Show("Save file does not exist.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "This will delete the save file and allow you to re-unlock all games.\n" +
                "Games marked as impossible to unlock will be checked again too.\n\nAre you sure?",
                "Confirm Clear Save File",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            File.Delete(saveFilePath);
            if (skipFilePath != null) File.Delete(skipFilePath);
            log.Info($"Deleted save file: {saveFilePath}");

            MessageBox.Show("Save file cleared successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var message = $"An error occurred attempting to clear the save file. {ex.Message}";
            log.Error(message, ex);
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnHomeViewChanged()
    {
    }

    // cache missing stats schemas in the background so Unlock All starts with everything on disk
    private void StartSchemaPrefetch()
    {
        try
        {
            var items = SteamLibraryManager.DefaultLibrary?.Items;
            if (items == null || items.Count == 0) return;

            var skipped = SteamHelper.LoadSkippedGames(SteamHelper.GetCurrentSteamId());
            SchemaPrefetcher.Start(items.Select(g => (uint) g.Id).Where(id => !skipped.Contains(id)).ToList());
        }
        catch (Exception ex)
        {
            log.Warn("Failed to start stats schema prefetch.", ex);
        }
    }

    private void UpdateCanStartUnlockAll()
    {
        var lib = SteamLibraryManager.DefaultLibrary;
        CanStartUnlockAll = lib != null && !lib.IsLoading && lib.Items != null && lib.Items.Count > 0;
    }

    private void ShowLoadingCompletedMessage()
    {
        try
        {
            // Cancel any pending message clear
            _loadingMessageCts?.Cancel();
            _loadingMessageCts = new CancellationTokenSource();

            LoadingCompletedMessage = "Loaded.";

            // Clear the message after 3 seconds
            _ = Task.Delay(3000, _loadingMessageCts.Token).ContinueWith(_ =>
            {
                if (!_loadingMessageCts.Token.IsCancellationRequested)
                {
                    LoadingCompletedMessage = string.Empty;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception ex)
        {
            log.Error("Error showing loading completed message", ex);
        }
    }
}
