using SylverInk.FileIO;
using SylverInk.Interop;
using SylverInk.XAML;
using System.ComponentModel;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SylverInk.Mvvm;

[ValueSerializer(typeof(ImageContainerValueSerializer))]
public sealed class ImageContainer : Image, IDisposable
{
    private string? _cachedBase64;
    private readonly List<BitmapSource> _compositedFrames = [];
    private long _currentDelay;
    private readonly DispatcherTimer _delayTimer;
    private bool _disposed;
    private string _format = "image/png";
    private int _frameIndex;
    private readonly List<long> _frameTiming = [];
    private long _lastVisibilityCheck;
    private const int _maxDisplayDimension = 2048;
    private const int _maxCompositeDimension = 1024;
    private byte[] _originalBytes = [];
    private Window? _parentWindow;
    private readonly List<BitmapFrame> _rawFrames = [];
    private long _ticksElapsed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal List<BitmapSource> CompositedFrames
    {
        get => _compositedFrames;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal long CurrentDelay
    {
        get => _currentDelay;
        private set
        {
            _currentDelay = value;
            _delayTimer.Interval = TimeSpan.FromTicks(Math.Max(1, value / 4));
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal DispatcherTimer DelayTimer
    {
        get => _delayTimer;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string Format
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int FrameIndex
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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal List<long> FrameTiming
    {
        get => _frameTiming;
    }

    internal byte[] OriginalBytes
    {
        get => _originalBytes;
        private set => _originalBytes = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Window? ParentWindow
    {
        get => _parentWindow;
        set
        {
            _parentWindow = value;
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal List<BitmapFrame> RawFrames
    {
        get => _rawFrames;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal long TicksElapsed
    {
        get => _ticksElapsed;
        set
        {
            _ticksElapsed = value;
        }
    }

    private EventHandler? _layoutHandler;

    internal ImageContainer() : base()
    {
        _delayTimer = new(DispatcherPriority.Background);
        _delayTimer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow.Ticks - TicksElapsed < CurrentDelay)
                return;

            FrameIndex++;
        };
    }

    private ImageContainer Clone()
    {
        using MemoryStream stream = new(ToByteArray(), writable: false);
        return Decode(stream, _format);
    }

    private void CompositeFrames(GifBitmapDecoder decoder)
    {
        if (decoder.Frames.Count == 0)
            return;

        BitmapMetadata? firstMeta = decoder.Frames[0].Metadata as BitmapMetadata;
        int screenWidth = BitmapUtils.GetQuery(firstMeta, "/logscrdesc/Width", (ushort)decoder.Frames[0].PixelWidth);
        int screenHeight = BitmapUtils.GetQuery(firstMeta, "/logscrdesc/Height", (ushort)decoder.Frames[0].PixelHeight);
        if (screenWidth <= 0)
            screenWidth = decoder.Frames[0].PixelWidth;
        if (screenHeight <= 0)
            screenHeight = decoder.Frames[0].PixelHeight;

        for (int i = 0; i < decoder.Frames.Count; i++)
        {
            var metadata = decoder.Frames[i].Metadata as BitmapMetadata;
            int left = BitmapUtils.GetQuery<ushort>(metadata, "/imgdesc/Left", 0);
            int top = BitmapUtils.GetQuery<ushort>(metadata, "/imgdesc/Top", 0);
            screenWidth = Math.Max(screenWidth, left + decoder.Frames[i].PixelWidth);
            screenHeight = Math.Max(screenHeight, top + decoder.Frames[i].PixelHeight);
        }

        byte bgIndex = BitmapUtils.GetQuery<byte>(firstMeta, "/logscrdesc/BackgroundColorIndex", 0);

        int largestDim = Math.Max(screenWidth, screenHeight);
        double scale = largestDim > _maxCompositeDimension ? (double)_maxCompositeDimension / largestDim : 1.0;
        int canvasWidth = Math.Max(1, (int)Math.Round(screenWidth * scale));
        int canvasHeight = Math.Max(1, (int)Math.Round(screenHeight * scale));

        var canvas = new WriteableBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32, null);
        int canvasStride = canvasWidth * 4;
        int canvasByteCount = canvasStride * canvasHeight;

        byte[] fillBuffer = new byte[canvasByteCount];
        byte[] frameIndices = new byte[screenWidth * screenHeight];
        byte[] framePixels = new byte[screenWidth * screenHeight * 4];
        byte[] paletteBuffer = new byte[256 * 4];
        byte[] regionBlend = new byte[canvasByteCount];
        byte[] snapshotBuffer = new byte[canvasByteCount];

        for (int i = 0; i < decoder.Frames.Count; i++)
        {
            var frame = decoder.Frames[i];
            var metadata = frame.Metadata as BitmapMetadata;

            int left = BitmapUtils.GetQuery<ushort>(metadata, "/imgdesc/Left", 0);
            int top = BitmapUtils.GetQuery<ushort>(metadata, "/imgdesc/Top", 0);
            byte disposal = BitmapUtils.GetQuery<byte>(metadata, "/grctlext/Disposal", 0);
            if (disposal == 0)
                disposal = 1;
            bool hasTransparency = BitmapUtils.GetQuery(metadata, "/grctlext/TransparencyFlag", false);
            byte transparentIndex = BitmapUtils.GetQuery<byte>(metadata, "/grctlext/TransparentColorIndex", 0);

            BitmapPalette? palette = frame.Palette;
            Color bgColor = hasTransparency && palette != null && bgIndex < palette.Colors.Count
                ? palette.Colors[bgIndex]
                : Colors.Transparent;

            int fw = frame.PixelWidth;
            int fh = frame.PixelHeight;

            int clipLeft = Math.Max(0, left);
            int clipTop = Math.Max(0, top);
            int clipRight = Math.Min(screenWidth, left + fw);
            int clipBottom = Math.Min(screenHeight, top + fh);
            int clipWidth = clipRight - clipLeft;
            int clipHeight = clipBottom - clipTop;

            // Disposal = 3 captures the pre-draw state, full canvas.
            if (disposal == 3)
                canvas.CopyPixels(snapshotBuffer, canvasStride, 0);

            if (clipWidth > 0 && clipHeight > 0)
            {
                int srcX = clipLeft - left;
                int srcY = clipTop - top;

                // Map clipped region onto scaled canvas.
                int dstLeft = Math.Clamp((int)Math.Round(clipLeft * scale), 0, canvasWidth);
                int dstTop = Math.Clamp((int)Math.Round(clipTop * scale), 0, canvasHeight);
                int dstRight = Math.Clamp((int)Math.Round(clipRight * scale), dstLeft, canvasWidth);
                int dstBottom = Math.Clamp((int)Math.Round(clipBottom * scale), dstTop, canvasHeight);
                int dstWidth = dstRight - dstLeft;
                int dstHeight = dstBottom - dstTop;

                if (dstWidth > 0 && dstHeight > 0)
                {
                    int dstStride = dstWidth * 4;
                    bool indexed8 = frame.Format == PixelFormats.Indexed8 && palette != null;
                    int bpp = indexed8 ? (frame.Format.BitsPerPixel + 7) / 8 : 1;
                    int frameStride = fw * bpp;
                    int colorCount = 0;

                    // Opaque frames overwrite every pixel, so skip the expensive canvas read entirely.
                    if (hasTransparency)
                        canvas.CopyPixels(new Int32Rect(dstLeft, dstTop, dstWidth, dstHeight), regionBlend, dstStride, 0);

                    if (indexed8)
                    {
                        IList<Color> colors = palette!.Colors;
                        colorCount = colors.Count;

                        // Precompute palette in BGRA bytes once per frame; avoids Color struct lookups per pixel.
                        for (int c = 0; c < colorCount; c++)
                        {
                            Color color = colors[c];
                            int p = c * 4;
                            paletteBuffer[p] = color.B;
                            paletteBuffer[p + 1] = color.G;
                            paletteBuffer[p + 2] = color.R;
                            paletteBuffer[p + 3] = 255;
                        }

                        frame.CopyPixels(frameIndices, frameStride, 0);
                    }
                    else
                    {
                        frame.CopyPixels(framePixels, frameStride, 0);
                    }

                    for (int dy = 0; dy < dstHeight; dy++)
                    {
                        int sy = srcY + (int)((long)dy * clipHeight / dstHeight);
                        int srcRow = sy * frameStride;
                        int dstRow = dy * dstStride;

                        for (int dx = 0; dx < dstWidth; dx++)
                        {
                            int sx = srcX + (int)((long)dx * clipWidth / dstWidth);
                            int d = dstRow + dx * 4;
                            int s = srcRow + sx * bpp;

                            if (bpp == 4 && hasTransparency && framePixels[s + 3] == 0)
                                continue;

                            if (indexed8)
                            {
                                // Nearest-neighbour downsample during the blit.
                                byte idx = frameIndices[s];

                                if (hasTransparency && idx == transparentIndex)
                                    continue;

                                if (idx >= colorCount)
                                    continue;

                                s = idx * 4;
                                regionBlend[d] = paletteBuffer[s];
                                regionBlend[d + 1] = paletteBuffer[s + 1];
                                regionBlend[d + 2] = paletteBuffer[s + 2];
                                regionBlend[d + 3] = 255;
                            }
                            else
                            {
                                // Frame is not Indexed8. GIFs always should be, so this is defensive.
                                regionBlend[d] = framePixels[s];
                                regionBlend[d + 1] = bpp >= 3 ? framePixels[s + 1] : framePixels[s];
                                regionBlend[d + 2] = bpp >= 3 ? framePixels[s + 2] : framePixels[s];
                                regionBlend[d + 3] = bpp == 4 ? framePixels[s + 3] : (byte)255;
                            }
                        }
                    }

                    canvas.WritePixels(new Int32Rect(dstLeft, dstTop, dstWidth, dstHeight), regionBlend, dstStride, 0);
                }
            }

            CompositedFrames.Add(BitmapUtils.Snapshot(canvas));

            // Disposal semantics (region recomputed in canvas space).
            switch (disposal)
            {
                case 2:
                    // Restore only the visible slice of the frame region.
                    int dstLeft = Math.Clamp((int)Math.Round(clipLeft * scale), 0, canvasWidth);
                    int dstTop = Math.Clamp((int)Math.Round(clipTop * scale), 0, canvasHeight);
                    int dstRight = Math.Clamp((int)Math.Round(clipRight * scale), dstLeft, canvasWidth);
                    int dstBottom = Math.Clamp((int)Math.Round(clipBottom * scale), dstTop, canvasHeight);
                    int dstWidth = dstRight - dstLeft;
                    int dstHeight = dstBottom - dstTop;

                    if (dstWidth <= 0 || dstHeight <= 0)
                        break;

                    int needed = dstWidth * 4 * dstHeight;
                    if (hasTransparency)
                    {
                        Array.Clear(fillBuffer, 0, needed);
                        canvas.WritePixels(new Int32Rect(dstLeft, dstTop, dstWidth, dstHeight), fillBuffer, dstWidth * 4, 0);
                        break;
                    }

                    for (int p = 0; p < needed; p += 4)
                    {
                        fillBuffer[p] = bgColor.B;
                        fillBuffer[p + 1] = bgColor.G;
                        fillBuffer[p + 2] = bgColor.R;
                        fillBuffer[p + 3] = 255;
                    }

                    canvas.WritePixels(new Int32Rect(dstLeft, dstTop, dstWidth, dstHeight), fillBuffer, dstWidth * 4, 0);
                    break;
                case 3:
                    // Restore the entire canvas to the snapshot state.
                    canvas.WritePixels(new Int32Rect(0, 0, canvasWidth, canvasHeight), snapshotBuffer, canvasStride, 0);
                    break;
            }
        }
    }

    private static BitmapDecoder CreateDecoder(Stream stream, string? mime) => mime switch
    {
        "image/bmp" => new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
        "image/gif" => new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
        "image/jpeg" => new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
        "image/png" => new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
        "image/tiff" => new TiffBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
        _ => BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
    };

    private static ImageContainer Decode(Stream stream, string? format = null)
    {
        var originalBytes = ReadAllBytes(stream);
        var container = new ImageContainer { OriginalBytes = originalBytes };

        if (originalBytes.Length == 0)
        {
            container.Init();
            return container;
        }

        string mime;
        int srcWidth, srcHeight;
        bool animated;

        using (var probeStream = new MemoryStream(originalBytes, writable: false))
        {
            var probe = BitmapDecoder.Create(probeStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

            mime = !string.IsNullOrWhiteSpace(format) ? format! : probe.CodecInfo.MimeTypes.Split(',')[0];
            animated = probe.CodecInfo.SupportsAnimation && probe.Frames.Count > 1;
            srcWidth = probe.Frames[0].PixelWidth;
            srcHeight = probe.Frames[0].PixelHeight;
        }

        if (!animated)
        {
            var source = DecodeScaled(originalBytes, srcWidth, srcHeight);
            container.CompositedFrames.Add(source);
            container.FrameTiming.Add(1_000_000L); // For a static image, apply the software-default delay of 100 milliseconds (here written as 10^6 ticks) to keep other routines happy.
            container.Init(mime);
            return container;
        }

        using (var decodeStream = new MemoryStream(originalBytes, writable: false))
        {
            var decoder = CreateDecoder(decodeStream, mime);

            for (int i = 0; i < decoder.Frames.Count; i++)
            {
                ushort rawDelay = 10; // GIF frame delays are stored as centiseconds. Most software gives a default of 10 when the file doesn't specify.
                if (decoder.Frames[i].Metadata is BitmapMetadata meta && meta.GetQuery("/grctlext/Delay") is ushort us && us > 0)
                    rawDelay = us;
                container.FrameTiming.Add(rawDelay * 100_000L); // Multiply by ten for milliseconds, then by 10^4 for 100-nanosecond intervals (i.e. ticks).
            }

            if (decoder is GifBitmapDecoder gif)
            {
                try
                {
                    container.CompositeFrames(gif);
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                }
            }

            if (container.CompositedFrames.Count == 0)
                container.CompositedFrames.Add(BitmapUtils.Snapshot(decoder.Frames[0])); // Fallback for non-animated images.
        }

        container.Init(mime);
        RenderOptions.SetBitmapScalingMode(container, BitmapScalingMode.LowQuality);
        container.Loaded += OnLoaded;
        container.Unloaded += OnUnloaded;

        return container;
    }

    private List<BitmapFrame> DecodeFullResolutionFrames()
    {
        if (_originalBytes.Length == 0)
            return [];

        try
        {
            using var stream = new MemoryStream(_originalBytes, writable: false);
            var decoder = CreateDecoder(stream, _format);
            var frames = new List<BitmapFrame>(decoder.Frames.Count);
            foreach (var frame in decoder.Frames)
                frames.Add(frame);
            return frames;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return [];
        }
    }

    private static BitmapImage DecodeScaled(byte[] originalBytes, int srcWidth, int srcHeight)
    {
        bool tooWide = srcWidth > _maxDisplayDimension;
        bool tooTall = srcHeight > _maxDisplayDimension;

        using var stream = new MemoryStream(originalBytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.None;

        // Setting a single dimension preserves aspect ratio and never upscales.
        if (tooWide && srcWidth >= srcHeight)
            image.DecodePixelWidth = _maxDisplayDimension;
        else if (tooTall)
            image.DecodePixelHeight = _maxDisplayDimension;

        image.EndInit();
        image.Freeze();
        return image;
    }

    internal static void DetectImageDoubleClick(object sender, RoutedEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;

        while (source != null && source != sender)
        {
            if (source is ImageContainer container)
            {
                container.ExpandImage();
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

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _delayTimer.Stop();
            _compositedFrames.Clear();
            _frameTiming.Clear();
            _rawFrames.Clear();
            _originalBytes = [];
            _parentWindow = null;
            Source = null;
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal void EvaluateVisibility()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now - _lastVisibilityCheck < 1e6) // At most, refresh ten times per second.
            return;
        _lastVisibilityCheck = now;

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

    private void ExpandImage()
    {
        ImageView view = new();
        view.Construct(this.Clone());
        view.Show();
    }

    internal static ImageContainer FromBase64(string data)
    {
        try
        {
            var dataID = string.Empty;
            if (data.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                dataID = data[..10];
                if (dataID.EndsWith(';'))
                    dataID = dataID[..^1];
                data = data[dataID.Length..];
                if (data.StartsWith(";base64,", StringComparison.OrdinalIgnoreCase))
                    data = data[8..];
            }

            using MemoryStream stream = new(Convert.FromBase64String(data));
            return Decode(stream, dataID);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return new();
        }
    }

    internal static ImageContainer FromSource(BitmapSource source)
    {
        ImageContainer container = new();

        try
        {
            var frame = BitmapFrame.Create(BitmapUtils.Snapshot(source));
            container.RawFrames.Add(frame);
            container.CompositedFrames.Add(frame);
            container.Init();
            return container;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return container;
        }
    }

    internal static ImageContainer FromStream(Stream stream)
    {
        try
        {
            return Decode(stream);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return new();
        }
    }

    private void Init(string? format = null)
    {
        BeginInit();
        Format = format ?? "image/png";
        Margin = new Thickness(5, 0, 5, 0);
        Source = CompositedFrames[0];
        Stretch = Stretch.UniformToFill;
        EndInit();
    }

    internal static ImageContainer Load(string filename)
    {
        try
        {
            var bytes = File.ReadAllBytes(filename);
            using Stream imageStream = new MemoryStream(bytes);

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

    internal static void OnLoaded(object? sender, EventArgs e)
    {
        if (sender is not ImageContainer container)
            return;

        container.ParentWindow = Window.GetWindow(container);
        if (container.ParentWindow != null)
        {
            container._layoutHandler = (_, _) => container.EvaluateVisibility();
            container.ParentWindow.LayoutUpdated += container._layoutHandler;
        }

        container.EvaluateVisibility();
    }

    private static void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not ImageContainer container)
            return;

        container.DelayTimer.Stop();

        if (container.ParentWindow is null)
            return;

        if (container._layoutHandler is not null)
            container.ParentWindow.LayoutUpdated -= container._layoutHandler;

        container._layoutHandler = null;
        container.ParentWindow = null;
    }

    private static void ParentWindow_LayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not ImageContainer container)
            return;

        container.EvaluateVisibility();
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
            stream.Position = 0;
        using var mem = new MemoryStream(stream.CanSeek ? (int)stream.Length : 0);
        stream.CopyTo(mem);
        return mem.ToArray();
    }

    internal string ToBase64() => _cachedBase64 ??= $"{Format};base64,{Convert.ToBase64String(ToByteArray())}";

    internal byte[] ToByteArray(string? exportFormat = null)
    {
        // Same-format request returns the original compressed bytes untouched.
        if (_originalBytes.Length > 0
            && (exportFormat is null || exportFormat.Equals(_format, StringComparison.OrdinalIgnoreCase)))
            return _originalBytes;

        exportFormat ??= _format;
        _cachedBase64 = null;

        // Pasted images have no original bytes; use the frames we hold.
        List<BitmapFrame> frames = _rawFrames.Count > 0 ? _rawFrames : DecodeFullResolutionFrames();

        if (frames.Count == 0)
            return [];

        BitmapEncoder encoder = exportFormat switch
        {
            "image/bmp" => new BmpBitmapEncoder(),
            "image/gif" => new GifBitmapEncoder(),
            "image/jpeg" => new JpegBitmapEncoder() { QualityLevel = 100 },
            "image/png" => new PngBitmapEncoder() { Interlace = PngInterlaceOption.On },
            "image/tiff" => new TiffBitmapEncoder() { Compression = TiffCompressOption.Lzw },
            _ => new PngBitmapEncoder() { Interlace = PngInterlaceOption.On },
        };

        foreach (BitmapFrame frame in frames)
            encoder.Frames.Add(frame);

        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        byte[] result = buffer.ToArray();

        if (encoder is GifBitmapEncoder)
        {
            List<long> timing = FrameTiming;
            if (timing.Count != frames.Count)
            {
                timing = new List<long>(frames.Count);
                for (int i = 0; i < frames.Count; i++)
                    timing.Add(1_000_000L); // 100-ms default for pasted images.
            }

            List<BitmapMetadata?> rawMetadata = new(frames.Count);
            foreach (BitmapFrame frame in frames)
                rawMetadata.Add(frame.Metadata as BitmapMetadata);

            result = BitmapUtils.RewriteGif(result, timing, rawMetadata);
        }

        return result;
    }
}
