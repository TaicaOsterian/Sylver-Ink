using System.Windows.Markup;

namespace SylverInk.Interop;

public class ImageContainerValueSerializer : ValueSerializer
{
    public override bool CanConvertToString(object value, IValueSerializerContext context) => value is ImageContainer;

    public override bool CanConvertFromString(string value, IValueSerializerContext context) => !string.IsNullOrWhiteSpace(value);

    public override string ConvertToString(object value, IValueSerializerContext context) => ((ImageContainer)value).ToBase64();

    public override object ConvertFromString(string value, IValueSerializerContext context) => ImageContainer.FromBase64(value);
}
