using SylverInk.XAML;
using SylverInk.XAML.Objects;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Interop.VisualUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk;

/// <summary>
/// For values which must be cleanly overwritten on a major properties sweep, ContextFreeze prevents cascading behavior.
/// </summary>
public readonly ref struct ContextFreeze
{
    private readonly ContextSettings _context;
    private readonly bool _highContrastAssist;

    public ContextFreeze(ContextSettings context)
    {
        _context = context;

        _highContrastAssist = context.HighContrastAssist;
        _context.HighContrastAssist = false;
    }

    public void Dispose()
    {
        _context.HighContrastAssist = _highContrastAssist;
    }
}

public class ContextSettings : ViewModelBase
{
    private Brush? _accentBackground = Themes.Default.AccentBackground;
    private Brush? _accentForeground = Themes.Default.AccentForeground;
    private bool _firstRun = true;
    private bool _highContrast;
    private bool _highContrastAssist = true;
    private int _lineTolerance;
    private Brush? _listBackground = Themes.Default.ListBackground;
    private Brush? _listForeground = Themes.Default.ListForeground;
    private FontFamily? _mainFontFamily = Fonts.SystemFontFamilies.ElementAt(0); // This is unclean, but a proper default font will be selected before the head is established.
    private double _mainFontSize = 11.0;
    private Typeface? _mainTypeFace;
    private Brush? _menuBackground = Themes.Default.MenuBackground;
    private Brush? _menuForeground = Themes.Default.MenuForeground;
    private double _noteClickthrough;
    private double _noteClickthroughInverse = 4.0;
    private double _noteTransparency;
    private bool _queryAllDatabases;
    private bool _searchResultsOnTop = true;
    private bool _searchResultsInTaskbar;
    private Theme _selectedTheme = Themes.Default;
    private bool _snapSearchResults = true;

