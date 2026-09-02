using SylverInk.XAML.Objects;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAMLUtils;

/// <summary>
/// Static functions aiding in the display of specific data to the main application window.
/// </summary>
public static class MainWindowUtils
{
    public static bool CanResize { get; set; }
    public static bool DelayVisualUpdates { get; set; }
    public static bool RecentNotesDirty { get; set; }
    public static SortType RecentEntriesSortMode { get; set; } = SortType.ByChange;
    public static DisplayType RibbonTabContent { get; set; } = DisplayType.Change;
    public static List<TabItem> OpenTabs { get; } = [];

    public static async void DeferUpdateRecentNotes()
    {
        if (!CanResize)
            return;

        if (DelayVisualUpdates)
            return;

        DelayVisualUpdates = true;

        var panel = GetChildPanel("DatabasesPanel");

        if (panel.Dispatcher.Invoke(() => panel.FindName("RecentNotesBox")) is not ListBox RecentBox)
            return;

        try
        {
            await Task.Run(() =>
            {
                do
                {
                    WindowHeight = double.IsNaN(RecentBox.ActualHeight) ? Application.Current.MainWindow.ActualHeight : RecentBox.ActualHeight;
                    WindowWidth = double.IsNaN(RecentBox.ActualWidth) ? Application.Current.MainWindow.ActualWidth : RecentBox.ActualWidth;
                } while (WindowHeight <= 0);
            });

            await UpdateRecentNotes();

            Concurrent(UpdateDatabaseMenu);
            Concurrent(UpdateRibbonTabs);
        }
        catch
        {
            return;
        }
        finally
        {
            DelayVisualUpdates = false;
        }
    }

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
        DisplayType.Index => $"{Resources.Word_Note} #{record.Index + 1:N0} — {record.Preview}",
        _ => record.Preview
    };

    private static void UpdateDatabaseMenu()
    {
        var control = (TabControl)Application.Current.MainWindow.FindName("DatabasesPanel");
        var menu = (Menu)Application.Current.MainWindow.FindName("DatabaseMenu");

        foreach (MenuItem tab in menu.Items)
        {
            foreach (MenuItem mItem in tab.Items)
            {
                var tag = mItem.GetValue(FrameworkElement.TagProperty) ?? string.Empty;
                if (tag.Equals("Always"))
                    continue;

                var client = CurrentDatabase.Client.Active;
                var server = CurrentDatabase.Server.Active;

                var enable = tag switch
                {
                    "Connected" => client && !server,
                    "NotConnected" => !client && !server,
                    "NotServing" => !client && !server,
                    "Recents" => CommonUtils.Settings.RecentDatabases.Count > 0,
                    "Serving" => !client && server,
                    _ => control.Items.Count != 1
                };

                mItem.SetValue(UIElement.IsEnabledProperty, enable);
            }
        }
    }

    private static async Task UpdateRecentNotes()
    {
        if (Settings.MainTypeFace is null)
            return;

        Application.Current.Resources["MainFontFamily"] = Settings.MainFontFamily;
        Application.Current.Resources["MainFontSize"] = Settings.MainFontSize;

        if (RecentNotesDirty)
            Settings.RecentNotes.Clear();

        await Task.Run(() =>
        {
            var DpiInfo = VisualTreeHelper.GetDpi(Concurrent(() => Application.Current.MainWindow));
            var PixelRatio = Settings.MainFontSize * DpiInfo.PixelsPerInchY / 72.0;
            var LineHeight = PixelRatio * Settings.MainTypeFace.FontFamily.LineSpacing;
            var LineRatio = Math.Max(1.0, (WindowHeight / LineHeight) - 0.5);

            CurrentDatabase.Sort(RecentEntriesSortMode);

            while (Settings.RecentNotes.Count < LineRatio && Settings.RecentNotes.Count < CurrentDatabase.RecordCount)
            {
                var record = CurrentDatabase.GetRecord(Settings.RecentNotes.Count);
                if (record is null)
                    break;

                Concurrent(Settings.RecentNotes.Add, record);
            }

            while (Settings.RecentNotes.Count > LineRatio)
                Concurrent(Settings.RecentNotes.RemoveAt, Settings.RecentNotes.Count - 1);

            CurrentDatabase.Sort();
        });

        RecentNotesDirty = false;
    }

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
