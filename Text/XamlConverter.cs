using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using static SylverInk.XAMLUtils.ImageUtils;

namespace SylverInk.Text;

public partial class XamlConverter : ITextConverter
{
	private readonly static string FlowDocumentClosing = "</FlowDocument>";
	private readonly static string FlowDocumentOpening = @"<FlowDocument xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">";

	public string Convert(string text, TextFormat sourceFormat) => sourceFormat switch
	{
		TextFormat.Plaintext => ToXamlFromPlaintext(text),
		TextFormat.Xaml => text,
		_ => throw new NotSupportedException()
	};

	public FlowDocument Parse(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return new();

		var escaped = ImageTagRegex().Replace(text.Replace("{}{", "{"), string.Empty);

		if (!escaped.StartsWith("<FlowDocument"))
			escaped = $"{FlowDocumentOpening}{escaped}";

		if (!escaped.EndsWith(FlowDocumentClosing))
			escaped = $"{escaped}{FlowDocumentClosing}";

		var document = (FlowDocument)XamlReader.Parse(escaped);
		var pointer = document.ContentStart;

		while (pointer is not null)
		{
			if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart
				&& pointer.GetAdjacentElement(LogicalDirection.Forward) is Paragraph paragraph
				&& paragraph.Tag is "base64")
			{
				while (string.IsNullOrEmpty(pointer.GetTextInRun(LogicalDirection.Forward)))
					pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);

				var img = DecodeEmbed(pointer.GetTextInRun(LogicalDirection.Forward));
				BlockUIContainer container = new(img);
				document.Blocks.InsertBefore(paragraph, container);
				document.Blocks.Remove(paragraph);

				while (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.ElementStart)
					pointer = pointer.GetNextContextPosition(LogicalDirection.Backward);

				pointer = pointer.GetNextContextPosition(LogicalDirection.Backward) ?? document.ContentStart;
			}

			pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
		}

		return document;
	}

	public string Save(FlowDocument document)
	{
		var content = XamlWriter.Save(document);
		content = content.Replace("{}{", "{");

		var pointer = document.ContentStart;

		while (pointer is not null)
		{
			if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart
				&& pointer.GetAdjacentElement(LogicalDirection.Forward) is BlockUIContainer container
				&& container.Child is Image img)
			{
				var embed = EncodeEmbed(img);
				var match = ContainerRegex().Match(content);

				content = $"{content[..match.Index]}<Paragraph Tag=\"base64\">{System.Convert.ToBase64String(embed)}</Paragraph>{content[(match.Index + match.Length)..]}";
			}

			pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
		}

		return FlowDocumentOpeningRegex().Replace(FlowDocumentClosingRegex().Replace(content, string.Empty), string.Empty);
	}

	private string ToXamlFromPlaintext(string text) => Save(TextConverter.Parse(text, TextFormat.Plaintext));

	[GeneratedRegex(@"<BlockUIContainer.*?</BlockUIContainer>")]
	private static partial Regex ContainerRegex();

	[GeneratedRegex(@"</FlowDocument>")]
	private static partial Regex FlowDocumentClosingRegex();

	[GeneratedRegex(@"<FlowDocument.*?>")]
	private static partial Regex FlowDocumentOpeningRegex();

	[GeneratedRegex(@"<Image.*?>.*?</Image>")]
	private static partial Regex ImageTagRegex();
}
