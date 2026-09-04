using SylverInk.Interop;
using System.ComponentModel;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Interop.VisualUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private bool ShellVerbsPassed;

    public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow()
    {
        DataContext = new MainWindowViewModel();
        ViewModel.RequestSelectDatabase += SelectDatabaseTab;
        InitializeComponent();
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

    private static bool IsShuttingDown()
    {
        try
        {
            Application.Current.ShutdownMode = Application.Current.ShutdownMode;
            return false;
        }
        catch
        {
            return true;
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (IsShuttingDown()) // Prevent redundant event handling.
            return;

        if (AbortRun)
        {
            Application.Current.Shutdown();
            return;
        }

        Settings.Save();

        if (!DatabaseChanged)
        {
            switch (MessageBox.Show(Strings.ExitMessage, Strings.Title_Notification, MessageBoxButton.YesNo, MessageBoxImage.Information))
            {
                case MessageBoxResult.No:
                    e.Cancel = true;
                    return;
                case MessageBoxResult.Yes:
                    ViewModel.GridEnabled = false;
                    Application.Current.Shutdown();
                    return;
            }
        }

        switch (MessageBox.Show(Strings.ExitMessage_SaveWork, Strings.Title_Notification, MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
        {
            case MessageBoxResult.Cancel:
                e.Cancel = true;
                return;
            case MessageBoxResult.Yes:
                e.Cancel = true;
                ViewModel.GridEnabled = false;

                foreach (Database db in Databases)
                    Erase(GetLockFile(db.DBFile));

                await SaveDatabases();

                DatabaseChanged = false;
                Settings.Save();
                Application.Current.Shutdown();
                return;
            case MessageBoxResult.No:
                foreach (Database db in Databases)
                    Erase(GetLockFile(db.DBFile));

                Application.Current.Shutdown();
                return;
        }
    }

    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        MainWindowViewModel.OnSizeChanged();
    }

    private void MenuTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl control)
            return;

        if (control.SelectedItem is TabItem item && item.Tag is Database newDB && !newDB.Equals(CurrentDatabase))
        {
            CurrentDatabase = newDB;
            RecentNotesDirty = true;
            Settings.SearchResults.Clear();
            DeferUpdateRecentNotes();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        HotKeyUtils.Release();
        MutexUtils.Release();
        base.OnClosed(e);
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Hotkey registration
        HotKeyUtils.Init();

        // Database initialization
        HandleCheckInit();
        ShellVerbsPassed = MutexUtils.Init();

        if (InstanceRunning())
        {
            if (!ShellVerbsPassed)
                MessageBox.Show(Strings.Error_InstanceRunning, Strings.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);

            // If shell verbs were passed to an existing instance, close this instance silently before a head is established.
            AbortRun = true;
            Close();
            return;
        }

        // Settings initialization
        await Settings.Load();
        SettingsLoaded = true;

        // Style initialization
        SetMenuColors(this);

        // Documents subdirectory initialization
        foreach (var folder in Subfolders)
        {
            if (!Directory.Exists(folder.Value))
                Directory.CreateDirectory(folder.Value);
        }

        // (If initialization was interrupted, prevent marking it as completed)
        if (!IsShuttingDown())
            UpdatesChecked = true;

        // Perform first run operations (if needed)
        await OnFirstRun();

        // If there are no active notes from last run, open an empty note and focus it.
        if (LastActiveNotes.Count == 0)
            CreateNewNote();

        // Refresh the display
        DeferUpdateRecentNotes();

        // Check for updates. This is a blocking call, so it has to be the very last thing that we do on startup.
        Erase(UpdateHandler.UpdateLockUri);
        Erase(UpdateHandler.TempUri);
        await UpdateHandler.CheckForUpdates();
    }

    private void SelectDatabaseTab(string filePath)
    {
        // Find the tab with the matching file path and select it
        foreach (TabItem item in DatabasesPanel.Items)
        {
            if (item.Tag is Database db && Path.GetFullPath(db.DBFile) == filePath)
            {
                DatabasesPanel.SelectedItem = item;
                break;
            }
        }
    }
}
