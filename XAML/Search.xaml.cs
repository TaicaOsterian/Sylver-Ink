using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Search.xaml
/// </summary>
public partial class Search : Window
{
    public string Query { get; private set; } = string.Empty;
    public SearchViewModel ViewModel => (SearchViewModel)DataContext;

    public Search()
    {
        DataContext = new SearchViewModel();
        ViewModel.RequestClose += (_, _) => Close();
        InitializeComponent();
        CreateContextMenu();
        ViewModel.QueryCommand.Execute(true);
    }

    private void ContextDelete(object? sender, RoutedEventArgs e)
    {
        if (RecentSelection is null)
            return;

        if (MessageBox.Show(Localization.Resources.ConfirmDeleteNote, Localization.Resources.Title_Notification, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        CurrentDatabase.DeleteRecord(RecentSelection);

        return;
    }

    private void ContextOpen(object? sender, RoutedEventArgs e)
    {
        if (RecentSelection is null)
            return;

        OpenQuery(RecentSelection);

        return;
    }
    private void CreateContextMenu()
    {
        ContextMenu menu = new()
        {
            DataContext = CommonUtils.Settings
        };

        MenuItem itemOpen = new()
        {
            Header = Localization.Resources.Word_Open,
        };

        MenuItem itemDelete = new()
        {
            Header = Localization.Resources.Word_Delete,
        };

        itemOpen.Click += ContextOpen;
        itemDelete.Click += ContextDelete;

        menu.Items.Add(itemOpen);
        menu.Items.Add(itemDelete);

        Results.ContextMenu = menu;
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

    private void ListItemChosen(object? sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox box)
            return;

        if (box.SelectedItem is not NoteRecord record)
            return;

        RecentSelection = record;

        if (e.ChangedButton == MouseButton.Right)
            return;

        OpenQuery(record)?.ViewModel.ScrollToText(Query);
    }

    private void OnClose(object? sender, EventArgs e)
    {
        CommonUtils.Settings.SearchResults.Clear();
    }
}
