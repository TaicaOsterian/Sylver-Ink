using SylverInk.XAML;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SylverInk.Interop;

public class ImageContainer : Image, IDisposable
{
    private readonly List<BitmapFrame> _compositedFrames = [];
    private long _currentDelay;
    private readonly DispatcherTimer _delayTimer;
    private bool _disposed;
    private string _format = "image/png";
    private int _frameIndex;
    private readonly List<long> _frameTiming = [];
    private Window? _parentWindow;
    private readonly List<BitmapFrame> _rawFrames = [];
    private long _ticksElapsed;

    public List<BitmapFrame> CompositedFrames
    {
        get => _compositedFrames;
    }

    public long CurrentDelay
    {
        get => _currentDelay;
        private set
        {
            _currentDelay = value;
        }
    }
    public DispatcherTimer DelayTimer
    {
        get => _delayTimer;
    }
    public string Format
    {
        get => _format;
        private set
        {
            _format = value;
        }
    }

    /// <summary>
    /// The setter for this property explicitly computes the given value modulo the number of frames.
    /// </summary>
    public int FrameIndex
    {
        get => _frameIndex;
        set
        {
            _frameIndex = value % CompositedFrames.Count;
            CurrentDelay = FrameTiming[_frameIndex];
            Source = CompositedFrames[_frameIndex];
            TicksElapsed = DateTime.UtcNow.Ticks;
        }
    }

    public List<long> FrameTiming
    {
        get => _frameTiming;
    }

    public Window? ParentWindow
    {
        get => _parentWindow;
        set
        {
            _parentWindow = value;
        }
    }

    public List<BitmapFrame> RawFrames
    {
        get => _rawFrames;
    }

    public long TicksElapsed
    {
        get => _ticksElapsed;
        set
        {
            _ticksElapsed = value;
        }
    }

    public ImageContainer() : base()
    {
        _delayTimer = new(DispatcherPriority.Background);
        _delayTimer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow.Ticks - TicksElapsed < CurrentDelay)
                return;

