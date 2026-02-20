using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Xml;
using DevExpress.Mvvm.CodeGenerators;
using log4net;
using SAM.API;
using SAM.Core;
using SAM.Core.Extensions;

namespace SAM;

[GenerateViewModel]
public partial class SteamUser
{
    private const string PROFILE_URL_FORMAT = @"https://steamcommunity.com/profiles/{0}";
    private const string PROFILE_XML_URL_FORMAT = @"https://steamcommunity.com/profiles/{0}?xml=1";

    private static readonly ILog log = LogManager.GetLogger(nameof(SteamUser));
    private readonly Client _client;
    [GenerateProperty] private ulong _steamId64;
    [GenerateProperty] private string _steamId;
    [GenerateProperty] private string _profileUrl;
    [GenerateProperty] private string _avatarIcon;
    [GenerateProperty] private string _avatarMedium;
    [GenerateProperty] private string _avatarFull;
    [GenerateProperty] private ImageSource _avatar;
    public SteamUser(Client client)
    {
        _client = client;

        _ = RefreshProfile();
    }

    public async Task Refresh()
    {
        log.Info("SteamUser refresh requested");
        await Task.Run(async () => await RefreshProfile());
    }

    // Read-only display string combining persona name and 64-bit id
    public string PersonaAndId => !string.IsNullOrEmpty(SteamId)
        ? string.Format("{0} ({1})", SteamId, SteamId64)
        : SteamId64 == 0 ? string.Empty : SteamId64.ToString();

    private async Task RefreshProfile()
    {
            log.Info("RefreshProfile method entered");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile START\n"); } catch { }
            try
            {
                log.Info("Starting SteamUser profile refresh...");
                log.Debug($"Client is null: {_client == null}");
                if (_client == null)
                {
                    log.Error("Cannot refresh SteamUser: Client is null");
                    return;
                }
                log.Debug($"Client.SteamUser is null: {_client.SteamUser == null}");
                if (_client.SteamUser == null)
                {
                    log.Error("Cannot refresh SteamUser: Steam API user is null");
                    return;
                }
                var oldSteamId = SteamId64;
                log.Debug("About to call GetSteamId()");
                await Task.Delay(500); // Wait for potential user switch to complete
                log.Debug("Delay completed, about to get Steam ID");
                log.Debug($"Client is still null: {_client == null}");
                log.Debug($"Client.SteamUser is still null: {_client.SteamUser == null}");
                if (_client.SteamUser == null)
                {
                    log.Error("SteamUser is null after delay");
                    return;
                }
                log.Debug("Checking if user is logged in...");
                if (!_client.SteamUser.IsLoggedIn())
                {
                    log.Info("User is not logged in, skipping Steam ID retrieval");
                    return;
                }
                try
                {
                    SteamId64 = _client.SteamUser.GetSteamId();
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to get Steam ID from API: {ex.Message}", ex);
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile GetSteamId failed: {ex.Message}\n"); } catch { }
                    return;
                }
                log.Info($"SteamId64: {oldSteamId} -> {SteamId64}");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile SteamId64 set to {SteamId64}\n"); } catch { }
                log.Debug($"SteamId64 retrieved: {SteamId64}");
                if (SteamId64 == 0)
                {
                    log.Error("Invalid Steam ID (0) - user may not be logged in or account switched");
                    return;
                }
            ProfileUrl = string.Format(PROFILE_URL_FORMAT, SteamId64);
            log.Debug($"Profile URL: {ProfileUrl}");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile ProfileUrl set\n"); } catch { }
            // Use XML profile data for avatar info (no playerSummaries parsing)
                try
                {
                    log.Debug("Falling back to XML method for avatar...");
                    var xmlFeedUrl = string.Format(PROFILE_XML_URL_FORMAT, SteamId64);
                    log.Debug($"XML URL: {xmlFeedUrl}");
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile XML URL: {xmlFeedUrl}\n"); } catch { }
                    try
                    {
                        using var reader = new XmlTextReader(xmlFeedUrl);
                        var doc = new XmlDocument();
                        doc.Load(reader);
                        SteamId = doc.GetValue(@"//steamID");
                        log.Debug($"Steam ID from XML: {SteamId}");
                        AvatarIcon = doc.GetValue(@"//avatarIcon");
                        AvatarMedium = doc.GetValue(@"//avatarMedium");
                        AvatarFull = doc.GetValue(@"//avatarFull");
                        log.Info($"Avatar from XML: {AvatarFull}");
                        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile AvatarFull={AvatarFull}\n"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to load XML profile data from {xmlFeedUrl}: {ex.Message}", ex);
                        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile XML load failed: {ex.Message}\n"); } catch { }
                        throw;
                    }
                }
            catch (Exception ex)
            {
                log.Warn($"Failed to retrieve XML profile data: {ex.Message}", ex);
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile XML retrieval failed: {ex.Message}\n"); } catch { }
            }
            if (!string.IsNullOrEmpty(AvatarFull) || !string.IsNullOrEmpty(AvatarMedium) || !string.IsNullOrEmpty(AvatarIcon))
            {
                log.Debug("Creating avatar image source (with fallbacks)...");
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile Starting avatar loading\n"); } catch { }

                var candidates = new[] { AvatarFull, AvatarMedium, AvatarIcon };
                ImageSource result = null;
                using var http = new HttpClient();
                foreach (var url in candidates)
                {
                    if (string.IsNullOrEmpty(url)) continue;
                    try
                    {
                        log.Debug($"Attempting to load avatar from {url}");
                        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile Avatar attempt: {url}\n"); } catch { }
                        using var resp = await http.GetAsync(url);
                        // If the resource is missing (404) then stop trying other candidates
                        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            log.Debug($"Avatar URL returned 404 Not Found: {url}");
                            break;
                        }

                        if (!resp.IsSuccessStatusCode)
                        {
                            log.Debug($"Avatar URL returned non-success status {resp.StatusCode}: {url}");
                            continue;
                        }

                        var bytes = await resp.Content.ReadAsByteArrayAsync();
                        if (bytes == null || bytes.Length == 0)
                        {
                            log.Debug($"Avatar URL returned empty content: {url}");
                            continue;
                        }

                        using var ms = new MemoryStream(bytes);
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = ms;
                        bmp.EndInit();
                        bmp.Freeze();
                        result = bmp;
                        log.Info($"Avatar image loaded from {url}");
                        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile Avatar loaded from {url}\n"); } catch { }
                        break;
                    }
                    catch (HttpRequestException hre)
                    {
                        log.Debug($"Failed to load avatar from {url}: {hre.Message}");
                        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile Avatar HttpRequestException: {hre.Message}\n"); } catch { }
                        continue;
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"Failed to load avatar from {url}: {ex.Message}");
                        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile Avatar Exception: {ex.Message}\n"); } catch { }
                        continue;
                    }
                }

                if (result != null)
                {
                    Avatar = result;
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile Avatar set\n"); } catch { }
                }
                else
                {
                    log.Warn("No avatar image available after checking fallbacks");
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile No avatar available\n"); } catch { }
                }
            }
            else
            {
                log.Warn("No avatar URL available");
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile No avatar URL\n"); } catch { }
            }
            log.Info($"Finished loading steam user {SteamId} ({SteamId64}) user profile.");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} SteamUser.RefreshProfile FINISHED\n"); } catch { }
        }
        catch (Exception e)
        {
            var message = $"An error occurred loading user information. {e.Message}";
            log.Error(message, e);
            return;
        }
    }

    // removed playerSummaries parsing helper
}
