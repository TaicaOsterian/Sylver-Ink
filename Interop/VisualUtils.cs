using SylverInk.XAML.Objects;
using System.Globalization;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace SylverInk.Interop;

/// <summary>
/// Helper functions serving visual tree and Drawing type-conversion needs.
/// </summary>
public static class VisualUtils
{
    public const float RsRGB = 0.2126f;
    public const float GsRGB = 0.7152f;
    public const float BsRGB = 0.0722f;
    public const float AsRGB = 1.0f;

    /// <summary>
    /// Parses an RGB or ARGB hex code into a SolidColorBrush object. This function is well-behaved for user interaction purposes, i.e. it will return a default value if the hex code is formatted incorrectly as opposed to throwing an exception.
    /// </summary>
    /// <returns>A SolidColorBrush encoding the provided hex code (or Brushes.Transparent if the data was invalid).</returns>
    public static SolidColorBrush BrushFromBytes(string data)
    {
        if (data.StartsWith('#'))
            data = data[1..];

        if (data.Length == 6)
            data = "FF" + data;

        if (data.Length != 8)
            return Brushes.Transparent;

        try
        {
            return new(new()
            {
                A = byte.Parse(data[..2], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo),
                R = byte.Parse(data[2..4], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo),
                G = byte.Parse(data[4..6], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo),
                B = byte.Parse(data[6..8], NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo)
            });
        }
        catch { return Brushes.Transparent; }
    }

    /// <summary>
    /// Formats a Brush object as a string of characters representing an RGB or ARGB hex code.
    /// </summary>
    /// <returns>A string-encoded RGB hex code, or (iff the brush had an alpha value not equal to 1.0) a string-encoded ARGB hex code.</returns>
    public static string BytesFromBrush(Brush? brush)
    {
        if (brush is not SolidColorBrush scb)
            return string.Empty;

        if ($"{scb.Color.A}" is "FF")
            return $"{scb?.Color.R:X2}{scb?.Color.G:X2}{scb?.Color.B:X2}";

        return $"{scb?.Color.A:X2}{scb?.Color.R:X2}{scb?.Color.G:X2}{scb?.Color.B:X2}";
    }

    public static T? FindVisualChildByName<T>(DependencyObject? parent, string name) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && name.Equals(child.GetValue(FrameworkElement.NameProperty) as string, StringComparison.Ordinal))
                return typedChild;

            if (FindVisualChildByName<T>(child, name) is T result)
                return result;
        }

        return null;
    }

    private static double GetChannelFromLuminance(double linear)
    {
        linear = Math.Clamp(linear, 0.0, 1.0);
        return linear <= 0.0031308
            ? linear * 12.92
            : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
    }

    private static double GetChannelLuminance(double channel) => channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    public static double GetRelativeLuminance(Color color)
    {
        const double i = 1.0 / 255.0;
        return RsRGB * GetChannelLuminance(color.R * i)
             + GsRGB * GetChannelLuminance(color.G * i)
             + BsRGB * GetChannelLuminance(color.B * i);
    }

    public static Color SetLuminance(Color color, double target, bool lighten)
    {
        double r = GetChannelLuminance(color.R / 255.0);
        double g = GetChannelLuminance(color.G / 255.0);
        double b = GetChannelLuminance(color.B / 255.0);
        double L = RsRGB * r + GsRGB * g + BsRGB * b;

        double t;
        if (lighten)
        {
            if (L >= 1.0) return color;
            t = Math.Clamp((target - L) / (1.0 - L), 0.0, 1.0);
        }
        else
        {
            if (L <= 0.0) return color;
            t = Math.Clamp(1.0 - target / L, 0.0, 1.0);
        }

        double r2 = lighten ? r + t * (1.0 - r) : r * (1.0 - t);
        double g2 = lighten ? g + t * (1.0 - g) : g * (1.0 - t);
        double b2 = lighten ? b + t * (1.0 - b) : b * (1.0 - t);

        return Color.FromArgb(
            color.A,
            (byte)Math.Round(GetChannelFromLuminance(r2) * 255.0),
            (byte)Math.Round(GetChannelFromLuminance(g2) * 255.0),
            (byte)Math.Round(GetChannelFromLuminance(b2) * 255.0));
    }

    public static void SetMenuColors()
    {
        foreach (Window window in Application.Current.Windows)
            SetMenuColors(window);
    }

    /// <summary>
    /// Recursively iterate through a visual tree to change the style of a Menu object and its items.
    /// </summary>
    private static void SetMenuColors(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            SetMenuColors(VisualTreeHelper.GetChild(parent, i));

        if (parent.GetType() != typeof(MenuItem))
            return;

        if (VisualTreeHelper.GetChild(parent, 0) is not Border itemBorder)
            return;

        if (itemBorder.Child is not Grid itemGrid)
            return;

        foreach (var itemChild in itemGrid.Children)
        {
            if (itemChild is not Popup popup)
                continue;

            if (popup.Child is not Border popupBorder)
                continue;

            BindingOperations.SetBinding(popupBorder, Control.BackgroundProperty, new Binding("AppSettings.MenuBackground"));
            BindingOperations.SetBinding(popupBorder, Control.BorderBrushProperty, new Binding("AppSettings.AccentBackground"));
            BindingOperations.SetBinding(popupBorder, Control.ForegroundProperty, new Binding("AppSettings.MenuForeground"));
            popupBorder.BorderThickness = new(1);

            if (popupBorder.Child is not ScrollViewer viewer)
                continue;

            if (viewer.Content is not Grid viewerGrid)
                continue;

            foreach (var viewerChild in viewerGrid.Children)
            {
                if (viewerChild is not System.Windows.Shapes.Rectangle rect)
                    continue;

                BindingOperations.SetBinding(rect, System.Windows.Shapes.Shape.FillProperty, new Binding("AppSettings.MenuBackground"));
            }

            return;
        }
    }

    public static void SwitchTheme(Theme theme)
    {
        using ContextFreeze _ = new(Settings);

        Settings.HighContrast = theme.Name.Equals(Strings.Label_HighContrast, StringComparison.Ordinal);

        Settings.AccentBackground = theme.AccentBackground;
        Settings.AccentForeground = theme.AccentForeground;
        Settings.ListBackground = theme.ListBackground;
        Settings.ListForeground = theme.ListForeground;
        Settings.MenuBackground = theme.MenuBackground;
        Settings.MenuForeground = theme.MenuForeground;
    }
}