            FrameIndex++;
        };
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
            decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }
        else
        {
            char delim = '\0';
            while (delim != ',')
                delim = (char)stream.ReadByte();

            decoder = dataID switch
            {
                "image/bmp" => new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
                "image/gif" => new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
                "image/jpeg" => new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
                "image/png" => new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
                "image/tiff" => new TiffBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
                _ => BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
            };
        }

        for (int i = 0; i < decoder.Frames.Count; i++)
        {
            var frame = decoder.Frames[i];
            var rawFrame = BitmapFrame.Create(frame);

            if (decoder.CodecInfo.SupportsAnimation && rawFrame.Metadata is BitmapMetadata metadata)
            {
                try
                {
                    ushort rawDelay = (ushort)metadata.GetQuery("/grctlext/Delay");
                    container.FrameTiming.Add(rawDelay * 100000); // GIF delay provided in centiseconds, multipled by ten for milliseconds, multipled by 10^4 for 100-nanosecond intervals (i.e. ticks).
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
            }

            container.RawFrames.Add(rawFrame);
        }

        if (decoder.CodecInfo.SupportsAnimation)
        {
            try
            {
                WriteableBitmap bitmap = new(decoder.Frames[0]);
                bitmap.Lock();
                for (int i = 1; i < decoder.Frames.Count; i++)
                {
                    if (decoder.Frames[i].Metadata is not BitmapMetadata metadata)
                        continue;

                    var frame = new FormatConvertedBitmap(decoder.Frames[i], PixelFormats.Pbgra32, null, 0);

                    object? mTransparency = metadata.GetQuery("/grctlext/TransparencyFlag");
                    object? mTransparentColorIndex = metadata.GetQuery("/grctlext/TransparentColorIndex");
                    object? mLeft = metadata.GetQuery("/imgdesc/Left");
                    object? mTop = metadata.GetQuery("/imgdesc/Top");
                    object? mWidth = metadata.GetQuery("/logscrdesc/Width");
                    object? mHeight = metadata.GetQuery("/logscrdesc/Height");
                    object? mDisposal = metadata.GetQuery("/grctlext/Disposal");

                    bool transparency = (bool)(mTransparency ?? false);
                    byte transparentColorIndex = (byte)(mTransparentColorIndex ?? 0);
                    ushort left = (ushort)(mLeft ?? 0);
                    ushort top = (ushort)(mTop ?? 0);
                    int width = (int)(mWidth ?? frame.PixelWidth);
                    int height = (int)(mHeight ?? frame.PixelHeight);
                    byte disposal = (byte)(mDisposal ?? 0);

                    int[] pixels = new int[width * 4 * height];
                    frame.CopyPixels(new(left, top, width, height), pixels, width * 4, 0);
                    bitmap.WritePixels(new(left, top, width, height), pixels, width * 4, 0);
                }
                bitmap.Unlock();

                container.CompositedFrames.Add(BitmapFrame.Create(bitmap));
            }
            catch (Exception ex)
            {
                App.LogException(ex);
            }
        }

        container.BeginInit();
        container.Format = decoder.CodecInfo.MimeTypes.Split(',')[0];
        container.Margin = new Thickness(5, 0, 5, 0);
        container.Source = container.CompositedFrames[0];
        container.Stretch = Stretch.UniformToFill;
        container.EndInit();

        if (container.Format.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            RenderOptions.SetBitmapScalingMode(container, BitmapScalingMode.LowQuality);
            container.Loaded += OnLoaded;
            container.Unloaded += OnUnloaded;
        }

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

        _compositedFrames.Clear();
        _delayTimer.Stop();
        _rawFrames.Clear();
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public void EvaluateVisibility()
    {
        if (!IsVisible || _parentWindow == null || ActualWidth == 0 || ActualHeight == 0)
        {
            _delayTimer.Stop();
            return;
        }

        try
        {
            GeneralTransform transform = TransformToAncestor(_parentWindow);
            Rect controlBounds = transform.TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
            Rect windowBounds = new(0, 0, _parentWindow.ActualWidth, _parentWindow.ActualHeight);

            if (windowBounds.IntersectsWith(controlBounds))
                _delayTimer.Start();
            else
                _delayTimer.Stop();
        }
        catch (InvalidOperationException)
        {
            _delayTimer.Stop();
        }
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

    public static void OnLoaded(object? sender, EventArgs e)
    {
        if (sender is not ImageContainer container)
            return;

        container.ParentWindow = Window.GetWindow(container);
        if (container.ParentWindow != null)
            container.ParentWindow.LayoutUpdated += ParentWindow_LayoutUpdated;

        container.EvaluateVisibility();
    }

    private static void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not ImageContainer container)
            return;

        container.DelayTimer.Stop();

        if (container.ParentWindow is null)
            return;

        container.ParentWindow.LayoutUpdated -= ParentWindow_LayoutUpdated;
        container.ParentWindow = null;
    }

    private static void ParentWindow_LayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not ImageContainer container)
            return;

        container.EvaluateVisibility();
    }

    private static void PatchGifDelays(byte[] gifData, List<long> frameTimings)
    {
        int frameIndex = 0;
        for (int i = 0; i < gifData.Length - 7; i++)
        {
            if (gifData[i] != 0x21 || gifData[i + 1] != 0xF9 || gifData[i + 2] != 0x04)
                continue;

            if (frameIndex < frameTimings.Count)
            {
                ushort delay = (ushort)(frameTimings[frameIndex] / 100000);
                gifData[i + 4] = (byte)(delay & 0xFF);
                gifData[i + 5] = (byte)(delay >> 8);
                frameIndex++;
            }

            i += 6;
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

        foreach (BitmapFrame frame in _rawFrames)
            encoder.Frames.Add(BitmapFrame.Create(
                source: frame,
                thumbnail: frame.Thumbnail,
                metadata: (BitmapMetadata)frame.Metadata.Clone(),
                colorContexts: frame.ColorContexts));

        encoder.Save(stream);
        byte[] result = stream.ToArray();
        if (Format.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
            PatchGifDelays(result, FrameTiming);
        return result;
    }
}
