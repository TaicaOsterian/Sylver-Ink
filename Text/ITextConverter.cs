namespace SylverInk.Text;

public enum TextFormat
{
	Plaintext,
	Xaml,
}

public interface ITextConverter
{
	abstract string Convert(string text, TextFormat sourceFormat);
}
