using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private int? _defaultFontIndex;

    public int? DefaultFontIndex
    {
        get => _defaultFontIndex;
        set
        {
            _defaultFontIndex = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand FontSizeChangedCommand { get; }
    public ICommand ResetCommand { get; }

    public event EventHandler? RequestClose;

    public SettingsViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        FontSizeChangedCommand = new RelayCommand(FontSizeChanged);
        ResetCommand = new RelayCommand(Reset);
    }

    private void FontSizeChanged(object? param = null)
    {
        if (param is not Button button)
            return;

        CommonUtils.Settings.MainFontSize += button.Content.Equals("+") ? 0.5 : -0.5;
    }

    private void Reset(object? param = null)
    {
        CommonUtils.Settings.AccentBackground = Brushes.PaleGoldenrod;
        CommonUtils.Settings.AccentForeground = Brushes.Blue;
        CommonUtils.Settings.ListBackground = Brushes.White;
        CommonUtils.Settings.ListForeground = Brushes.Black;
        CommonUtils.Settings.MainFontFamily = CommonUtils.Settings.DefaultFont;
        CommonUtils.Settings.MainFontSize = 11.0;
        CommonUtils.Settings.MenuBackground = Brushes.Beige;
        CommonUtils.Settings.MenuForeground = Brushes.Black;
        CommonUtils.Settings.NoteClickthrough = 0.0;
        CommonUtils.Settings.NoteTransparency = 0.0;
        RecentEntriesSortMode = SortType.ByChange;
        RibbonTabContent = DisplayType.Content;
        CommonUtils.Settings.SearchResultsOnTop = true;
        CommonUtils.Settings.SnapSearchResults = true;

        DeferUpdateRecentNotes();
    }
}
