using SylverInk.XAML.Objects;
using System.Globalization;

namespace SylverInk.XAMLUtils;

/// <summary>
/// Static functions aiding in the display of specific data to the main application window.
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

    public static Label GetRibbonHeader(NoteRecord record)
    {
        var tooltip = GetRibbonTooltip(record);
        var content = tooltip;

        if (content.Contains(Environment.NewLine))
            content = content[..content.IndexOf(Environment.NewLine, StringComparison.OrdinalIgnoreCase)];

        if (content.Length >= 13)
            content = $"{content[..10]}...";

        return new()
        {
            Content = content,
            Margin = new(0, -4, 0, 0),
            ToolTip = tooltip[..Math.Min(40, tooltip.Length)]
        };
    }

    private static string GetRibbonTooltip(NoteRecord record) => RibbonTabContent switch
    {
        DisplayType.Change => $"{record.ShortChange} — {record.Preview}",
        DisplayType.Content => record.Preview,
        DisplayType.Creation => $"{record.GetCreated()} — {record.Preview}",
        DisplayType.Index => string.Format(CultureInfo.CurrentCulture, CacheNoteIndexLabel, record.Index + 1, record.Preview),
        _ => record.Preview
    };

    public static void UpdateRibbonTabs()
    {
        foreach (var item in OpenTabs)
        {
            if (item.Content is not NoteTab tab)
                continue;

            item.Header = GetRibbonHeader(tab.ViewModel.Record);
        }
    }
}
