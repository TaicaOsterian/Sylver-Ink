using SylverInk.Notes;
using SylverInk.XAMLUtils;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
        AboutCommand = new RelayCommand(_ => MenuShowAbout());
        BackupDatabaseCommand = new RelayCommand(_ => MenuBackup());
        CancelConnectCommand = new RelayCommand(_ => ConnectPopupVisible = false);
        CancelRenameCommand = new RelayCommand(_ => RenamePopupVisible = false);
        CloseCodePopupCommand = new RelayCommand(_ => CodePopupVisible = false);
        CloseDatabaseCommand = new RelayCommand(_ => MenuClose(), _ => CanCloseDatabase());
        ConnectCommand = new RelayCommand(_ => MenuConnect(), _ => CanConnect());
        ConnectPopupEnterCommand = new RelayCommand(_ => PopupSaveAddress());
        ConnectPopupEscapeCommand = new RelayCommand(_ => ConnectPopupVisible = false);
        CopyAddressCodeCommand = new RelayCommand(_ => PopupCodeClosed());
        CopyCodeCommand = new RelayCommand(_ => MenuCopyCode(), _ => CanCopyCode());
        DeleteDatabaseCommand = new RelayCommand(_ => MenuDelete(), _ => CanDeleteDatabase());
        DisconnectCommand = new RelayCommand(_ => MenuDisconnect(), _ => CanDisconnect());
        NewDatabaseCommand = new RelayCommand(_ => MenuCreate());
        OpenDatabaseCommand = new RelayCommand(_ => MenuOpen());
        PropertiesCommand = new RelayCommand(_ => MenuProperties());
        RenameDatabaseCommand = new RelayCommand(_ => MenuRename());
        RenamePopupEnterCommand = new RelayCommand(_ => PopupRenameClosed());
        RenamePopupEscapeCommand = new RelayCommand(_ => RenamePopupVisible = false);
        SaveAsCommand = new RelayCommand(_ => MenuSaveAs());
        SaveConnectCommand = new RelayCommand(_ => PopupSaveAddress());
        SaveLocalCommand = new RelayCommand(_ => MenuSaveLocal());
        SaveRenameCommand = new RelayCommand(_ => PopupRenameClosed());
        ServeCommand = new RelayCommand(_ => MenuServe(), _ => CanServe());
        UnserveCommand = new RelayCommand(_ => MenuUnserve(), _ => CanUnserve());
    }

    private static bool CanCloseDatabase() => Databases.Count > 1;

    private static bool CanConnect() => CurrentDatabase?.Client?.Connected is not true;

    private static bool CanCopyCode() => CurrentDatabase?.Server?.Serving is true;

    private static bool CanDeleteDatabase() => Databases.Count > 1;

    private static bool CanDisconnect() => CurrentDatabase?.Client?.Connected is true;

    private static bool CanServe() => CurrentDatabase?.Server?.Serving is not true;

    private static bool CanUnserve() => CurrentDatabase?.Server?.Serving is true;

    private static void MenuBackup() => CurrentDatabase.MakeBackup();

    private static void MenuClose()
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

    private void MenuConnect()
    {
        ConnectPopupVisible = true;
        AddressCode = string.Empty;
    }

    private static void MenuCopyCode() => Clipboard.SetText(CurrentDatabase.Server?.AddressCode);

    private static void MenuCreate()
    {
        AddDatabase(new Database());
        DeferUpdateRecentNotes();
    }

    private static void MenuDelete()
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

    private static void MenuDisconnect()
    {
        CurrentDatabase.Client.Disconnect();
        CurrentDatabase.Changed = true;
    }

    private void MenuOpen()
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

        Database.Create(dbFile).Wait();
        DeferUpdateRecentNotes();
    }

    private static void MenuProperties()
    {
        new Properties().Show();
    }

    private void MenuRename()
    {
        RenameDatabaseName = CurrentDatabase.Name ?? string.Empty;
        RenamePopupVisible = true;
    }

    private static void MenuSaveAs()
    {
        var newPath = DialogFileSelect(true, 2, CurrentDatabase.Name);
        if (!string.IsNullOrWhiteSpace(newPath))
            CurrentDatabase.DBFile = newPath;
        CurrentDatabase.Format = HighestSIDBFormat;
    }

    private static void MenuSaveLocal()
    {
        CurrentDatabase.Changed = true;
        CurrentDatabase.DBFile = Path.Join(Subfolders["Databases"], Path.GetFileNameWithoutExtension(CurrentDatabase.DBFile), Path.GetFileName(CurrentDatabase.DBFile));
        CurrentDatabase.Format = HighestSIDBFormat;
        CurrentDatabase.Save();
    }

    private static void MenuServe() => CurrentDatabase.Server.Serve(0);

    private static void MenuShowAbout() => new About().Show();

    private static void MenuUnserve() => CurrentDatabase.Server.Close();

    public static void OnSizeChanged()
    {
        DeferUpdateRecentNotes();
    }

    private void PopupRenameClosed()
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

    private async void PopupSaveAddress()
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

    private void PopupCodeClosed()
    {
        if (CurrentDatabase == null)
            return;

        Clipboard.SetText(CurrentDatabase.Server?.AddressCode);
        CodePopupVisible = false;
    }
}