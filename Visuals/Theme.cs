namespace SylverInk.Visuals;

/// <summary>
/// A predefined set of Brush objects that may be selected by the user in the Settings menu.
/// </summary>
public sealed class Theme(string name, Brush accentBg, Brush accentFg, Brush listBg, Brush listFg, Brush menuBg, Brush menuFg)
{
    private readonly Brush _accentBackground = accentBg;
    private readonly Brush _accentForeground = accentFg;
    private readonly Brush _listBackground = listBg;
    private readonly Brush _listForeground = listFg;
    private readonly Brush _menuBackground = menuBg;
    private readonly Brush _menuForeground = menuFg;
    private readonly string _name = name;

    internal Brush AccentBackground => _accentBackground;
    internal Brush AccentForeground => _accentForeground;
    internal Brush ListBackground => _listBackground;
    internal Brush ListForeground => _listForeground;
    internal Brush MenuBackground => _menuBackground;
    internal Brush MenuForeground => _menuForeground;
    internal string Name => _name;

    public override string ToString() => Name;
}
