using System;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SylverInk.Text;

/// <summary>
/// Static functions serving operations on FlowDocument objects.
/// </summary>
public static partial class FlowDocumentUtils
{
	private const int PreviewLength = 250;

	public static string FlowDocumentPreview(FlowDocument? document)
	{
		if (document is null)
			return string.Empty;

		if (!document.IsInitialized)
			return string.Empty;

		StringBuilder content = new();
		var pointer = document.ContentStart;

		while (pointer is not null && document.ContentStart.GetOffsetToPosition(pointer) < PreviewLength)
			pointer = TranslatePointer(pointer, ref content);

		return content.ToString().Trim();
	}
	
	public static void ScrollToText(RichTextBox box, string text, LogicalDirection direction = LogicalDirection.Forward)
	{
		int index = 0;
		string plaintext = (direction == LogicalDirection.Forward
			? new TextRange(box.CaretPosition, box.Document.ContentEnd)
			: new TextRange(box.Document.ContentStart, box.CaretPosition))
			.Text.ReplaceLineEndings(string.Empty);

		TextPointer pointer = direction == LogicalDirection.Forward
			? box.CaretPosition
			: box.Document.ContentStart;

		if (direction == LogicalDirection.Backward && plaintext.EndsWith(text, StringComparison.InvariantCultureIgnoreCase))
			plaintext = plaintext[..^text.Length];

		int offset = direction == LogicalDirection.Forward
			? plaintext.IndexOf(text, StringComparison.InvariantCultureIgnoreCase)
			: plaintext.LastIndexOf(text, StringComparison.InvariantCultureIgnoreCase);

		if (offset == -1)
			return;

		offset += text.Length;

		while (index < offset)
		{
			if (pointer is null)
				return;

			var next = pointer.GetTextInRun(LogicalDirection.Forward);
			if (index + next.Length > offset)
			{
				pointer = pointer.GetPositionAtOffset(offset - index);
				break;
			}

			index += next.Length;
			pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
		}

		box.CaretPosition = pointer;

		pointer = box.CaretPosition;
		for (int i = 0; i < text.Length; i++)
		{
			pointer = pointer.GetNextInsertionPosition(LogicalDirection.Backward);
			if (pointer is null)
				return;
		}

		box.Focus();
		box.Selection.Select(pointer, box.CaretPosition);
	}

	public static TextPointer? TranslatePointer(TextPointer textPointer, ref StringBuilder content)
	{
		switch (textPointer.GetPointerContext(LogicalDirection.Forward))
		{
			case TextPointerContext.None:
				return null;
			case TextPointerContext.Text:
				var runText = textPointer.GetTextInRun(LogicalDirection.Forward);

				// Xaml escape sequences aren't handled by XamlReader.Parse, which is very frustrating.
				content.Append(runText.Replace("{}{", "{"));

				return textPointer.GetPositionAtOffset(textPointer.GetTextRunLength(LogicalDirection.Forward));
			case TextPointerContext.ElementStart:
				var element = textPointer.GetAdjacentElement(LogicalDirection.Forward);

				if (element is Paragraph && content.Length > 0)
				{
					content.AppendLine();
					content.AppendLine();
				}
				else if (element is LineBreak)
					content.AppendLine();

				return textPointer.GetNextContextPosition(LogicalDirection.Forward);
			default:
				return textPointer.GetNextContextPosition(LogicalDirection.Forward);
		}
	}
}
