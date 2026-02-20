using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DevExpress.Mvvm;
using System.Windows.Data;
using DevExpress.Mvvm.CodeGenerators;
using log4net;
using SAM.Core.Extensions;
using SAM.Core.Messages;
using SAM.Core;
using SAM.Managers;
using SAM.Services;
using SAM;

namespace SAM.ViewModels;

[GenerateViewModel(ImplementISupportServices = true)]
public partial class LibraryViewModel
{
    protected readonly ILog log = LogManager.GetLogger(nameof(HomeViewModel));
    protected IGroupViewService groupViewService => GetService<IGroupViewService>();

    private readonly ObservableHandler<ILibrarySettings> _settingsHandler;

    [GenerateProperty] protected SteamUser _user;

    protected CollectionViewSource _itemsViewSource;
    protected bool _loading = true;
    private ILibrarySettings _settings;
    
    [GenerateProperty] protected string _filterText;
    [GenerateProperty] protected bool _filterNormal;
    [GenerateProperty] protected bool _filterDemos;
    [GenerateProperty] protected bool _filterMods;
    [GenerateProperty] protected bool _filterJunk;
    [GenerateProperty] protected string _filterTool;
    [GenerateProperty] protected ICollectionView _itemsView;
    [GenerateProperty] protected List<string> _suggestions;
    [GenerateProperty] protected SteamApp _selectedItem;
    [GenerateProperty] protected SteamLibrary _library;

    protected LibraryViewModel(ILibrarySettings settings, SteamUser user = null)
    {
        User = user;
        _settings = settings;

        _settingsHandler = new ObservableHandler<ILibrarySettings>(settings)
            .Add(s => s.EnableGrouping, OnEnableGroupingChanged)
            .Add(s => s.ShowHidden, OnShowHiddenChanged)
            .Add(s => s.ShowFavoritesOnly, OnFilterFavoritesChanged);

        Messenger.Default.Register<ActionMessage>(this, OnActionMessage);
    }

    [GenerateCommand]
    public void ExpandAll()
    {
        groupViewService.ExpandAll();
    }

    [GenerateCommand]
    public void CollapseAll()
    {
        groupViewService.CollapseAll();
    }

    [GenerateCommand]
    public void ToggleShowHidden()
    {
        if (_settings == null) return;

        _settings.ShowHidden = !_settings.ShowHidden;
    }
    
    [GenerateCommand]
    public void ToggleEnableGrouping()
    {
        if (_settings == null) return;

        _settings.EnableGrouping = !_settings.EnableGrouping;
    }

    [GenerateCommand]
    public void UnHideAll()
    {
        // TODO: consider adding confirmation before clearing the user's hidden apps
        var hidden = _library!.Items.Where(a => a.IsHidden).ToList();

        hidden.ForEach(a => a.ToggleVisibility());
    }

    [GenerateCommand]
    public void ManageApp()
    {
        if (SelectedItem == null) return;

        SAMHelper.OpenManager(SelectedItem.Id);
    }

    [GenerateCommand]
    public async void Refresh(bool force = false)
    {
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh START (force:{force})\n"); } catch { }
        try
        {
            if (_settings == null) return;

            log.Info($"Library refresh started (force: {force})");

            _loading = true;

            // Refresh user profile in case account changed
            if (User != null)
            {
                log.Info("Refreshing SteamUser profile...");
                try
                {
                    await User.Refresh();
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh User.Refresh completed\n"); } catch { }
                }
                catch (Exception ux)
                {
                    log.Error($"Error refreshing SteamUser: {ux.Message}", ux);
                    SAM.Core.Logging.CrashLogger.Log(ux, "User.Refresh");
                }
            }

            log.Info("Starting SteamLibrary refresh...");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh Starting SteamLibrary.Refresh\n"); } catch { }
            if (force)
            {
                SteamLibraryManager.DefaultLibrary.Refresh();
            }
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh SteamLibrary.Refresh called\n"); } catch { }

            Library ??= SteamLibraryManager.DefaultLibrary;

            // Ensure the view source uses a snapshot of the library items to avoid
            // CollectionView construction copying the collection while it is being
            // modified on a background thread (causes IndexOutOfRangeException).
            var snapshot = Library.Items.ToList();
            log.Info($"Created snapshot with {snapshot.Count} items");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh Snapshot created with {snapshot.Count} items\n"); } catch { }

            // ReSharper disable once RedundantCheckBeforeAssignment
            if (_itemsViewSource == null)
            {
                _itemsViewSource = new CollectionViewSource();
            }

            // Assign Source on the UI dispatcher to ensure WPF safely creates the view
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => _itemsViewSource.Source = snapshot);
            }
            else
            {
                _itemsViewSource.Source = snapshot;
            }
            log.Info("View source updated with snapshot");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh View source set\n"); } catch { }

