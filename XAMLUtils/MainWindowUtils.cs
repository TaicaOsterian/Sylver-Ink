using SylverInk.XAML.Views;

namespace SylverInk.XAMLUtils;

/// <summary>
/// Static functions and properties aiding in the display of specific data to the main application window.
/// </summary>
public static class MainWindowUtils
{
    public static bool CanResize { get; set; }
    public static bool DelayVisualUpdates { get; set; }
    public static List<TabItem> OpenTabs { get; } = [];
    public static SortType RecentEntriesSortMode { get; set; } = SortType.ByChange;
    public static DisplayType RibbonTabContent { get; set; } = DisplayType.Change;

    public static TabControl GetChildPanel(string basePanel) => Concurrent(() =>
    {
        var db = (TabControl)Application.Current.MainWindow.FindName(basePanel);
        var dbItem = (TabItem)db.SelectedItem;
        return (TabControl)((DatabaseControl)dbItem.Content).Content;
    });

    public static void UpdateRibbonTabs()
    {
        foreach (var item in OpenTabs)
        {
            if (item.Content is not NoteTab tab)
                continue;

            item.Header = tab.ViewModel.Record.GetRibbonHeader();
        }
    }
}
