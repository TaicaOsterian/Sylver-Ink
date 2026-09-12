namespace SylverInk.XAML.Objects;

/// <summary>
/// A predefined set of Brush objects that may be selected by the user in the Settings menu.
/// </summary>
public class Theme(string name, Brush aB, Brush aF, Brush lB, Brush lF, Brush mB, Brush mF)
{
    private readonly Brush _accentBackground = aB;
    private readonly Brush _accentForeground = aF;
    private readonly Brush _listBackground = lB;
    private readonly Brush _listForeground = lF;
    private readonly Brush _menuBackground = mB;
    private readonly Brush _menuForeground = mF;
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