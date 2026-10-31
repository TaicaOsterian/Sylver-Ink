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

    public event EventHandler? RequestClose;

    public PopoutViewModel() : base()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
    }
}
