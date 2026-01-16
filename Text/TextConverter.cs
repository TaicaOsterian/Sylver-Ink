using System;
using System.Collections.Generic;
using System.Windows.Documents;

namespace SylverInk.Text;

public static class TextConverter
{
	private static readonly Dictionary<TextFormat, ITextConverter> _converters = new()
	{
		{ TextFormat.Plaintext, new PlaintextConverter() },
		{ TextFormat.Xaml, new XamlConverter() },
	};

	public static string Convert(string text, TextFormat from, TextFormat to)
	{
		if (_converters.TryGetValue(to, out var converter))
			return converter.Convert(text, from);

		throw new ArgumentException($"No converter registered for {to}");
	}

	public static FlowDocument Parse(string text, TextFormat from)
	{
		if (_converters.TryGetValue(from, out var converter))
			return converter.Parse(text);

		throw new ArgumentException($"No parser registered for {from}");
	}

	public static string Save(FlowDocument document, TextFormat to)
	{
		if (_converters.TryGetValue(to, out var converter))
			return converter.Save(document);

		throw new ArgumentException($"No saver registered for {to}");
	}
}