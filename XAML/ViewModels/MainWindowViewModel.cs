using SylverInk.Mvvm;
using SylverInk.XAML.Menus;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private double _pixelsPerInchY = 96.0;
    private int _refreshToken;
    private double _viewportHeight;
    private double _viewportWidth;

    public FileMenuViewModel FileMenu { get; private set; }
    public DatabaseMenuViewModel DatabaseMenu { get; private set; }
    public NetworkMenuViewModel NetworkMenu { get; private set; }
    public HelpMenuViewModel HelpMenu { get; private set; }

    public MainWindowViewModel()
    {
        FileMenu = new();
        DatabaseMenu = new();
        NetworkMenu = new();
        HelpMenu = new();
    }

    private static List<NoteRecord> BuildRecentNotesSnapshot(double viewportHeight, double pixelsPerInchY)
    {
        var pixelRatio = CommonUtils.Settings.MainFontSize * pixelsPerInchY / 72.0;
        var typeface = CommonUtils.Settings.MainTypeFace!;
        var lineHeight = pixelRatio * typeface.FontFamily.LineSpacing;
        var lineRatio = Math.Max(1.0, (viewportHeight / lineHeight) - 0.5);
        var target = (int)Math.Min(lineRatio, CurrentDatabase.RecordCount);

        if (target <= 0)
            return [];

        var records = CurrentDatabase.Sort(RecentEntriesSortMode);
        var result = new List<NoteRecord>(target);
        for (int i = 0; i < target; i++)
            result.Add(records[i]);

        return result;
    }

    public void OnViewportMetricsChanged(double width, double height, double pixelsPerInchY)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        _pixelsPerInchY = pixelsPerInchY;
        RefreshRecentNotes();
    }

    public async Task RefreshRecentNotesAsync()
    {
        if (!CanResize || CommonUtils.Settings.MainTypeFace is null || _viewportHeight <= 0)
            return;

        int token = ++_refreshToken;
        double height = _viewportHeight;
        double dpiY = _pixelsPerInchY;

        IReadOnlyList<NoteRecord> snapshot;
        try
        {
            snapshot = await Task.Run(() => BuildRecentNotesSnapshot(height, dpiY));
        }
        catch (Exception e)
        {
            App.LogException(e);
            return;
        }

        if (token != _refreshToken)
            return; // Superseded by a newer request

        CommonUtils.Settings.RecentNotes = snapshot;

        Concurrent(UpdateRibbonTabs);
    }
}
