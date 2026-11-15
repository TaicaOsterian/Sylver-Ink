using SylverInk.Mvvm;

namespace SylverInk.XAML.ViewModels;

internal sealed class AboutViewModel : ViewModelBase
{
    public string AboutUsUri { get; } = "https://github.com/TaicaOsterian/Sylver-Ink";

    public ICommand CloseCommand { get; }
    public ICommand NavigateCommand { get; }

    internal event EventHandler? RequestClose;

    internal AboutViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        NavigateCommand = new RelayCommand(Navigate);
    }

    private void Navigate(object? param)
    {
        if (param is not string uri)
            return;

        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}
