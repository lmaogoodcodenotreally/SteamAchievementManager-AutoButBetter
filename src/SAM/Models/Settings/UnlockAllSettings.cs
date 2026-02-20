using System;
using System.ComponentModel.DataAnnotations;
using DevExpress.Mvvm;
using DevExpress.Mvvm.CodeGenerators;
using log4net;
using Newtonsoft.Json;
using SAM.Core;
using SAM.Core.Storage;

namespace SAM;

[GenerateViewModel]
[MetadataType(typeof(UnlockAllSettingsMetaData))]
public partial class UnlockAllSettings : BindableBase
{
    private const string NAME = "unlockAllSettings.json";
    private const int DEFAULT_MAX_PROCESS_SIZE_MB = 1500;

    private static readonly ILog log = LogManager.GetLogger(typeof(UnlockAllSettings));
    private static readonly CacheKey cacheKey = new (NAME, CacheKeyType.Settings);

    /// <summary>
    /// Maximum allowed working set size (in MB) for a single steamwebhelper.exe process.
    /// When 0, monitoring is disabled.
    /// </summary>
    [GenerateProperty(OnChangedMethod = nameof(Save))]
    private int _maxProcessSizeMB = DEFAULT_MAX_PROCESS_SIZE_MB;

    [JsonIgnore]
    public bool Loaded { get; private set; }

    [JsonConstructor]
    private UnlockAllSettings()
    {
    }

    public static UnlockAllSettings Load()
    {
        try
        {
            var settings = new UnlockAllSettings();
            CacheManager.TryPopulateObject(cacheKey, settings);
            settings.Loaded = true;
            return settings;
        }
        catch (Exception e)
        {
            log.Error($"An error occurred attempting to load the {nameof(UnlockAllSettings)}. {e.Message}", e);
            throw;
        }
    }

    public void Save()
    {
        if (!Loaded) return;

        try
        {
            CacheManager.CacheObject(cacheKey, this);
            log.Info($"Saved {nameof(UnlockAllSettings)}.");
        }
        catch (Exception e)
        {
            log.Error($"An error occurred attempting to save the {nameof(UnlockAllSettings)}. {e.Message}", e);
        }
    }
}

public class UnlockAllSettingsMetaData
{
    [JsonProperty]
    public int MaxProcessSizeMB { get; set; }
}
