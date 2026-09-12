using SylverInk.XAML.Objects;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private int? _defaultFontIndex;
    private bool _smartAssistChecked = true;

    public int? DefaultFontIndex
    {
        get => _defaultFontIndex;
        set
        {
            _defaultFontIndex = value;
            OnPropertyChanged();
        }
    }

    public bool SmartAssistChecked
    {
        get => _smartAssistChecked;
        set
        {
            _smartAssistChecked = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand FontSizeChangedCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SmartAssistCommand { get; }

    public event EventHandler? RequestClose;

    public SettingsViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        FontSizeChangedCommand = new RelayCommand(FontSizeChanged);
        ResetCommand = new RelayCommand(Reset);
        SmartAssistCommand = new RelayCommand(SmartAssistChanged);
    }

    private void FontSizeChanged(object? param = null)
    {
        if (param is not Button button)
            return;

        CommonUtils.Settings.MainFontSize += button.Content.Equals("+") ? 0.5 : -0.5;
    }

    private void Reset(object? param = null)
    {
        using (new ContextFreeze(CommonUtils.Settings))
        {
            var theme = CommonUtils.Settings.HighContrast ? Themes.HighContrast : CommonUtils.Settings.SelectedTheme;

            CommonUtils.Settings.AccentBackground = theme.AccentBackground;
            CommonUtils.Settings.AccentForeground = theme.AccentForeground;
            CommonUtils.Settings.ListBackground = theme.ListBackground;
            CommonUtils.Settings.ListForeground = theme.ListForeground;
            CommonUtils.Settings.MainFontFamily = CommonUtils.Settings.DefaultFont;
            CommonUtils.Settings.MainFontSize = 11.0;
            CommonUtils.Settings.MenuBackground = theme.MenuBackground;
            CommonUtils.Settings.MenuForeground = theme.MenuForeground;
            CommonUtils.Settings.NoteClickthrough = 0.0;
            CommonUtils.Settings.NoteTransparency = 0.0;
            RecentEntriesSortMode = SortType.ByChange;
            RibbonTabContent = DisplayType.Content;
            CommonUtils.Settings.SearchResultsOnTop = true;
            CommonUtils.Settings.SnapSearchResults = true;
        }

        DeferUpdateRecentNotes();
    }

    private void SmartAssistChanged(object? param = null)
    {
        CommonUtils.Settings.HighContrastAssist = SmartAssistChecked;
    }
}
