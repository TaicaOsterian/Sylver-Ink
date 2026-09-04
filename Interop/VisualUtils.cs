using System.Globalization;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace SylverInk.Interop;

/// <summary>
/// Helper functions serving visual tree and Drawing type-conversion needs.
/// </summary>
public static class VisualUtils
{
    public static SolidColorBrush BrushFromBytes(string data)
    {
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

    /// <summary>
    /// Recursively iterate through a visual tree to change the style of a Menu object and its items.
    /// </summary>
    public static void SetMenuColors(DependencyObject parent)
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
}
