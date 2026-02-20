using DevExpress.Mvvm.CodeGenerators;

namespace SAM.ViewModels;

[GenerateViewModel]
public partial class LibraryDataGridViewModel : LibraryViewModel
{
    [GenerateProperty] private LibraryGridSettings _settings;

    public LibraryDataGridViewModel(LibraryGridSettings settings, SteamUser user = null) : base(settings, user)
    {
        Settings = settings;

        Refresh();

        _loading = false;
    }
}
