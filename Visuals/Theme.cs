namespace SylverInk.Visuals;

/// <summary>
/// A predefined set of Brush objects that may be selected by the user in the Settings menu.
/// </summary>
public class Theme(string name, Brush accentBg, Brush accentFg, Brush listBg, Brush listFg, Brush menuBg, Brush menuFg)
{
    private readonly Brush _accentBackground = accentBg;
    private readonly Brush _accentForeground = accentFg;
    private readonly Brush _listBackground = listBg;
    private readonly Brush _listForeground = listFg;
    private readonly Brush _menuBackground = menuBg;
    private readonly Brush _menuForeground = menuFg;
    private readonly string _name = name;

    public Brush AccentBackground => _accentBackground;
    public Brush AccentForeground => _accentForeground;
    public Brush ListBackground => _listBackground;
    public Brush ListForeground => _listForeground;
    public Brush MenuBackground => _menuBackground;
    public Brush MenuForeground => _menuForeground;
    public string Name => _name;

    public override string ToString() => Name;
}