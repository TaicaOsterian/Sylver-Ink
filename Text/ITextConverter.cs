using System.Windows.Documents;

namespace SylverInk.Text;

public enum TextFormat
{
	Plaintext,
	Xaml,
}

public interface ITextConverter
{
	abstract string Convert(string text, TextFormat sourceFormat);
	abstract FlowDocument Parse(string text);
	abstract string Save(FlowDocument document);
}
