using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Controls;

/// <summary>
/// Interaction logic for DatabaseControl.xaml
/// </summary>
public partial class DatabaseControl : UserControl
{
    public static readonly DependencyProperty DatabaseProperty =
        DependencyProperty.RegisterAttached(
            "Database",
            typeof(Database),
            typeof(DatabaseControl),
            new PropertyMetadata(null, OnDatabaseChanged));

    public double NoteListActualHeight => RecentNotesBox.ActualHeight;

    public double NoteListActualWidth => RecentNotesBox.ActualWidth;

    public DatabaseControl()
    {
        DataContext = new DatabaseControlViewModel();
        InitializeComponent();
        CreateContextMenu();
    }

    private void ContextDelete(object? sender, RoutedEventArgs e)
    {
        if (RecentSelection is null)
            return;

        if (MessageBox.Show(Strings.ConfirmDeleteNote, Strings.Title_Notification, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        CurrentDatabase.DeleteRecord(RecentSelection);
        RefreshRecentNotes();
    }

    private void ContextOpen(object? sender, RoutedEventArgs e)
    {
        if (RecentSelection is null)
            return;

        OpenQuery(RecentSelection);
    }

    private void Controller_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshRecentNotes();
    }

    private void CreateContextMenu()
    {
        ContextMenu menu = new()
        {
            DataContext = CommonUtils.Settings
        };

        MenuItem itemOpen = new()
        {
            Header = Strings.Word_Open,
        };

        MenuItem itemDelete = new()
        {
            Header = Strings.Word_Delete,
        };

        itemOpen.Click += ContextOpen;
        itemDelete.Click += ContextDelete;

        menu.Items.Add(itemOpen);
        menu.Items.Add(itemDelete);

        PlusTab.ContextMenu = menu;
    }

    public static Database GetDatabase(DependencyObject source) => (Database)source.GetValue(DatabaseProperty);

    private void ListItemChosen(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox box)
            return;

        if (box.SelectedItem is not NoteRecord record)
            return;

        RecentSelection = record;

        // We set the recent selection on any click, but only open it on a left button click. This makes it easier for the context menu to grab the affected note when needed.
        if (e.ChangedButton == MouseButton.Right)
            return;

        OpenQuery(RecentSelection);
    }

    private static void OnDatabaseChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not DatabaseControl control)
            return;

        RefreshRecentNotes();
    }

    private void RecentNotesBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var viewModel = (MainWindowViewModel)Window.GetWindow(this).DataContext;
        var dpi = VisualTreeHelper.GetDpi(this);
        viewModel.OnViewportMetricsChanged(e.NewSize.Width, e.NewSize.Height, dpi.PixelsPerInchY);
    }

    public static void SetDatabase(DependencyObject source, Database value) => source.SetValue(DatabaseProperty, value);
}
