using System;
using System.Collections.Generic;

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
}