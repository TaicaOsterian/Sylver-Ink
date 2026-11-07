using SylverInk.XAML;
using System.Text;
using System.Windows.Media.Imaging;

namespace SylverInk.Interop;

public class ImageContainer : Image, IDisposable
{
    private bool _disposed;
    private string _format = "image/png";
    private readonly List<BitmapSource> _frames = [];

    public string Format
    {
        get => _format;
        private set
        {
            _format = value;
        }
    }

    public List<BitmapSource> Frames
    {
        get => _frames;
    }

    public ImageContainer() : base()
    {
    }

    private static BitmapSource BitmapSourceClone(BitmapSource source)
    {
        var stride = source.PixelWidth * 4;
        byte[] pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, source.Format, source.Palette, pixels, stride);
    }

    private static ImageContainer Decode(Stream stream)
    {
        ImageContainer container = new();
        BitmapDecoder decoder;
        byte[] buffer = new byte[10];

        stream.Seek(0, SeekOrigin.Begin);
        stream.ReadExactly(buffer, 0, 10);
        stream.Seek(0, SeekOrigin.Begin);

        var dataID = Encoding.UTF8.GetString(buffer);
        if (dataID.EndsWith(';'))
            dataID = dataID[..^1];

        if (!dataID.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || dataID.StartsWith("application/", StringComparison.OrdinalIgnoreCase))
        {
            decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        else
        {
            char delim = '\0';
            while (delim != ',')
                delim = (char)stream.ReadByte();

            decoder = dataID switch
            {
                "image/bmp" => new BmpBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad),
                "image/gif" => new GifBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad),
                "image/jpeg" => new JpegBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad),
                "image/png" => new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad),
                "image/tiff" => new TiffBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad),
                _ => BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
            };
        }

        for (int i = 0; i < decoder.Frames.Count; i++)
        {
            using MemoryStream frameStream = new();
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[i]));
            encoder.Save(frameStream);

            frameStream.Position = 0;
            BitmapImage image = new();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = frameStream;
            image.EndInit();
            image.Freeze();

            container.Frames.Add(image);
        }

        container.BeginInit();
        container.Format = ImageMimeType.GetImageMimeType(decoder.CodecInfo.FileExtensions);
        container.Margin = new Thickness(5, 0, 5, 0);
        container.Source = container.Frames[0];
        container.Stretch = Stretch.UniformToFill;
        container.EndInit();

        return container;
    }

    public static void DetectImageDoubleClick(object sender, RoutedEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;

        while (source != null && source != sender)
        {
            if (source is Image img)
            {
                ExpandImage(img.Source);
                e.Handled = true;
                return;
            }

            try
            {
                source = VisualTreeHelper.GetParent(source);
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                return;
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _frames.Clear();
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private static void ExpandImage(ImageSource source)
    {
        ImageView view = new();
        view.Construct(source);
        view.Show();
    }

    public static ImageContainer FromStream(Stream stream)
    {
        using StreamReader reader = new(stream);
        return FromBase64(reader.ReadToEnd());
    }

    public static ImageContainer FromBase64(string data)
    {
        try
        {
            var formattedData = data;
            if (formattedData.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || formattedData.StartsWith("application/", StringComparison.OrdinalIgnoreCase))
                formattedData = formattedData.Split(',')[1];
            using MemoryStream stream = new(Convert.FromBase64String(formattedData));
            return Decode(stream);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return new();
        }
    }

    public static ImageContainer Load(string filename)
    {
        try
        {
            using Stream imageStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (!imageStream.CanRead)
                return new();

            if (imageStream.Length == 0)
                return new();

            return Decode(imageStream);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return new();
        }
    }

    public string ToBase64()
    {
        var sb = new StringBuilder();

        sb.Append(Format)
            .Append(";base64,")
            .Append(Convert.ToBase64String(ToByteArray()));

        return sb.ToString();
    }

    public byte[] ToByteArray()
    {
        using MemoryStream stream = new();

        BitmapEncoder encoder = Format switch
        {
            "image/bmp" => new BmpBitmapEncoder(),
            "image/gif" => new GifBitmapEncoder(),
            "image/jpeg" => new JpegBitmapEncoder() { QualityLevel = 100 },
            "image/png" => new PngBitmapEncoder() { Interlace = PngInterlaceOption.On, },
            "image/tiff" => new TiffBitmapEncoder() { Compression = TiffCompressOption.Lzw },
            _ => new PngBitmapEncoder() { Interlace = PngInterlaceOption.On, }
        };

        foreach (BitmapSource source in _frames)
            encoder.Frames.Add(BitmapFrame.Create(BitmapSourceClone(source)));

        encoder.Save(stream);
        return stream.ToArray();
    }
}
