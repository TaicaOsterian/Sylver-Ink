using System;
using static SylverInk.Text.FlowDocumentUtils;

namespace SylverInk.Text;

public class XamlConverter : ITextConverter
{
	public string Convert(string text, TextFormat sourceFormat) => sourceFormat switch
	{
		TextFormat.Plaintext => ToXamlFromPlaintext(text),
		TextFormat.Xaml => text,
		_ => throw new NotSupportedException()
	};

	private static string ToXamlFromPlaintext(string text) => FlowDocumentToXaml(PlaintextToFlowDocument(new(), text));
}
