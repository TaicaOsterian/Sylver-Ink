using SylverInk.Mvvm;
using System.Globalization;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Controls;

public class DatabaseControlViewModel : ViewModelBase
{
    public static string NewNoteButtonTooltip => string.Format(CultureInfo.CurrentCulture, CacheNewNoteButtonTooltip.Format, AppSettings.NewNoteHotKey);
    public static string RecentNotesTooltip => string.Format(CultureInfo.CurrentCulture, CacheRecentNotesTooltip.Format, AppSettings.PreviousNoteHotKey);
    public static string ReopenNoteButtonTooltip => string.Format(CultureInfo.CurrentCulture, CacheRecentNotesButtonTooltip.Format, AppSettings.PreviousNoteHotKey);

    public ICommand ExitCommand { get; }
    public ICommand NewNoteCommand { get; }
    public ICommand ReopenNoteCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand SettingsCommand { get; }

    public DatabaseControlViewModel()
    {
        ExitCommand = new RelayCommand(Exit);
        NewNoteCommand = new RelayCommand(NewNote);
        ReopenNoteCommand = new RelayCommand(ReopenNote, CanPopPreviousNote);
        SearchCommand = new RelayCommand(Search);
        SettingsCommand = new RelayCommand(Settings);
    }

    private static bool CanPopPreviousNote(object? param) => CurrentDatabase.GetPreviousNoteCount() > 0;

    private void Exit(object? param) => Application.Current.MainWindow.Close();

    private void NewNote(object? param) => CreateNewNote();

    private void ReopenNote(object? param) => CurrentDatabase.PopPreviousNote();

    private void Search(object? param) => SearchWindow = new();

    private void Settings(object? param) => SettingsWindow = new();
}
