using static SylverInk.Visuals.VisualUtils;

namespace SylverInk.Visuals;

public static class Themes
{
    public static Theme Catpuccin { get; } = new(Strings.Label_Catpuccin,
            accentBg: BrushFromBytes("1E1E2E"),
            accentFg: BrushFromBytes("89B4FA"),
            listBg:   BrushFromBytes("11111B"),
            listFg:   BrushFromBytes("CDD6F4"),
            menuBg:   BrushFromBytes("313244"),
            menuFg:   BrushFromBytes("A6ADC8"));

    public static Theme HighContrast { get; } = new(Strings.Label_HighContrast,
            accentBg: BrushFromBytes("000000"),
            accentFg: BrushFromBytes("00FFFF"),
            listBg:   BrushFromBytes("1E1E1E"),
            listFg:   BrushFromBytes("FFFFFF"),
            menuBg:   BrushFromBytes("121212"),
            menuFg:   BrushFromBytes("FFFFFF"));

    public static Theme Manila { get; } = new(Strings.Label_Manila,
            accentBg: BrushFromBytes("EEE8AA"),
            accentFg: BrushFromBytes("0000FF"),
            listBg:   BrushFromBytes("FFFFFF"),
            listFg:   BrushFromBytes("000000"),
            menuBg:   BrushFromBytes("F5F5DC"),
            menuFg:   BrushFromBytes("696969"));

    public static Theme Default => Manila;

    public static ICollection<Theme> ThemeCollection { get; } = [Catpuccin, HighContrast, Manila];

    public static Theme GetTheme(string name)
    {
        foreach (Theme theme in ThemeCollection)
        {
            if (theme.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return theme;
        }

        return Default;
    }
}
