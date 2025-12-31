using System;
using static SylverInk.Text.FlowDocumentUtils;

namespace SylverInk.Text;

public class PlaintextConverter : ITextConverter
{
	public string Convert(string text, TextFormat sourceFormat) => sourceFormat switch
	{
		TextFormat.Plaintext => text,
		TextFormat.Xaml => ToPlaintextFromXaml(text),
		_ => throw new NotSupportedException()
	};

	private static string ToPlaintextFromXaml(string text) => FlowDocumentToPlaintext(XamlToFlowDocument(text));
}
