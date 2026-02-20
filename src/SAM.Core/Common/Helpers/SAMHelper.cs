#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using log4net;
using SAM.Core.Extensions;

namespace SAM.Core
{
    public static class SAMHelper
    {
        private const string SAM_EXE = @"SAM.exe";
        private const string STEAM_PROCESS_NAME = @"Steam";

        private const string SAM_PROCESS_REGEX = @"^SAM(?:\.exe)?$";

        private static readonly ILog log = LogManager.GetLogger(nameof(SAMHelper));

        public static void VerifySteamProcess()
        {
            if (IsSteamRunning()) return;

            //  TODO: Change the error message to indicate that Steam needs to be started
            throw new SAMInitializationException(@"Steam process is not currently running.");
        }

        public static bool IsSteamRunning()
        {
            var processes = Process.GetProcessesByName(STEAM_PROCESS_NAME);
            return processes.Any();
        }

        public static bool IsPickerRunning()
        {
            var processes = Process.GetProcesses();
            return processes.Any(p => Regex.IsMatch(p.ProcessName, SAM_PROCESS_REGEX));
        }

        public static Process? OpenPicker()
        {
            if (!File.Exists(SAM_EXE))
            {
                throw new FileNotFoundException($"Unable to start '{SAM_EXE}' because it does not exist.", SAM_EXE);
            }

            var proc = Process.Start(SAM_EXE);

            proc.SetActive();

            return proc;
        }

        public static Process? OpenManager(uint appId)
        {
            if (appId == default) throw new ArgumentException($"App id {appId} is not valid.", nameof(appId));
            
            if (!File.Exists(SAM_EXE))
            {
                throw new FileNotFoundException($"Unable to start '{SAM_EXE}' because it does not exist.", SAM_EXE);
            }
            
            string[] args = [ "manage", $"{appId}" ];
            var psi = new ProcessStartInfo(SAM_EXE, args);
            var proc = Process.Start(psi);

            proc.SetActive();

            return proc;
        }

