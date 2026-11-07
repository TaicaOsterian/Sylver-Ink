namespace SylverInk.Interop;

public static class ImageMimeType
{
    public const string Bmp = "image/bmp";
    public const string Gif = "image/gif";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Tiff = "image/tiff";
    public const string Fallback = "application/octet-stream";

    public static string GetImageExtension(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return "dat";

        return mimeType.ToLowerInvariant() switch
        {
            Bmp => ".bmp",
            Gif => ".gif",
            Jpeg => ".jpg",
            Png => ".png",
            Tiff => ".tif",
            _ => "dat"
        };
    }

    public static string GetImageMimeType(string filePathOrName)
    {
        if (string.IsNullOrWhiteSpace(filePathOrName))
            return Fallback;

        string extension = Path.GetExtension(filePathOrName);

        return string.IsNullOrWhiteSpace(extension) ? Fallback : extension.ToLowerInvariant() switch
        {
            ".bmp" => Bmp,
            ".gif" => Gif,
            ".jpg" or ".jpeg" => Jpeg,
            ".png" => Png,
            ".tif" or ".tiff" => Tiff,
            _ => Fallback
        };
    }
}
