using SylverInk.Mvvm;
using System.Globalization;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Menus;

public class FileMenuViewModel : ViewModelBase
{
    public ICommand BrowseFolderCommand { get; }
    public ICommand DeleteDatabaseCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand NewDatabaseCommand { get; }
    public ICommand OpenDatabaseCommand { get; }
    public ICommand OpenRecentFileCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand SaveLocalCommand { get; }

    public event Action<string>? RequestSelectDatabase;

    public FileMenuViewModel()
    {
        BrowseFolderCommand = new RelayCommand(MenuBrowseFolder);
        DeleteDatabaseCommand = new RelayCommand(MenuDelete, CanDeleteDatabase);
        ImportCommand = new RelayCommand(MenuImport);
        NewDatabaseCommand = new RelayCommand(MenuCreate);
        OpenDatabaseCommand = new RelayCommand(MenuOpen);
        OpenRecentFileCommand = new RelayCommand(MenuOpenRecent, CanOpenRecent);
        SaveAsCommand = new RelayCommand(MenuSaveAs);
        SaveLocalCommand = new RelayCommand(MenuSaveLocal, CanSaveLocal);
    }

    private static bool CanDeleteDatabase(object? param) => Databases.Count > 1;

    private static bool CanOpenRecent(object? param) => CommonUtils.Settings.RecentDatabases.Count > 0;

    private static bool CanSaveLocal(object? param) => !CurrentDatabase.DBFile.Contains(Subfolders[Subfolder.Databases]);

    private static void MenuBrowseFolder(object? param)
    {
        if (param is not string folder)
            return;

        var target = folder switch
        {
            _ when folder.Equals(Strings.Subfolder_Backups, StringComparison.Ordinal) => Subfolders[Subfolder.Backups],
            _ when folder.Equals(Strings.Subfolder_Databases, StringComparison.Ordinal) => Subfolders[Subfolder.Databases],
            _ => string.Empty
        };

        if (!Directory.Exists(target))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
            Verb = "open"
        });
    }

    private static void MenuCreate(object? param)
    {
        AddDatabase(new Database());
        RefreshRecentNotes();
    }

    private static void MenuDelete(object? param)
    {
        if (MessageBox.Show(Strings.Message_ConfirmDeleteDatabase, Strings.Title_Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        Erase(CurrentDatabase.DBFile);
        RemoveDatabase(CurrentDatabase);
        RefreshRecentNotes();
    }

    private static void MenuImport(object? param) => ImportWindow = new();

    private async void MenuOpen(object? param)
    {
        string dbFile = DialogFileSelect(filterIndex: 2);
        if (string.IsNullOrWhiteSpace(dbFile))
            return;

        var path = Path.GetFullPath(dbFile);

        if (DatabaseFiles.Contains(path))
        {
            RequestSelectDatabase?.Invoke(path);
            return;
        }

        await Database.Create(dbFile);
        RefreshRecentNotes();
    }

    private async void MenuOpenRecent(object? param)
    {
        if (param is not string dbFile)
            return;

        var path = Path.GetFullPath(dbFile);

        if (DatabaseFiles.Contains(path))
        {
            RequestSelectDatabase?.Invoke(path);
            RefreshRecentNotes();
            return;
        }

        if (Path.Exists(path))
        {
            await Database.Create(dbFile);
            RefreshRecentNotes();
            return;
        }

        if (MessageBox.Show(string.Format(CultureInfo.CurrentCulture, CacheMessageFileMovedOrDeleted, path), Strings.Title_Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        CommonUtils.Settings.RecentDatabases.Remove(new() { FullPath = path });
    }

    private static void MenuSaveAs(object? param)
    {
        var newPath = DialogFileSelect(true, 2, CurrentDatabase.Name);
        if (!string.IsNullOrWhiteSpace(newPath))
            CurrentDatabase.DBFile = newPath;
        CurrentDatabase.Format = HighestSIDBFormat;
        CurrentDatabase.Save();
    }

    private static void MenuSaveLocal(object? param)
    {
        CurrentDatabase.Changed = true;
        CurrentDatabase.DBFile = Path.Join(Subfolders[Subfolder.Databases], Path.GetFileNameWithoutExtension(CurrentDatabase.DBFile), Path.GetFileName(CurrentDatabase.DBFile));
        CurrentDatabase.Format = HighestSIDBFormat;
        CurrentDatabase.Save();
    }
}
