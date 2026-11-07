using SylverInk.Interop;
using System.Text;
using System.Windows.Markup;

namespace SylverInk.Text;

public partial class XamlConverter : ITextConverter
{
    private const string FlowDocumentClosing = "</FlowDocument>";
    private const string FlowDocumentOpening = @"<FlowDocument xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">";

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

        if (!escaped.StartsWith("<FlowDocument", StringComparison.Ordinal))
            escaped = $"{FlowDocumentOpening}{escaped}";

        if (!escaped.EndsWith(FlowDocumentClosing, StringComparison.Ordinal))
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

                ImageContainer imgContainer = ImageContainer.FromBase64(pointer.GetTextInRun(LogicalDirection.Forward));

                BlockUIContainer blockContainer = new(imgContainer);
                document.Blocks.InsertBefore(paragraph, blockContainer);
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
        var sb = new StringBuilder();

        for (var block = document.Blocks.FirstBlock; block is not null; block = block.NextBlock)
        {
            if (block is BlockUIContainer bc && bc.Child is ImageContainer img)
            {
                sb.Append("<Paragraph Tag=\"base64\">")
                    .Append(img.ToBase64())
                    .Append("</Paragraph>");
            }
            else
            {
                sb.Append(XamlWriter.Save(block).Replace("{}{", "{"));
            }
        }

        return sb.ToString();
    }

    private string ToXamlFromPlaintext(string text) => Save(TextConverter.Parse(text, TextFormat.Plaintext));

    [GeneratedRegex(@"<Image.*?>.*?</Image>")]
    private static partial Regex ImageTagRegex();
}