    public Brush? AccentBackground
    {
        get => _accentBackground;
        set
        {
            _accentBackground = value;

            if (HighContrast)
            {
                OnPropertyChanged(null);
                HighContrastSmartAssist();
                HighContrastAssistInProgress = false;
                return;
            }

            OnPropertyChanged();
        }
    }
    public Brush? AccentForeground
    {
        get => _accentForeground;
        set
        {
            _accentForeground = value;

            if (HighContrast)
            {
                OnPropertyChanged(null);
                HighContrastSmartAssist();
                HighContrastAssistInProgress = false;
                return;
            }

            OnPropertyChanged();
        }
    }
    public List<FontFamily> AvailableFonts { get; } = [];
    public FontFamily? DefaultFont { get; private set; }
    private readonly string[] DefaultFonts = ["Segoe UI", "Helvetica Neue", "Arial", "Noto Sans", "Liberation Sans", "DejaVu Sans", "sans‑serif"];
    public bool FirstRun
    {
        get => _firstRun;
        set
        {
            _firstRun = value;
            OnPropertyChanged();
        }
    }
    public bool HighContrast
    {
        get => _highContrast;
        set
        {
            _highContrast = value;
            OnPropertyChanged(null);

            SetMenuColors();
        }
    }
    public bool HighContrastAssist
    {
        get => _highContrastAssist;
        set
        {
            _highContrastAssist = value;
            OnPropertyChanged();
        }
    }
    private bool HighContrastAssistInProgress;
    public string LastActiveDatabase { get; set; } = string.Empty;
    public List<string> LastDatabases { get; } = [];
    public int LineTolerance
    {
        get => _lineTolerance;
        set
        {
            _lineTolerance = Math.Min(36, Math.Max(0, value));
            OnPropertyChanged();
        }
    }
    public Brush? ListBackground
    {
        get => _listBackground;
        set
        {
            _listBackground = value;

            if (HighContrast)
            {
                OnPropertyChanged(null);
                HighContrastSmartAssist();
                HighContrastAssistInProgress = false;
                return;
            }

            OnPropertyChanged();
        }
    }
    public Brush? ListForeground
    {
        get => _listForeground;
        set
        {
            _listForeground = value;

            if (HighContrast)
            {
                OnPropertyChanged(null);
                HighContrastSmartAssist();
                HighContrastAssistInProgress = false;
                return;
            }

            OnPropertyChanged();
        }
    }
    public FontFamily? MainFontFamily
    {
        get => _mainFontFamily;
        set
        {
            _mainFontFamily = value; MainTypeFace = new(value, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            OnPropertyChanged();
        }
    }
    public double MainFontSize
    {
        get => _mainFontSize;
        set
        {
            _mainFontSize = Math.Min(24.0, Math.Max(10.0, value));
            OnPropertyChanged();
        }
    }
    public Typeface? MainTypeFace
    {
        get => _mainTypeFace;
        set
        {
            _mainTypeFace = value;
            OnPropertyChanged();
        }
    }
    public Brush? MenuBackground
    {
        get => _menuBackground;
        set
        {
            _menuBackground = value;

            if (HighContrast)
            {
                OnPropertyChanged(null);
                HighContrastSmartAssist();
                HighContrastAssistInProgress = false;
                return;
            }

            OnPropertyChanged();
        }
    }
    public Brush? MenuForeground
    {
        get => _menuForeground;
        set
        {
            _menuForeground = value;

            if (HighContrast)
            {
                OnPropertyChanged(null);
                HighContrastSmartAssist();
                HighContrastAssistInProgress = false;
                return;
            }

            OnPropertyChanged();
        }
    }
    public double NoteClickthrough
    {
        get => _noteClickthrough;
        set
        {
            _noteClickthrough = value;
            NoteClickthroughInverse = value == 0.0 ? -1.0 : 1.0 / value;
            OnPropertyChanged();
        }
    }
    public double NoteClickthroughInverse
    {
        get => _noteClickthroughInverse;
        set
        {
            _noteClickthroughInverse = value;
            OnPropertyChanged();
        }
    }
    public double NoteTransparency
    {
        get => _noteTransparency;
        set
        {
            _noteTransparency = value;

            foreach (SearchResult note in OpenQueries)
            {
                if (!note.IsActive)
                    note.Opacity = 1.0 - (value * 0.01);
            }

            OnPropertyChanged();
        }
    }
    public bool QueryAllDatabases
    {
        get => _queryAllDatabases;
        set
        {
            _queryAllDatabases = value;
            OnPropertyChanged();
        }
    }
    public ObservableCollection<PathItem> RecentDatabases { get; } = [];
    public ObservableCollection<NoteRecord> RecentNotes { get; } = [];
    public ObservableCollection<NoteRecord> SearchResults { get; } = [];
    public bool SearchResultsOnTop
    {
        get => _searchResultsOnTop;
        set
        {
            _searchResultsOnTop = value;
            SearchResultsInTaskbar = !value;
            OnPropertyChanged();
        }
    }
    public bool SearchResultsInTaskbar
    {
        get => _searchResultsInTaskbar;
        set
        {
            _searchResultsInTaskbar = value;
            OnPropertyChanged();
        }
    }
    public Theme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            _selectedTheme = value;
            OnPropertyChanged();

            SwitchTheme(value);
        }
    }
    public bool SnapSearchResults
    {
        get => _snapSearchResults;
        set
        {
            _snapSearchResults = value;
            OnPropertyChanged();
        }
    }
    public List<Theme> ThemeCollection { get; } = [];
    public string VersionString { get; } = $"v. {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)} © Taica, {GetBuildYear(Assembly.GetExecutingAssembly())}";

    private static int GetBuildYear(Assembly assembly)
    {
        const string prefix = "+build";
        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        string? value = attr?.InformationalVersion;
        int index = value?.IndexOf(prefix, StringComparison.Ordinal) ?? 0;
        if (index > 0 && DateTime.TryParseExact(value?[(index + prefix.Length)..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result.Year;

        return default;
    }

    private void HighContrastSmartAssist([CallerMemberName] string? name = null)
    {
        if (!HighContrastAssist)
            return;

        if (name is null)
            return;

        if (HighContrastAssistInProgress)
            return;

        HighContrastAssistInProgress = true;

        try
        {
            var type = GetType();

            SolidColorBrush GetBrush(string p) => type.GetProperty(p)?.GetValue(this) as SolidColorBrush ?? Brushes.Black;
            void SetBrush(string p, Brush? v) => type.GetProperty(p)?.SetValue(this, v);

            string otherName = name switch
            {
                "AccentForeground" => "AccentBackground",
                "AccentBackground" => "AccentForeground",
                "ListForeground" => "ListBackground",
                "ListBackground" => "ListForeground",
                "MenuForeground" => "MenuBackground",
                "MenuBackground" => "MenuForeground",
                _ => string.Empty
            };

            if (otherName.Length == 0)
                return;

            Color fixedColor = GetBrush(name).Color;
            Color otherColor = GetBrush(otherName).Color;

            double Lfixed = GetRelativeLuminance(fixedColor);
            double Lother = GetRelativeLuminance(otherColor);

            double currentRatio = (Math.Max(Lfixed, Lother) + 0.05) / (Math.Min(Lfixed, Lother) + 0.05);

            if (currentRatio >= 7.0)
                return;

            double darkTarget = (Lfixed + 0.05) / 7.0 - 0.05;
            double lightTarget = 7.0 * (Lfixed + 0.05) - 0.05;

            bool canDarken = darkTarget >= 0.0;
            bool canLighten = lightTarget <= 1.0;

            Color newColor;

            if (canDarken && canLighten)
            {
                bool otherWasLighter = Lother > Lfixed;
                newColor = otherWasLighter
                    ? SetLuminance(otherColor, lightTarget, lighten: true)
                    : SetLuminance(otherColor, darkTarget, lighten: false);
            }
            else if (canLighten)
            {
                newColor = SetLuminance(otherColor, lightTarget, lighten: true);
            }
            else if (canDarken)
            {
                newColor = SetLuminance(otherColor, darkTarget, lighten: false);
            }
            else
            {
                double blackRatio = (Lfixed + 0.05) / 0.05;
                double whiteRatio = 1.05 / (Lfixed + 0.05);
                newColor = blackRatio >= whiteRatio ? Color.FromArgb(otherColor.A, 0, 0, 0) : Color.FromArgb(otherColor.A, 255, 255, 255);
            }

            SetBrush(otherName, new SolidColorBrush(newColor));
        }
        finally
        {
            HighContrastAssistInProgress = false;
        }
    }

    private void InitFonts()
    {
        AvailableFonts.Clear();
        AvailableFonts.AddRange(Fonts.SystemFontFamilies);
        AvailableFonts.Sort(new Comparison<FontFamily>((f1, f2) => string.CompareOrdinal(f1.Source, f2.Source)));

        for (int i = 0; i < AvailableFonts.Count && DefaultFont is null; i++)
        {
            if (DefaultFonts.Contains(AvailableFonts[i].Source))
                DefaultFont ??= AvailableFonts[i];
        }
    }

    private void InitThemes()
    {
        ThemeCollection.Clear();
        ThemeCollection.AddRange(Themes.ThemeCollection);
        ThemeCollection.Sort(new Comparison<Theme>((t1, t2) => string.CompareOrdinal(t1.Name, t2.Name)));
    }

    public async Task Load()
    {
        InitFonts();
        InitThemes();

        if (!File.Exists(SettingsFile))
            return;

        foreach (string setting in File.ReadAllLines(SettingsFile))
        {
            var keyValue = setting.Trim().Split(':', 2);

            if (string.IsNullOrWhiteSpace(keyValue[0]))
                continue;

            if (string.IsNullOrWhiteSpace(keyValue[1]))
                continue;

            switch (keyValue[0])
            {
                case "AccentBackground":
                    _accentBackground = BrushFromBytes(keyValue[1]);
                    break;
                case "AccentForeground":
                    _accentForeground = BrushFromBytes(keyValue[1]);
                    break;
                case "FirstRun":
                    if (!bool.TryParse(keyValue[1], out var firstRun))
                        firstRun = true;

                    FirstRun = firstRun;
                    break;
                case "FontFamily":
                    MainFontFamily = new(keyValue[1]);
                    break;
                case "FontSize":
                    if (!double.TryParse(keyValue[1], CultureInfo.InvariantCulture, out var mainFontSize))
                        mainFontSize = 12;

                    MainFontSize = mainFontSize;
                    break;
                case "HighContrast":
                    if (!bool.TryParse(keyValue[1], out var highContrast))
                        highContrast = false;

                    HighContrast = highContrast;
                    break;
                case "HighContrastAssist":
                    if (!bool.TryParse(keyValue[1], out var highContrastAssist))
                        highContrastAssist = false;

                    HighContrastAssist = highContrastAssist;
                    break;
                case "LastActiveDatabase":
                    LastActiveDatabase = keyValue[1];
                    break;
                case "LastActiveNotes":
                    foreach (var note in keyValue[1].Split(';').Distinct())
                    {
                        if (!string.IsNullOrWhiteSpace(note))
                            LastActiveNotes.Add(note);
                    }

                    break;
                case "LastActiveNotesHeight":
                    foreach (var sHeight in keyValue[1].Split(';').Distinct())
                    {
                        var hSplit = sHeight.Split(':');
                        if (hSplit.Length < 3)
                            continue;

                        if (int.TryParse(hSplit[2], out var dHeight))
                            LastActiveNotesHeight.TryAdd(hSplit[0] + ":" + hSplit[1], dHeight);
                    }
                    break;
                case "LastActiveNotesLeft":
                    foreach (var sLeft in keyValue[1].Split(';').Distinct())
                    {
                        var lSplit = sLeft.Split(':');
                        if (lSplit.Length < 3)
                            continue;

                        if (int.TryParse(lSplit[2], out var dLeft))
                            LastActiveNotesLeft.TryAdd(lSplit[0] + ":" + lSplit[1], dLeft);
                    }
                    break;
                case "LastActiveNotesTop":
                    foreach (var sTop in keyValue[1].Split(';').Distinct())
                    {
                        var tSplit = sTop.Split(':');
                        if (tSplit.Length < 3)
                            continue;

                        if (int.TryParse(tSplit[2], out var dTop))
                            LastActiveNotesTop.TryAdd(tSplit[0] + ":" + tSplit[1], dTop);
                    }
                    break;
                case "LastActiveNotesWidth":
                    foreach (var sWidth in keyValue[1].Split(';').Distinct())
                    {
                        var wSplit = sWidth.Split(':');
                        if (wSplit.Length < 3)
                            continue;

                        if (int.TryParse(wSplit[2], out var dWidth))
                            LastActiveNotesWidth.TryAdd(wSplit[0] + ":" + wSplit[1], dWidth);
                    }
                    break;
                case "LastDatabases":
                    FirstRun = false;
                    LastDatabases.AddRange(keyValue[1].Replace("?\\", DocumentsFolder).Split(';').Distinct().Where(File.Exists));

                    foreach (var file in LastDatabases)
                    {
                        if (!Databases.Any(db => Path.GetFullPath(db.DBFile).Equals(Path.GetFullPath(file), StringComparison.Ordinal)))
                            await Database.Create(file);
                    }

                    if (Databases.Count != 0)
                        break;

                    await Database.Create(Path.Join(Subfolders[Strings.Subfolder_Databases], DefaultDatabase, $"{DefaultDatabase}.sidb"));
                    break;
                case "ListBackground":
                    _listBackground = BrushFromBytes(keyValue[1]);
                    break;
                case "ListForeground":
                    _listForeground = BrushFromBytes(keyValue[1]);
                    break;
                case "MenuBackground":
                    _menuBackground = BrushFromBytes(keyValue[1]);
                    break;
                case "MenuForeground":
                    _menuForeground = BrushFromBytes(keyValue[1]);
                    break;
                case "NoteClickthrough":
                    if (!double.TryParse(keyValue[1], out var clickthrough))
                        clickthrough = 0.25;

                    NoteClickthrough = clickthrough;
                    break;
                case "NoteTransparency":
                    if (!double.TryParse(keyValue[1], out var transparency))
                        transparency = 95.0;

                    NoteTransparency = transparency;
                    break;
                case "QueryAllDatabases":
                    if (!bool.TryParse(keyValue[1], out var queryAllDatabases))
                        queryAllDatabases = false;

                    QueryAllDatabases = queryAllDatabases;
                    break;
                case "RecentDatabases":
                    FirstRun = false;

                    foreach (var path in keyValue[1].Replace("?\\", DocumentsFolder).Split(';').Distinct())
                        RecentDatabases.Add(new() { FullPath = path });

                    break;
                case "RecentNotesSortMode":
                    if (!int.TryParse(keyValue[1], out var sortMode))
                        sortMode = 0;

                    RecentEntriesSortMode = (SortType)sortMode;
                    break;
                case "RibbonDisplayMode":
                    if (!int.TryParse(keyValue[1], out var displayMode))
                        displayMode = 0;

                    RibbonTabContent = (DisplayType)displayMode;
                    break;
                case "SearchResultsOnTop":
                    if (!bool.TryParse(keyValue[1], out var searchResultsOnTop))
                        searchResultsOnTop = false;

                    SearchResultsOnTop = searchResultsOnTop;
                    break;
                case "SelectedTheme":
                    _selectedTheme = Themes.GetTheme(keyValue[1]);
                    break;
                case "SnapSearchResults":
                    if (!bool.TryParse(keyValue[1], out var snapSearchResults))
                        snapSearchResults = false;

                    SnapSearchResults = snapSearchResults;
                    break;
                default:
                    break;
            }
        }
    }

    public void Save() => File.WriteAllLines(SettingsFile, [
        $"AccentBackground:{BytesFromBrush(_accentBackground)}",
        $"AccentForeground:{BytesFromBrush(_accentForeground)}",
        $"FirstRun:{FirstRun}",
        $"FontFamily:{MainFontFamily?.Source}",
        $"FontSize:{MainFontSize}",
        $"HighContrast:{HighContrast}",
        $"HighContrastAssist:{HighContrastAssist}",
        $"LastActiveDatabase:{CurrentDatabase.Name}",
        $"LastActiveNotes:{string.Join(';', OpenQueries.Select(query => $"{query.ViewModel.Record.DB?.Name}:{query.ViewModel.Record.Index}"))}",
        $"LastActiveNotesHeight:{string.Join(';', OpenQueries.Select(query => $"{query.ViewModel.Record.DB?.Name}:{query.ViewModel.Record.Index}:{query.Height}"))}",
        $"LastActiveNotesLeft:{string.Join(';', OpenQueries.Select(query => $"{query.ViewModel.Record.DB?.Name}:{query.ViewModel.Record.Index}:{query.Left}"))}",
        $"LastActiveNotesTop:{string.Join(';', OpenQueries.Select(query => $"{query.ViewModel.Record.DB?.Name}:{query.ViewModel.Record.Index}:{query.Top}"))}",
        $"LastActiveNotesWidth:{string.Join(';', OpenQueries.Select(query => $"{query.ViewModel.Record.DB?.Name}:{query.ViewModel.Record.Index}:{query.Width}"))}",
        $"LastDatabases:{string.Join(';', DatabaseFiles.Distinct().Where(File.Exists)).Replace(DocumentsFolder, "?\\")}",
        $"ListBackground:{BytesFromBrush(_listBackground)}",
        $"ListForeground:{BytesFromBrush(_listForeground)}",
        $"MenuBackground:{BytesFromBrush(_menuBackground)}",
        $"MenuForeground:{BytesFromBrush(_menuForeground)}",
        $"NoteClickthrough:{(double)NoteClickthrough}",
        $"NoteTransparency:{(double)NoteTransparency}",
        $"QueryAllDatabases:{QueryAllDatabases}",
        $"RecentDatabases:{string.Join(';', RecentDatabases.Distinct()).Replace(DocumentsFolder, "?\\")}",
        $"RecentNotesSortMode:{(int)RecentEntriesSortMode}",
        $"RibbonDisplayMode:{(int)RibbonTabContent}",
        $"SearchResultsOnTop:{SearchResultsOnTop}",
        $"SelectedTheme:{SelectedTheme}",
        $"SnapSearchResults:{SnapSearchResults}",
    ]);
}