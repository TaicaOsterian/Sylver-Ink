using SylverInk.Mvvm;

namespace SylverInk.XAML.ViewModels;

public class PopoutViewModel : ViewModelBase
{
    private bool _isFocused;

    public bool IsFocused
    {
        get => _isFocused;
        set
        {
            _isFocused = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }

    internal event EventHandler? RequestClose;

    internal PopoutViewModel() : base()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
    }
}
