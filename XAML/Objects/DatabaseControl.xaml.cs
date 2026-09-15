using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Objects;

/// <summary>
/// Interaction logic for DatabaseControl.xaml
/// </summary>
public partial class DatabaseControl : UserControl
{
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
    }

    private void ContextOpen(object? sender, RoutedEventArgs e)
    {
        if (RecentSelection is null)
            return;

        OpenQuery(RecentSelection);
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

    private void RecentNotesBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var viewModel = (MainWindowViewModel)Window.GetWindow(this).DataContext;
        var dpi = VisualTreeHelper.GetDpi(this);
        viewModel.OnViewportMetricsChanged(e.NewSize.Width, e.NewSize.Height, dpi.PixelsPerInchY);
    }
}
