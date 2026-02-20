using System.Windows;
using System.Windows.Media;
using DevExpress.Mvvm;
using SAM.Core.Messages;
using SAM.ViewModels;

namespace SAM.Views;

public partial class LibraryItemsControlView
{
    public LibraryItemsControlView()
    {
        InitializeComponent();

        Messenger.Default.Register<ActionMessage>(this, OnActionMessage);
        Loaded += (_, _) => UpdateAllTilesGlobalShowImages();
    }

    private void OnActionMessage(ActionMessage msg)
    {
        if (msg.EntityType == EntityType.HomeSettings && msg.ActionType == ActionType.Changed)
        {
            UpdateAllTilesGlobalShowImages();
        }
    }

    private void UpdateAllTilesGlobalShowImages()
    {
        try
        {
            var vm = DataContext as LibraryTileViewModel;
            if (vm == null || vm.Settings == null) return;

            var desired = vm.Settings.ShowImages;

            // Walk the visual tree to find AppTileButton instances and update their GlobalShowImages
            void Walk(DependencyObject parent)
            {
                if (parent == null) return;
                var count = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    if (child is SAM.Controls.AppTileButton atb)
                    {
                        atb.GlobalShowImages = desired;
                    }
                    Walk(child);
                }
            }

            Walk(this);
        }
        catch
        {
            // best-effort
        }
    }
}
