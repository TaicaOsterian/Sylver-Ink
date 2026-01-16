using System.Windows.Documents;

namespace SylverInk.Text;

public enum TextFormat
{
	Plaintext,
	Xaml,
	//Markdown,
}

/// <summary>
/// For extensible conversion needs among different text storage formats.
/// </summary>
public interface ITextConverter
{
	/// <summary>
	/// Convert from one text format to another.
	/// </summary>
	abstract string Convert(string text, TextFormat sourceFormat);

	/// <summary>
	/// Convert from text to a WPF FlowDocument object.
	/// </summary>
	abstract FlowDocument Parse(string text);

	/// <summary>
	/// Convert from a WPF FlowDocument object to text.
	/// </summary>
	abstract string Save(FlowDocument document);
}
