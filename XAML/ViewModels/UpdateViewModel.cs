namespace SylverInk.XAML.ViewModels;

public class UpdateViewModel : ViewModelBase
{
    private double _progress;

    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            OnPropertyChanged();
        }
    }

    public ICommand CancelCommand { get; }

    public UpdateViewModel()
    {
        CancelCommand = new RelayCommand(Cancel);
    }

    private static void Cancel(object? param = null)
    {
        UpdateHandler.CancelUpdate();
    }
}
