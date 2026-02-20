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

    public bool IsLibrary => _mode == ApplicationMode.Default;
    public bool IsManager => _mode == ApplicationMode.Manager;

    public MenuViewModel(HomeViewModel homeVm)
    {
        _homeVm = homeVm;
        _mode = ApplicationMode.Default;
        _unlockAllSettings = UnlockAllSettings.Load();

        _homeHandler = new ObservableHandler<HomeViewModel>(homeVm)
            .Add(h => h.CurrentVm, OnHomeViewChanged);

        // Ensure Unlock All command updates availability when library loading changes
        if (SteamLibraryManager.DefaultLibrary != null)
        {
            SteamLibraryManager.DefaultLibrary.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SteamLibrary.IsLoading))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            };
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
    public async void StartUnlockAll()
    {
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
            var allGames = library.Items.ToList();
            var gamesToUnlock = allGames.Where(g => !unlockedGameIds.Contains(g.Id.ToString())).ToList();

            if (!gamesToUnlock.Any())
            {
                MessageBox.Show("All games have already been unlocked!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"This will unlock achievements for {gamesToUnlock.Count} games (skipping {totalGames - gamesToUnlock.Count} already unlocked).\n\n" +
                $"Continue?",
                "Confirm Unlock All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                log.Info("User cancelled Unlock All operation.");
                return;
            }

            log.Info($"Starting Unlock All for {gamesToUnlock.Count} games...");

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
            try
            {
                library.IsLoading = true;

                // Steam connection check: consecutive failures indicate Steam client lost connection
                const int STEAM_FAIL_THRESHOLD = 3;
                const int STEAM_POLL_INTERVAL_MS = 5000;
                const int STEAM_RECONNECT_SETTLE_MS = 15000;
                const int MAX_STEAM_WAIT_MINUTES = 30;
                int consecutiveSteamFailures = 0;

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

                foreach (var game in gamesToUnlock)
                {
                    const int MAX_RETRIES = 1;
                    var attempts = 0;
                    var succeeded = false;

                    while (attempts <= MAX_RETRIES)
                    {
                        attempts++;
                        var shouldRetry = false;

                        try
                        {
                            // Spawn a lightweight helper process to unlock this single game.
                            var proc = SAM.Core.SAMHelper.UnlockAll((uint)game.Id);

                            if (proc == null)
                            {
                                log.Error($"Failed to start helper for game {game.Id} ({game.Name})");
                                break;
                            }

                            // wait asynchronously up to 60s for helper to finish so UI thread isn't blocked
                            var waitTask = proc.WaitForExitAsync();
                            var delayTask = Task.Delay(TimeSpan.FromSeconds(60));

                            var completed = await Task.WhenAny(waitTask, delayTask);

                            if (completed != waitTask)
                            {
                                log.Error($"Helper timed out for game {game.Id} (pid {proc.Id})");
                                try { proc.Kill(); } catch { }
                                consecutiveSteamFailures = 0;
                                break;
                            }

                            // ensure process exit code checked after completion
                            if (!proc.HasExited || proc.ExitCode != 0)
                            {
                                log.Error($"Helper failed for game {game.Id} (exit {proc.ExitCode})");

                                // Exit codes 2 (stats schema not found) and 99 indicate Steam client issues
                                if (proc.ExitCode == 2 || proc.ExitCode == 99)
                                {
                                    consecutiveSteamFailures++;

                                    if (consecutiveSteamFailures >= STEAM_FAIL_THRESHOLD && attempts <= MAX_RETRIES)
                                    {
                                        log.Warn($"{consecutiveSteamFailures} consecutive games failed with exit code {proc.ExitCode}. Possible Steam client connection issue. Waiting for Steam...");

                                        var waitedSuccessfully = await WaitForSteamClientAsync(STEAM_POLL_INTERVAL_MS, MAX_STEAM_WAIT_MINUTES, STEAM_RECONNECT_SETTLE_MS);

                                        if (!waitedSuccessfully)
                                        {
                                            log.Error($"Steam client did not reconnect within {MAX_STEAM_WAIT_MINUTES} minutes. Aborting Unlock All.");
                                            MessageBox.Show(
                                                $"Steam client connection lost and did not recover within {MAX_STEAM_WAIT_MINUTES} minutes.\n\n" +
                                                "The operation has been stopped. Progress was saved — you can resume later.",
                                                "Steam Connection Lost", MessageBoxButton.OK, MessageBoxImage.Warning);
                                            return;
                                        }

                                        log.Info("Steam client reconnected. Retrying current game...");
                                        consecutiveSteamFailures = 0;
                                        shouldRetry = true;
                                    }
                                }
                                else
                                {
                                    consecutiveSteamFailures = 0;
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
                    }

                    if (succeeded)
                    {
                        await File.AppendAllTextAsync(saveFilePath, game.Id.ToString() + Environment.NewLine);
                        unlockedGameIds.Add(game.Id.ToString());
                        log.Info($"Unlocked achievements for game {game.Id} ({game.Name})");
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

            MessageBox.Show($"Unlock All completed! Processed {gamesToUnlock.Count} games.", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            log.Info("Unlock All operation completed successfully.");
        }
        catch (Exception ex)
        {
            IsUnlocking = false;
            var message = $"An error occurred during Unlock All operation. {ex.Message}";
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

    // Command availability: disable until library is fully loaded
    public bool CanStartUnlockAll()
    {
        var lib = SteamLibraryManager.DefaultLibrary;
        if (lib == null) return false;
        return !lib.IsLoading && lib.Items != null && lib.Items.Count > 0;
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

            if (!File.Exists(saveFilePath))
            {
                MessageBox.Show("Save file does not exist.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "This will delete the save file and allow you to re-unlock all games.\n\nAre you sure?",
                "Confirm Clear Save File",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            File.Delete(saveFilePath);
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
}
