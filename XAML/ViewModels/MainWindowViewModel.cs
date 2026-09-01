using static SylverInk.CommonUtils;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _addressCode = string.Empty;
    private string _addressCodeDisplay = string.Empty;
    private bool _codePopupVisible;
    private bool _connectPopupVisible;
    private bool _gridEnabled = true;
    private string _renameDatabaseName = string.Empty;
    private bool _renamePopupVisible;

    public string AddressCode
    {
        get => _addressCode;
        set
        {
            _addressCode = value;
            OnPropertyChanged();
        }
    }

    public string AddressCodeDisplay
    {
        get => _addressCodeDisplay;
        set
        {
            _addressCodeDisplay = value;
            OnPropertyChanged();
        }
    }

    public bool CodePopupVisible
    {
        get => _codePopupVisible;
        set
        {
            _codePopupVisible = value;
            OnPropertyChanged();
        }
    }

    public bool ConnectPopupVisible
    {
        get => _connectPopupVisible;
        set
        {
            _connectPopupVisible = value;
            OnPropertyChanged();
        }
    }

    public bool GridEnabled
    {
        get => _gridEnabled;
        set
        {
            _gridEnabled = value;
            OnPropertyChanged();
        }
    }

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

    public ICommand AboutCommand { get; }
    public ICommand BackupDatabaseCommand { get; }
    public ICommand CancelConnectCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand CloseCodePopupCommand { get; }
    public ICommand CloseDatabaseCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand ConnectPopupEnterCommand { get; }
    public ICommand ConnectPopupEscapeCommand { get; }
    public ICommand CopyAddressCodeCommand { get; }
    public ICommand CopyCodeCommand { get; }
    public ICommand DeleteDatabaseCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand NewDatabaseCommand { get; }
    public ICommand OpenDatabaseCommand { get; }
    public ICommand OpenRecentFileCommand { get; }
    public ICommand PropertiesCommand { get; }
    public ICommand RenameDatabaseCommand { get; }
    public ICommand RenamePopupEnterCommand { get; }
    public ICommand RenamePopupEscapeCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand SaveConnectCommand { get; }
    public ICommand SaveLocalCommand { get; }
    public ICommand SaveRenameCommand { get; }
    public ICommand ServeCommand { get; }
    public ICommand UnserveCommand { get; }

    public event Action<string>? RequestSelectDatabase;

    public MainWindowViewModel()
    {
        AboutCommand = new RelayCommand(MenuShowAbout);
        BackupDatabaseCommand = new RelayCommand(MenuBackup);
        CancelConnectCommand = new RelayCommand(_ => ConnectPopupVisible = false);
        CancelRenameCommand = new RelayCommand(_ => RenamePopupVisible = false);
        CloseCodePopupCommand = new RelayCommand(_ => CodePopupVisible = false);
        CloseDatabaseCommand = new RelayCommand(MenuClose, CanCloseDatabase);
        ConnectCommand = new RelayCommand(MenuConnect, CanConnect);
        ConnectPopupEnterCommand = new RelayCommand(PopupSaveAddress);
        ConnectPopupEscapeCommand = new RelayCommand(_ => ConnectPopupVisible = false);
        CopyAddressCodeCommand = new RelayCommand(PopupCodeClosed);
        CopyCodeCommand = new RelayCommand(MenuCopyCode, CanCopyCode);
        DeleteDatabaseCommand = new RelayCommand(MenuDelete, CanDeleteDatabase);
        DisconnectCommand = new RelayCommand(MenuDisconnect, CanDisconnect);
        NewDatabaseCommand = new RelayCommand(MenuCreate);
        OpenDatabaseCommand = new RelayCommand(MenuOpen);
        OpenRecentFileCommand = new RelayCommand(MenuOpenRecent);
        PropertiesCommand = new RelayCommand(MenuProperties);
        RenameDatabaseCommand = new RelayCommand(MenuRename);
        RenamePopupEnterCommand = new RelayCommand(PopupRenameClosed);
        RenamePopupEscapeCommand = new RelayCommand(_ => RenamePopupVisible = false);
        SaveAsCommand = new RelayCommand(MenuSaveAs);
        SaveConnectCommand = new RelayCommand(PopupSaveAddress);
        SaveLocalCommand = new RelayCommand(MenuSaveLocal);
        SaveRenameCommand = new RelayCommand(PopupRenameClosed);
        ServeCommand = new RelayCommand(MenuServe, CanServe);
        UnserveCommand = new RelayCommand(MenuUnserve, CanUnserve);
    }

    private static bool CanCloseDatabase(object? param) => Databases.Count > 1;

    private static bool CanConnect(object? param) => CurrentDatabase?.Client?.Connected is not true;

    private static bool CanCopyCode(object? param) => CurrentDatabase?.Server?.Serving is true;

    private static bool CanDeleteDatabase(object? param) => Databases.Count > 1;

    private static bool CanDisconnect(object? param) => CurrentDatabase?.Client?.Connected is true;

    private static bool CanServe(object? param) => CurrentDatabase?.Server?.Serving is not true;

    private static bool CanUnserve(object? param) => CurrentDatabase?.Server?.Serving is true;

    private static void CopyCode()
    {
        try
        {
            Clipboard.SetText(CurrentDatabase.Server?.AddressCode);
        }
        catch
        {
            ShowTooltip("Failed to copy the address code to the clipboard");
        }
    }

    private static void MenuBackup(object? param) => CurrentDatabase.MakeBackup();

    private static void MenuClose(object? param)
    {
        if (CurrentDatabase.Changed)
        {
            var res = MessageBox.Show("Do you want to save your changes?", "Sylver Ink: Notification", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Cancel)
                return;
            if (res == MessageBoxResult.Yes)
                CurrentDatabase.Save();
        }

        RemoveDatabase(CurrentDatabase);
        DeferUpdateRecentNotes();
    }

    private void MenuConnect(object? param)
    {
        ConnectPopupVisible = true;
        AddressCode = string.Empty;
    }

    private static void MenuCopyCode(object? param)
    {
        CopyCode();
    }

    private static void MenuCreate(object? param)
    {
        AddDatabase(new Database());
        DeferUpdateRecentNotes();
    }

    private static void MenuDelete(object? param)
    {
        if (MessageBox.Show("Are you sure you want to permanently delete this database?", "Sylver Ink: Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        Erase(CurrentDatabase.DBFile);
        var BKPath = Path.GetDirectoryName(GetBackupPath(CurrentDatabase));
        if (Directory.Exists(BKPath))
            Directory.Delete(BKPath, true);

        RemoveDatabase(CurrentDatabase);
        DeferUpdateRecentNotes();
    }

    private static void MenuDisconnect(object? param)
    {
        CurrentDatabase.Client.Disconnect();
        CurrentDatabase.Changed = true;
    }

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
        DeferUpdateRecentNotes();
    }

    private async void MenuOpenRecent(object? param)
    {
        if (param is not string dbFile)
            return;

        var path = Path.GetFullPath(dbFile);

        if (DatabaseFiles.Contains(path))
        {
            RequestSelectDatabase?.Invoke(path);
            return;
        }

        if (!Path.Exists(path))
        {
            if (MessageBox.Show($"The file {path} has been either moved or deleted.\n\nDo you want to remove it from the list?", $"Sylver Ink: Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                CommonUtils.Settings.RecentDatabases.Remove(new() { FullPath = path });
                DeferUpdateRecentNotes();
            }

            return;
        }

        await Database.Create(dbFile);
        DeferUpdateRecentNotes();
    }

    private static void MenuProperties(object? param)
    {
        new Properties().Show();
    }

    private void MenuRename(object? param)
    {
        RenameDatabaseName = CurrentDatabase.Name ?? string.Empty;
        RenamePopupVisible = true;
    }

    private static void MenuSaveAs(object? param)
    {
        var newPath = DialogFileSelect(true, 2, CurrentDatabase.Name);
        if (!string.IsNullOrWhiteSpace(newPath))
            CurrentDatabase.DBFile = newPath;
        CurrentDatabase.Format = HighestSIDBFormat;
    }

    private static void MenuSaveLocal(object? param)
    {
        CurrentDatabase.Changed = true;
        CurrentDatabase.DBFile = Path.Join(Subfolders["Databases"], Path.GetFileNameWithoutExtension(CurrentDatabase.DBFile), Path.GetFileName(CurrentDatabase.DBFile));
        CurrentDatabase.Format = HighestSIDBFormat;
        CurrentDatabase.Save();
    }

    private static void MenuServe(object? param) => CurrentDatabase.Server.Serve(0);

    private static void MenuShowAbout(object? param) => new About().Show();

    private static void MenuUnserve(object? param) => CurrentDatabase.Server.Close();

    public static void OnSizeChanged()
    {
        DeferUpdateRecentNotes();
    }

    private void PopupCodeClosed(object? param)
    {
        if (CurrentDatabase == null)
            return;

        CopyCode();
        CodePopupVisible = false;
    }

    private void PopupRenameClosed(object? param)
    {
        if (CurrentDatabase == null)
            return;

        if (string.IsNullOrWhiteSpace(RenameDatabaseName))
            return;

        if (RenameDatabaseName.Equals(CurrentDatabase.Name, StringComparison.Ordinal))
            return;

        foreach (Database db in Databases)
        {
            if (!RenameDatabaseName.Equals(db.Name, StringComparison.Ordinal))
                continue;

            MessageBox.Show("A database already exists with the provided name.", "Sylver Ink: Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (char pc in InvalidPathChars)
        {
            if (!RenameDatabaseName.Contains(pc))
                continue;

            MessageBox.Show($"Provided name contains invalid character: {pc}", "Sylver Ink: Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        CurrentDatabase.Rename(RenameDatabaseName);
        RenamePopupVisible = false;
    }

    private async void PopupSaveAddress(object? param)
    {
        ConnectPopupVisible = false;

        if (string.IsNullOrWhiteSpace(AddressCode) || AddressCode.Length != 6)
        {
            MessageBox.Show("Invalid address code. Must be 6 characters.", "Sylver Ink: Error", MessageBoxButton.OK);
            return;
        }

        Database newDB = new();
        AddDatabase(newDB);
        await newDB.Client.Connect(AddressCode);
    }
}