        public static Process? UnlockAll(uint appId)
        {
            if (appId == default) throw new ArgumentException($"App id {appId} is not valid.", nameof(appId));

            // helper exe shipped next to the main exe
            var assemblyDir = AppContext.BaseDirectory;
            var helperName = "SAM.Unlocker.exe";
            var helperPath = Path.Combine(assemblyDir, helperName);

            if (!File.Exists(helperPath))
            {
                throw new FileNotFoundException($"Unable to start helper '{helperName}' because it does not exist.", helperPath);
            }

            // Discover Steam library folders by parsing libraryfolders.vdf in common locations
            var libraryPaths = new List<string>();
            try
            {
                var vdfCandidates = new List<string>
                {
                    Path.Combine(assemblyDir, "steamapps", "libraryfolders.vdf"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "libraryfolders.vdf"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "libraryfolders.vdf"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Steam", "steamapps", "libraryfolders.vdf"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", "Steam", "steamapps", "libraryfolders.vdf")
                };

                // also walk up a few directories from assembly dir
                var cur = assemblyDir.TrimEnd(Path.DirectorySeparatorChar);
                for (int i = 0; i < 4; i++)
                {
                    var tryPath = Path.Combine(cur, "steamapps", "libraryfolders.vdf");
                    vdfCandidates.Add(tryPath);
                    var parent = Path.GetDirectoryName(cur);
                    if (string.IsNullOrEmpty(parent)) break;
                    cur = parent;
                }

                foreach (var vdf in vdfCandidates.Distinct())
                {
                    try
                    {
                        if (!File.Exists(vdf)) continue;
                        var txt = File.ReadAllText(vdf);
                        var matches = System.Text.RegularExpressions.Regex.Matches(txt, "\"path\"\\s*\"([^\"]+)\"");
                        foreach (System.Text.RegularExpressions.Match m in matches)
                        {
                            var p = m.Groups[1].Value.Replace("\\\\", "\\");
                            if (!string.IsNullOrWhiteSpace(p) && !libraryPaths.Contains(p))
                            {
                                libraryPaths.Add(p);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // If we found at least one library path, put helper into the first SAM folder we can create or find.
            var samDirs = new List<string>();
            foreach (var lib in libraryPaths)
            {
                try
                {
                    var samDir = Path.Combine(lib, "steamapps", "common", "SAM");
                    if (!Directory.Exists(samDir))
                    {
                        try { Directory.CreateDirectory(samDir); } catch { }
                    }

                    if (Directory.Exists(samDir)) samDirs.Add(samDir);
                }
                catch { }
            }

            // fallback: if no SAM library folder found, use assembly dir (previous behavior)
            if (samDirs.Count == 0)
            {
                samDirs.Add(assemblyDir);
            }

            Process? startedProc = null;
            foreach (var samDir in samDirs)
            {
                var targetExe = Path.Combine(samDir, $"testapp_{appId}.exe");
                try
                {
                    // Ensure only one testapp_* exists in the SAM folder at any time.
                    try
                    {
                        var existing = Directory.GetFiles(samDir, "testapp_*.exe");
                        foreach (var exf in existing)
                        {
                            if (string.Equals(Path.GetFullPath(exf), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase)) continue;
                            try { File.Delete(exf); log.Debug($"Deleted other testapp in folder: {exf}"); } catch { }
                        }
                    }
                    catch { }

                    File.Copy(helperPath, targetExe, true);
                }
                catch (Exception e)
                {
                    log.Debug($"Failed to copy helper to '{targetExe}'", e);
                    continue;
                }

                var psi = new ProcessStartInfo(targetExe)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = samDir
                };

                try
                {
                    var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        startedProc = proc;
                        proc.EnableRaisingEvents = true;
                        proc.Exited += (s, e) =>
                        {
                            try { File.Delete(targetExe); } catch { }
                        };
                        // only start in the first usable folder
                        break;
                    }
                }
                catch (Exception ex)
                {
                    log.Debug($"Failed to start helper in '{samDir}'", ex);
                }
            }

            // Attempt to clean up stale testapp executables in discovered SAM folders
            try
            {
                CleanupStaleTestApps(assemblyDir, samDirs);
            }
            catch (Exception ex)
            {
                log.Warn("Failed to cleanup stale testapp files", ex);
            }

            return startedProc;
        }
        
        public static void CloseAllManagers()
        {
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    if (!Regex.IsMatch(proc.ProcessName, SAM_PROCESS_REGEX)) continue;

                    log.Info($"Found SAM Manager process with process ID {proc.Id}.");

                    proc.Kill();
                }
            }
            catch (Exception e)
            {
                var message = $"An error occurred attempting to stop the running SAM Manager processes. {e.Message}";
                log.Error(message, e);
            }
        }

        private static void CleanupStaleTestApps(string assemblyDir, IEnumerable<string> samDirs)
        {
            try
            {
                var metaFile = Path.Combine(assemblyDir, "testapp_metadata.json");
                if (!File.Exists(metaFile)) return;

                var json = File.ReadAllText(metaFile);
                // simple parse without newtonsoft to avoid extra deps: expecting {"helperFile":"SAM.Unlocker.exe","hash":"...","size":12345}
                var hashMatch = System.Text.RegularExpressions.Regex.Match(json, "\"hash\"\\s*:\\s*\"([0-9a-fA-F]+)\"");
                var sizeMatch = System.Text.RegularExpressions.Regex.Match(json, "\"size\"\\s*:\\s*(\\d+)");
                if (!hashMatch.Success || !sizeMatch.Success) return;

                var expectedHash = hashMatch.Groups[1].Value;
                if (!long.TryParse(sizeMatch.Groups[1].Value, out var expectedSize)) return;

                foreach (var samDir in samDirs)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(samDir) || !Directory.Exists(samDir)) continue;
                        var di = new DirectoryInfo(samDir);
                        var candidates = di.GetFiles("testapp_*.exe");

                        foreach (var f in candidates)
                        {
                            try
                            {
                                var nameNoExt = Path.GetFileNameWithoutExtension(f.Name);
                                var m = System.Text.RegularExpressions.Regex.Match(nameNoExt, "testapp_(\\d+)$");
                                if (!m.Success) continue;

                                var delta = Math.Abs(f.Length - expectedSize);
                                var threshold = Math.Min((long)(expectedSize * 0.02), 4096L);
                                if (delta > threshold) continue;

                                bool inUse = false;
                                FileStream? stream = null;
                                try { stream = new FileStream(f.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
                                catch { inUse = true; }
                                if (stream != null) stream.Dispose();
                                if (inUse) continue;

                                using var fs = File.OpenRead(f.FullName);
                                using var sha = System.Security.Cryptography.SHA256.Create();
                                var computed = sha.ComputeHash(fs);
                                var computedHex = BitConverter.ToString(computed).Replace("-", string.Empty).ToLowerInvariant();

                                if (string.Equals(computedHex, expectedHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    try { File.Delete(f.FullName); log.Info($"Deleted stale testapp helper: {f.FullName}"); }
                                    catch (Exception e) { log.Warn($"Failed to delete stale testapp helper {f.FullName}", e); }
                                }
                            }
                            catch (Exception e)
                            {
                                log.Debug($"Error while examining file {f.FullName}: {e.Message}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        log.Debug($"Error while scanning SAM dir {samDir}: {e.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn("CleanupStaleTestApps encountered an error", ex);
            }
        }
    }
}
