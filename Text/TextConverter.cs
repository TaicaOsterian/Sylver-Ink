using System.Globalization;

namespace SylverInk.Text;

internal static class TextConverter
{
    private static readonly Dictionary<TextFormat, ITextConverter> _converters = new()
    {
        { TextFormat.Plaintext, new PlaintextConverter() },
        { TextFormat.Xaml, new XamlConverter() },
    };

    internal static string Convert(string text, TextFormat from, TextFormat to)
    {
        if (_converters.TryGetValue(to, out var converter))
            return converter.Convert(text, from);

        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, CacheTextNoConverterRegistered, to));
    }

    internal static FlowDocument Parse(string text, TextFormat from)
    {
        if (_converters.TryGetValue(from, out var converter))
            return converter.Parse(text);

        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, CacheTextNoParserRegistered, from));
    }

    internal static string Save(FlowDocument document, TextFormat to)
    {
        if (_converters.TryGetValue(to, out var converter))
            return converter.Save(document);

        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, CacheTextNoSaverRegistered, to));
    }
}