            using (_itemsViewSource.DeferRefresh())
            {
                _itemsViewSource.GroupDescriptions.Clear();
                _itemsViewSource.LiveGroupingProperties.Clear();

                if (_settings.EnableGrouping)
                {
                    _itemsViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SteamApp.Group)));
                    _itemsViewSource.LiveGroupingProperties.Add(nameof(SteamApp.Group));
                }

                _itemsViewSource.IsLiveGroupingRequested = _settings.EnableGrouping;

                _itemsViewSource.SortDescriptions.Clear();
                _itemsViewSource.LiveSortingProperties.Clear();

                if (_settings.EnableGrouping)
                {
                    _itemsViewSource.SortDescriptions.Add(new (nameof(SteamApp.GroupSortIndex), ListSortDirection.Ascending));
                }

                _itemsViewSource.SortDescriptions.Add(new (nameof(SteamApp.Name), ListSortDirection.Ascending));
                
                _itemsViewSource.IsLiveSortingRequested = true;

                _itemsViewSource.LiveFilteringProperties.Clear();
                _itemsViewSource.LiveFilteringProperties.Add(nameof(SteamApp.IsHidden));
                _itemsViewSource.LiveFilteringProperties.Add(nameof(SteamApp.IsFavorite));
                _itemsViewSource.LiveFilteringProperties.Add(nameof(SteamApp.GameInfoType));
                
                // Avoid adding the same handler multiple times
                _itemsViewSource.Filter -= ItemsViewSourceOnFilter;
                _itemsViewSource.Filter += ItemsViewSourceOnFilter;
                
                _itemsViewSource.IsLiveFilteringRequested = true;
            }

            // Always reassign the ItemsView to force WPF to recreate the view when grouping changes
            ItemsView = _itemsViewSource.View;
            ItemsView!.Refresh();
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh ItemsView refreshed\n"); } catch { }
        }
        catch (Exception e)
        {
            log.Fatal($"Unhandled exception in LibraryViewModel.Refresh: {e.Message}", e);
            SAM.Core.Logging.CrashLogger.Log(e, "LibraryViewModel.Refresh");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh EXCEPTION: {e.Message}\n"); } catch { }
        }
        finally
        {
            _loading = false;
            log.Info("Library refresh completed");
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_instrument.log"), $"{DateTime.UtcNow:O} LibraryViewModel.Refresh FINISHED\n"); } catch { }
        }

        // suggestions are sorted by favorites first, then normal (non-favorite & non-hidden) apps,
        // and then any hidden apps
        try
        {
            Suggestions = Library.Items.ToList()
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name))
                .OrderBy(a => a.GroupSortIndex)
                .ThenBy(a => a.Name ?? string.Empty)
                .Select(a => a.Name ?? string.Empty)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            log.Error($"Error creating suggestions: {ex.Message}", ex);
            Suggestions = new List<string>();
        }

        _loading = false;
    }

    protected void OnFilterTextChanged()
    {
        if (_loading) return;

        ItemsView?.Refresh();
    }

    protected void OnShowHiddenChanged()
    {
        if (_loading) return;
        
        ItemsView?.Refresh();
    }

    protected void OnFilterFavoritesChanged()
    {
        if (_loading) return;
        
        ItemsView?.Refresh();
    }

    protected void OnEnableGroupingChanged()
    {
        if (_loading) return;

        Refresh();
    }

    protected virtual void OnActionMessage(ActionMessage message)
    {
        if (_loading) return;

        // on library refresh completed
        if (message.EntityType == EntityType.Library && message.ActionType == ActionType.Refreshed)
        {
            ItemsView?.Refresh();
        }
        // react to home settings changes (e.g., grouping/show-image toggles)
        if (message.EntityType == EntityType.HomeSettings && message.ActionType == ActionType.Changed)
        {
            // Refresh will re-apply grouping, sorting and filtering based on the current settings
            Refresh();
        }
    }

    protected virtual void ItemsViewSourceOnFilter(object sender, FilterEventArgs e)
    {
        if (e.Item == null) return;
        if (e.Item is not SteamApp app) throw new ArgumentException(nameof(e.Item));
        if (_settings == null) return;

        var hasNameFilter = !string.IsNullOrWhiteSpace(FilterText);
        var isNameMatch = !hasNameFilter || (app.Name?.ContainsIgnoreCase(FilterText) ?? false) || app.Id.ToString().Contains(FilterText);
        var isJunkFiltered = !FilterJunk || app.IsJunk;
        var isHiddenFiltered = _settings.ShowHidden || !app.IsHidden;
        var isNonFavoriteFiltered = !_settings.ShowFavoritesOnly || app.IsFavorite;

        e.Accepted = isNameMatch && isJunkFiltered && isHiddenFiltered && isNonFavoriteFiltered;
    }
}
