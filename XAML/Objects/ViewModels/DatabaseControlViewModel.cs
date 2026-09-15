using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Objects.ViewModels;

public class DatabaseControlViewModel : ViewModelBase
{
    public ICommand ExitCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand NewNoteCommand { get; }
    public ICommand ReopenNoteCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand SettingsCommand { get; }

    public DatabaseControlViewModel()
    {
        ExitCommand = new RelayCommand(Exit);
        ImportCommand = new RelayCommand(Import);
        NewNoteCommand = new RelayCommand(NewNote);
        ReopenNoteCommand = new RelayCommand(ReopenNote, CanPopPreviousNote);
        SearchCommand = new RelayCommand(Search);
        SettingsCommand = new RelayCommand(Settings);
    }

    private static bool CanPopPreviousNote(object? param) => CurrentDatabase.GetPreviousNoteCount() > 0;

    private void Exit(object? param) => Application.Current.MainWindow.Close();

    private void Import(object? param) => ImportWindow = new();

    private void NewNote(object? param) => CreateNewNote();

    private void ReopenNote(object? param) => CurrentDatabase.PopPreviousNote();

    private void Search(object? param) => SearchWindow = new();

    private void Settings(object? param) => SettingsWindow = new();
}
