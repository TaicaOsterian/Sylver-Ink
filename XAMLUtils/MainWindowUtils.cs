namespace SylverInk.XAMLUtils;

/// <summary>
/// Static properties aiding in the display of specific data to the main application window.
/// </summary>
public static class MainWindowUtils
{
    public static bool CanResize { get; set; }
    public static bool DelayVisualUpdates { get; set; }
    public static SortType RecentEntriesSortMode { get; set; } = SortType.ByChange;
    public static DisplayType RibbonTabContent { get; set; } = DisplayType.Content;
}
