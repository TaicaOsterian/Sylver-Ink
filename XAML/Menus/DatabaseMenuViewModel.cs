using SylverInk.Mvvm;
using System.Globalization;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Menus;

internal sealed class DatabaseMenuViewModel : ViewModelBase
{
    private Properties? _properties;
    private string _renameDatabaseName = string.Empty;
    private bool _renamePopupVisible;
    private Trim? _trim;

    public string RenameDatabaseName
    {
        get => _renameDatabaseName;
        set
        {
            _renameDatabaseName = value;
            OnPropertyChanged();
        }
    }

    public bool RenamePopupVisible
    {
        get => _renamePopupVisible;
        set
        {
            _renamePopupVisible = value;
            OnPropertyChanged();
        }
    }

    public ICommand BackupDatabaseCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand CloseDatabaseCommand { get; }
    public ICommand PropertiesCommand { get; }
    public ICommand RenameDatabaseCommand { get; }
    public ICommand RenamePopupEnterCommand { get; }
    public ICommand RenamePopupEscapeCommand { get; }
    public ICommand SaveRenameCommand { get; }
    public ICommand TrimDatabaseCommand { get; }

    internal DatabaseMenuViewModel()
    {
        BackupDatabaseCommand = new RelayCommand(MenuBackup);
        CancelRenameCommand = new RelayCommand(_ => RenamePopupVisible = false);
        CloseDatabaseCommand = new RelayCommand(MenuClose, CanCloseDatabase);
        PropertiesCommand = new RelayCommand(MenuProperties);
        RenameDatabaseCommand = new RelayCommand(MenuRename);
        RenamePopupEnterCommand = new RelayCommand(PopupRenameClosed);
        RenamePopupEscapeCommand = new RelayCommand(_ => RenamePopupVisible = false);
        SaveRenameCommand = new RelayCommand(PopupRenameClosed);
        TrimDatabaseCommand = new RelayCommand(MenuTrim);
    }

    private static bool CanCloseDatabase(object? param) => Databases.Count > 1;

    private static void MenuBackup(object? param) => CurrentDatabase.MakeBackup();

    private static void MenuClose(object? param)
    {
        if (CurrentDatabase.Changed)
        {
            var res = MessageBox.Show(Strings.ExitMessage_SaveWork, Strings.Title_Notification, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Cancel)
                return;
            if (res == MessageBoxResult.Yes)
                CurrentDatabase.Save();
        }

        RemoveDatabase(CurrentDatabase);
        RefreshRecentNotes();
    }

    private void MenuProperties(object? param)
    {
        _properties?.Close();
        _properties = new();
        _properties.Show();
    }

    private void MenuRename(object? param)
    {
        RenameDatabaseName = CurrentDatabase.Name ?? string.Empty;
        RenamePopupVisible = true;
    }

    private void MenuTrim(object? param)
    {
        _trim?.Close();
        _trim = new();
        _trim.Show();
    }

    private void PopupRenameClosed(object? param)
    {
        if (CurrentDatabase == null)
            return;

        if (string.IsNullOrWhiteSpace(RenameDatabaseName))
            return;

        if (RenameDatabaseName.Equals(CurrentDatabase.Name, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (Database db in Databases)
        {
            if (!RenameDatabaseName.Equals(db.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            ShowTooltip(Strings.Message_DatabaseExists!);
            return;
        }

        foreach (char pc in InvalidPathChars)
        {
            if (!RenameDatabaseName.Contains(pc))
                continue;

            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, CacheMessageInvalidPath, pc), Strings.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        CurrentDatabase.Rename(RenameDatabaseName);
        RenamePopupVisible = false;
        RefreshRecentNotes();
    }
}
