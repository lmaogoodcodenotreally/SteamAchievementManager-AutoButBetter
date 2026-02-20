using System;
using System.IO;
using System.Text.RegularExpressions;
using log4net;
using Microsoft.Win32;

namespace SAM.Helpers;

public static class SteamHelper
{
    private static readonly ILog log = LogManager.GetLogger(typeof(SteamHelper));

    public static string GetSteamInstallPath()
    {
        try
        {
            return (string)Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null);
        }
        catch (Exception ex)
        {
            log.Error("Failed to get Steam install path from registry.", ex);
            return null;
        }
    }

    public static string GetCurrentSteamId()
    {
        try
        {
            var installPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(installPath))
            {
                log.Warn("Could not find the Steam installation path.");
                return null;
            }

            var filePath = Path.Combine(installPath, "config", "loginusers.vdf");
            log.Debug($"Attempting to read Steam ID from: {filePath}");

            if (!File.Exists(filePath))
            {
                log.Warn($"Steam login users file not found at: {filePath}");
                return null;
            }

            var contents = File.ReadAllText(filePath);
            var regex = new Regex(@"""(\d{17})""\s*\{[^}]*""MostRecent""\s*""1""");
            var match = regex.Match(contents);

            if (match.Success)
            {
                var steamId = match.Groups[1].Value;
                log.Info($"Successfully retrieved Steam ID64: {steamId}");
                return steamId;
            }

            log.Warn("Could not find active Steam ID in loginusers.vdf");
            return null;
        }
        catch (Exception ex)
        {
            log.Error("Error retrieving current Steam ID.", ex);
            return null;
        }
    }

    public static string GetSaveDirectory(string steamId)
    {
        if (string.IsNullOrEmpty(steamId))
        {
            log.Warn("Cannot create save directory: Steam ID is null or empty.");
            return null;
        }

        try
        {
            var directoryPath = Path.Combine(".", "data", "saves", steamId);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                log.Info($"Created save directory: {directoryPath}");
            }

            return directoryPath;
        }
        catch (Exception ex)
        {
            log.Error($"Failed to create save directory for Steam ID {steamId}.", ex);
            return null;
        }
    }

    public static string GetSaveFilePath(string steamId)
    {
        var saveDirectory = GetSaveDirectory(steamId);
        if (string.IsNullOrEmpty(saveDirectory))
        {
            return null;
        }

        return Path.Combine(saveDirectory, "save.unlocks");
    }
}
