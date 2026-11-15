namespace SylverInk.XAMLUtils;

/// <summary>
/// Static properties aiding in the display of specific data to the main application window.
/// </summary>
internal static class MainWindowUtils
{
    internal static bool CanResize { get; set; }
    internal static bool DelayVisualUpdates { get; set; }
    internal static SortType RecentEntriesSortMode { get; set; } = SortType.ByChange;
    internal static DisplayType RibbonTabContent { get; set; } = DisplayType.Content;
}
