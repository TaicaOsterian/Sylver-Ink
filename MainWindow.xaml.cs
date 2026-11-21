using SylverInk.Interop;
using SylverInk.Visuals;
using SylverInk.XAML.Controls;
using System.ComponentModel;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.Visuals.VisualUtils;

namespace SylverInk;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool IsShuttingDown;

    internal MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    internal MainWindow()
    {
        DataContext = new MainWindowViewModel();
        ViewModel.FileMenu.RequestSelectDatabase += SelectDatabaseTab;
        InitializeComponent();
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (IsShuttingDown) // Prevent redundant event handling.
            return;

        if (AbortRun)
        {
            Shutdown();
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
                    Settings.GridEnabled = false;

                    foreach (Database db in Databases)
                    {
                        if (!File.Exists(db.DBFile))
                            db.Save();
                    }

                    Shutdown();
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
                Settings.GridEnabled = false;

                foreach (Database db in Databases)
                    Erase(GetLockFile(db.DBFile));

                await SaveDatabases();

                DatabaseChanged = false;
                Settings.Save();
                Shutdown();
                return;
            case MessageBoxResult.No:
                foreach (Database db in Databases)
                    Erase(GetLockFile(db.DBFile));

                Shutdown();
                return;
        }
    }

    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e) => PushViewportMetrics();

    private void MenuTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl control)
            return;

        if (control.SelectedItem is not TabItem item)
            return;

        if (item.Content is not DatabaseControl dbControl)
            return;

        if (DatabaseControl.GetDatabase(dbControl) is not Database newDB)
            return;

        if (newDB.Equals(CurrentDatabase))
            return;

        CurrentDatabase = newDB;
        Settings.SearchResults.Clear();
        PushViewportMetrics();
        RefreshRecentNotes();
    }

    protected override void OnClosed(EventArgs e)
    {
        SavePaths();
        HotKeyUtils.Release();
        MutexUtils.Release();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        base.OnClosed(e);
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Clear error logs from previous session.
        Erase(ErrorFile);

        //Mutex registration
        if (await MutexUtils.Init())
        {
            if (MutexUtils.ShellVerbs < 2)
                MessageBox.Show(Strings.Error_InstanceRunning, Strings.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);

            // If shell verbs were passed to an existing instance, close this instance silently before a head is established.
            AbortRun = true;
            Close();
            return;
        }

        // Path initialization
        LoadPaths();

        // Database initialization
        HandleCheckInit();

        // Settings initialization
        await Settings.Load();
        SettingsLoaded = true;

        // High-contrast theme detection
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
        if (SystemParameters.HighContrast && !Settings.HighContrast)
        {
            // If the user didn't previously have high contrast enabled, but the system reports that they now do, override existing theme settings and switch to high contrast.
            Settings.HighContrast = SystemParameters.HighContrast;
            SwitchTheme(Themes.HighContrast);
        }

        // Hotkey registration
        HotKeyUtils.Init();

        // Documents subdirectory initialization
        foreach (var folder in Subfolders)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folder.Value))
                    Directory.CreateDirectory(folder.Value);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
            }
        }

        // (If initialization was interrupted, prevent marking it as completed)
        if (!IsShuttingDown)
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
        Erase(SessionFile);
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
            if (item.Content is not DatabaseControl dbControl)
                continue;

            if (DatabaseControl.GetDatabase(dbControl) is not Database db)
                continue;

            if (Path.GetFullPath(db.DBFile) != filePath)
                continue;

            DatabasesPanel.SelectedItem = item;
            RefreshRecentNotes();
            break;
        }
    }

    private void Shutdown()
    {
        IsShuttingDown = true;
        Concurrent(Application.Current.Shutdown);
    }
}
