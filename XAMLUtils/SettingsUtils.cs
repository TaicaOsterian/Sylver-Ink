using SylverInk.Visuals;

namespace SylverInk.XAMLUtils;

public static class SettingsUtils
{
    public static void ColorChanged(string? ColorTag, Brush? ColorSelection, RichTextBox? TextTarget = null)
    {
        if (ColorTag is null)
            return;

        var theme = Settings.HighContrast ? Themes.HighContrast : Settings.SelectedTheme;

        switch (ColorTag)
        {
            case "P1F":
                Settings.MenuForeground = ColorSelection ?? theme.MenuForeground;
                break;
            case "P1B":
                Settings.MenuBackground = ColorSelection ?? theme.MenuBackground;
                break;
            case "P2F":
                Settings.ListForeground = ColorSelection ?? theme.ListForeground;
                break;
            case "P2B":
                Settings.ListBackground = ColorSelection ?? theme.ListBackground;
                break;
            case "P3F":
                Settings.AccentForeground = ColorSelection ?? theme.AccentForeground;
                break;
            case "P3B":
                Settings.AccentBackground = ColorSelection ?? theme.AccentBackground;
                break;
            case "PT":
                if (TextTarget is null)
                    break;

                if (TextTarget.Selection.IsEmpty)
                    break;

                if (ColorSelection is not null)
                {
                    TextTarget.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, ColorSelection);
                    break;
                }

                // Programmatically clearing a single property on a TextSelection object is convoluted.
                // Other methods exist, but by far the simplest is to clear *all* properties, then reapply the ones we aren't changing.

                var end = TextTarget.Selection.End;
                TextPointer next;
                var pointer = TextTarget.Selection.Start;
                var start = TextTarget.Selection.Start;

                while (pointer?.GetOffsetToPosition(end) > 0)
                {
                    var runLength = pointer.GetTextRunLength(LogicalDirection.Forward);
                    next = runLength > 0 ? pointer.GetPositionAtOffset(runLength) : pointer.GetNextContextPosition(LogicalDirection.Forward);

                    if (next is null)
                        break;

                    TextTarget.Selection.Select(pointer, next);
                    pointer = next;

                    if (TextTarget.Selection.IsEmpty)
                        continue;

                    var fontFamily = TextTarget.Selection.GetPropertyValue(TextElement.FontFamilyProperty);
                    var fontSize = TextTarget.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                    var fontStyle = TextTarget.Selection.GetPropertyValue(TextElement.FontStyleProperty);
                    var fontWeight = TextTarget.Selection.GetPropertyValue(TextElement.FontWeightProperty);

                    TextTarget.Selection.ClearAllProperties();

                    TextTarget.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, fontFamily);
                    TextTarget.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, fontSize);
                    TextTarget.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, fontStyle);
                    TextTarget.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, fontWeight);
                }

                TextTarget.Selection.Select(start, end);

                break;
        }
    }
}
