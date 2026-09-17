using SylverInk.Interop;
using SylverInk.XAML.Views;
using System.ComponentModel;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.Visuals.VisualUtils;

namespace SylverInk;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
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
        PushViewportMetrics();
    }

    private void MenuTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl control)
            return;

        if (control.SelectedItem is not TabItem item)
            return;

        if (item.Tag is not Database newDB)
            return;

        if (newDB.Equals(CurrentDatabase))
            return;

        CurrentDatabase = newDB;
        RefreshRecentNotes();
        Settings.SearchResults.Clear();
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

        //Mutex registration
        if (MutexUtils.Init())
        {
            if (MutexUtils.ShellVerbs < 2)
                MessageBox.Show(Strings.Error_InstanceRunning, Strings.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);

            // If shell verbs were passed to an existing instance, close this instance silently before a head is established.
            AbortRun = true;
            Close();
            return;
        }

        // Database initialization
        HandleCheckInit();

        // Settings initialization
        await Settings.Load();
        SettingsLoaded = true;

        // High-contrast theme detection
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += CommonUtils.SystemPreferenceChanged;
        Settings.HighContrast = SystemParameters.HighContrast;

        // Style initialization
        SetMenuColors();

        // Hotkey registration
        HotKeyUtils.Init();

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

        // Perform secondary initialization once the settings have been loaded and the environment configured.
        await HandleFinalInit();

        // If there are no active notes from last run, open an empty note and focus it.
        if (OpenQueries.Count == 0)
            CreateNewNote();

        // Refresh the display
        PushViewportMetrics();

        // Check for updates. This is a blocking call, so it has to be the very last thing that we do on startup.
        Erase(UpdateHandler.UpdateLockUri);
        Erase(UpdateHandler.TempUri);
        await UpdateHandler.CheckForUpdates();
    }

    private void PushViewportMetrics()
    {
        if (DatabasesPanel?.SelectedItem is not TabItem { Content: DatabaseControl control })
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        ViewModel.OnViewportMetricsChanged(
            control.NoteListActualWidth,
            control.NoteListActualHeight,
            dpi.PixelsPerInchY);
    }

    private void SelectDatabaseTab(string filePath)
    {
        foreach (TabItem item in DatabasesPanel.Items)
        {
            if (item.Tag is not Database db || Path.GetFullPath(db.DBFile) != filePath)
                continue;

            DatabasesPanel.SelectedItem = item;
            break;
        }
    }
}
