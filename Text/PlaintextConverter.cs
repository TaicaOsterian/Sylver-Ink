using System;
using System.Text;
using System.Windows.Documents;
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

	public FlowDocument Parse(string text)
	{
		FlowDocument document = new();
		TextPointer pointer = document.ContentStart;

		var lineSplit = text.Replace("\r", string.Empty).Split('\n') ?? [];
		for (int i = 0; i < lineSplit.Length; i++)
		{
			var line = lineSplit[i];
			if (string.IsNullOrEmpty(line))
				continue;

			pointer.InsertTextInRun(line);
			while (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.ElementEnd)
				pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);

			if (i >= lineSplit.Length - 1)
				continue;

			if (string.IsNullOrEmpty(lineSplit[i + 1]))
				pointer = pointer.InsertParagraphBreak();
			else
				pointer = pointer.InsertLineBreak();
		}

		return document;
	}

	public string Save(FlowDocument document)
	{
		if (document is null)
			return string.Empty;

		if (!document.IsInitialized)
			return string.Empty;

		StringBuilder content = new();
		var pointer = document.ContentStart;

		while (pointer is not null)
			pointer = TranslatePointer(pointer, ref content);

		return content.ToString().Trim();
	}

	private string ToPlaintextFromXaml(string text) => Save(TextConverter.Parse(text, TextFormat.Xaml));
}
