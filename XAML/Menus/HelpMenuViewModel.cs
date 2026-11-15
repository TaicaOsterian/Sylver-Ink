using SylverInk.Mvvm;

namespace SylverInk.XAML.Menus;

internal sealed class HelpMenuViewModel : ViewModelBase
{
    private About? _about;

    public ICommand AboutCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    internal HelpMenuViewModel()
    {
        AboutCommand = new RelayCommand(MenuShowAbout);
        CheckForUpdatesCommand = new RelayCommand(CheckForUpdates);
    }

    private static async void CheckForUpdates(object? param)
    {
        if (await UpdateHandler.CheckForUpdates(true))
            return;

        ShowTooltip(UpdateHandler.GracefulExit ? Strings.NoUpdateAvailable! : Strings.FailedUpdateCheck!);
    }

    private void MenuShowAbout(object? param)
    {
        _about?.Close();
        _about = new();
        _about?.Show();
    }
}
