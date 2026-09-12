using SylverInk.XAML.Objects;
using static SylverInk.Interop.VisualUtils;

namespace SylverInk.XAMLUtils;

public static class Themes
{
    public static Theme Catpuccin { get; } = new(Strings.Label_Catpuccin,
            BrushFromBytes("1E1E2E"),
            BrushFromBytes("89B4FA"),
            BrushFromBytes("11111B"),
            BrushFromBytes("CDD6F4"),
            BrushFromBytes("313244"),
            BrushFromBytes("A6ADC8"));

    public static Theme HighContrast { get; } = new(Strings.Label_HighContrast,
            BrushFromBytes("000000"),
            BrushFromBytes("00FFFF"),
            BrushFromBytes("1E1E1E"),
            BrushFromBytes("FFFFFF"),
            BrushFromBytes("121212"),
            BrushFromBytes("FFFFFF"));

    public static Theme Manila { get; } = new(Strings.Label_Manila,
            BrushFromBytes("EEE8AA"),
            BrushFromBytes("0000FF"),
            BrushFromBytes("FFFFFF"),
            BrushFromBytes("000000"),
            BrushFromBytes("F5F5DC"),
            BrushFromBytes("696969"));

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
