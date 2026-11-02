using SylverInk.Mvvm;

namespace SylverInk.XAML.Menus;

public class HelpMenuViewModel : ViewModelBase
{
    public ICommand AboutCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    public HelpMenuViewModel()
    {
        AboutCommand = new RelayCommand(MenuShowAbout);
        CheckForUpdatesCommand = new RelayCommand(CheckForUpdates);
    }

    private static async void CheckForUpdates(object? param)
    {
        if (await UpdateHandler.CheckForUpdates(true))
            return;

        ShowTooltip(UpdateHandler.GracefulExit ? Strings.NoUpdateAvailable : Strings.FailedUpdateCheck);
    }

    private static void MenuShowAbout(object? param) => new About().Show();
}
