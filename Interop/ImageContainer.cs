using SylverInk.FileIO;
using SylverInk.XAML;
using System.ComponentModel;
using System.Text;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SylverInk.Interop;

[ValueSerializer(typeof(ImageContainerValueSerializer))]
public class ImageContainer : Image, IDisposable
{
    private readonly List<BitmapFrame> _compositedFrames = [];
    private long _currentDelay;
    private readonly DispatcherTimer _delayTimer;
    private bool _disposed;
    private string _format = "image/png";
    private int _frameIndex;
    private readonly List<long> _frameTiming = [];
    private long _lastVisibilityCheck;
    private Window? _parentWindow;
    private readonly List<BitmapFrame> _rawFrames = [];
    private long _ticksElapsed;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<BitmapFrame> CompositedFrames
    {
        get => _compositedFrames;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long CurrentDelay
    {
        get => _currentDelay;
        private set
        {
            _currentDelay = value;
            _delayTimer.Interval = TimeSpan.FromTicks(Math.Max(1, value / 4));
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DispatcherTimer DelayTimer
    {
        get => _delayTimer;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<long> FrameTiming
    {
        get => _frameTiming;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Window? ParentWindow
    {
        get => _parentWindow;
        set
        {
            _parentWindow = value;
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<BitmapFrame> RawFrames
    {
        get => _rawFrames;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long TicksElapsed
    {
        get => _ticksElapsed;
        set
        {
            _ticksElapsed = value;
        }
    }

    private EventHandler? _layoutHandler;

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

    private ImageContainer Clone()
    {
        using MemoryStream stream = new(ToByteArray());
        return Decode(stream);
    }

    private void CompositeFrames(GifBitmapDecoder decoder)
    {
        if (decoder.Frames.Count == 0)
            return;

        BitmapMetadata firstMeta = (BitmapMetadata)decoder.Frames[0].Metadata;
        int screenWidth = BitmapUtils.GetQuery(firstMeta, "/logscrdesc/Width", (ushort)decoder.Frames[0].PixelWidth);
        int screenHeight = BitmapUtils.GetQuery(firstMeta, "/logscrdesc/Height", (ushort)decoder.Frames[0].PixelHeight);

        if (screenWidth <= 0) screenWidth = decoder.Frames[0].PixelWidth;
        if (screenHeight <= 0) screenHeight = decoder.Frames[0].PixelHeight;

        // Make sure the canvas covers every frame, even on non-compliant GIFs or when /logscrdesc metadata is unavailable.
        for (int i = 0; i < decoder.Frames.Count; i++)
        {
            BitmapMetadata? m = decoder.Frames[i].Metadata as BitmapMetadata;
            int l = BitmapUtils.GetQuery<ushort>(m, "/imgdesc/Left", 0);
            int t = BitmapUtils.GetQuery<ushort>(m, "/imgdesc/Top", 0);
            screenWidth = Math.Max(screenWidth, l + decoder.Frames[i].PixelWidth);
            screenHeight = Math.Max(screenHeight, t + decoder.Frames[i].PixelHeight);
        }

        byte bgIndex = BitmapUtils.GetQuery<byte>(firstMeta, "/logscrdesc/BackgroundColorIndex", 0);

        WriteableBitmap canvas = new(screenWidth, screenHeight, 96, 96, PixelFormats.Pbgra32, null);
        int screenStride = screenWidth * 4;
        byte[]? savedState = null;

        for (int i = 0; i < decoder.Frames.Count; i++)
        {
            BitmapFrame frame = decoder.Frames[i];
            BitmapMetadata? metadata = frame.Metadata as BitmapMetadata;

            int left = BitmapUtils.GetQuery<ushort>(metadata, "/imgdesc/Left", 0);
            int top = BitmapUtils.GetQuery<ushort>(metadata, "/imgdesc/Top", 0);
            byte disposal = BitmapUtils.GetQuery<byte>(metadata, "/grctlext/Disposal", 0);
            if (disposal == 0) disposal = 1;
            bool hasTransparency = BitmapUtils.GetQuery(metadata, "/grctlext/TransparencyFlag", false);
            byte transparentIndex = BitmapUtils.GetQuery<byte>(metadata, "/grctlext/TransparentColorIndex", 0);

            BitmapPalette? palette = frame.Palette;
            Color bgColor = hasTransparency && palette != null && bgIndex < palette.Colors.Count
                ? palette.Colors[bgIndex]
                : Colors.Transparent;

            int fw = frame.PixelWidth;
            int fh = frame.PixelHeight;

            // Clip the frame's draw rect to the canvas.
            int drawLeft = Math.Max(0, left);
            int drawTop = Math.Max(0, top);
            int drawRight = Math.Min(screenWidth, left + fw);
            int drawBottom = Math.Min(screenHeight, top + fh);
            int drawWidth = drawRight - drawLeft;
            int drawHeight = drawBottom - drawTop;

            if (drawWidth <= 0 || drawHeight <= 0)
            {
                // Entirely off-canvas; still emit a composited frame.
                CompositedFrames.Add(BitmapFrame.Create(BitmapUtils.Snapshot(canvas)));
                if (disposal == 3)
                {
                    savedState ??= new byte[screenStride * screenHeight];
                    canvas.CopyPixels(savedState, screenStride, 0);
                }
                continue;
            }

            // Offset of the visible region within the frame's own pixel buffer.
            int srcX = drawLeft - left;
            int srcY = drawTop - top;
            var rect = new Int32Rect(drawLeft, drawTop, drawWidth, drawHeight);

            int regionStride = drawWidth * 4;
            byte[] blended = new byte[regionStride * drawHeight];
            int needed = regionStride * drawHeight;

            if (blended.Length < needed)
                blended = new byte[needed];
            else if (needed < blended.Length)
                Array.Clear(blended, 0, needed);

            canvas.CopyPixels(rect, blended, regionStride, 0);

            if (frame.Format == PixelFormats.Indexed8 && frame.Palette is { } pal)
            {
                byte[] indices = new byte[fw * fh];
                frame.CopyPixels(indices, fw, 0);
                IList<Color> colors = pal.Colors;

                for (int y = 0; y < drawHeight; y++)
                {
                    for (int x = 0; x < drawWidth; x++)
                    {
                        byte idx = indices[(y + srcY) * fw + (x + srcX)];
                        if (hasTransparency && idx == transparentIndex)
                            continue;
                        Color c = idx < colors.Count ? colors[idx] : Colors.Black;
                        int d = y * regionStride + x * 4;
                        blended[d] = c.B;
                        blended[d + 1] = c.G;
                        blended[d + 2] = c.R;
                        blended[d + 3] = 255;
                    }
                }
            }
            else
            {
                // Frame is not Indexed8. GIFs always should be, so this is defensive.
                int bytesPerPixel = (frame.Format.BitsPerPixel + 7) / 8;
                int frameStride = fw * bytesPerPixel;
                byte[] src = new byte[frameStride * fh];
                frame.CopyPixels(src, frameStride, 0);

                for (int y = 0; y < drawHeight; y++)
                {
                    for (int x = 0; x < drawWidth; x++)
                    {
                        int s = (y + srcY) * frameStride + (x + srcX) * bytesPerPixel;
                        if (bytesPerPixel == 4 && src[s + 3] == 0 && hasTransparency)
                            continue;
                        int d = y * regionStride + x * 4;
                        blended[d] = src[s];
                        blended[d + 1] = bytesPerPixel >= 3 ? src[s + 1] : src[s];
                        blended[d + 2] = bytesPerPixel >= 3 ? src[s + 2] : src[s];
                        blended[d + 3] = bytesPerPixel == 4 ? src[s + 3] : (byte)255;
                    }
                }
            }

            // Capture the pre-draw state before we write the frame.
            if (disposal == 3)
            {
                savedState ??= new byte[screenStride * screenHeight];
                canvas.CopyPixels(savedState, screenStride, 0);
            }

            canvas.WritePixels(rect, blended, regionStride, 0);

            CompositedFrames.Add(BitmapFrame.Create(BitmapUtils.Snapshot(canvas)));

            switch (disposal)
            {
                case 2:
                    // Restore only the visible slice of the frame region.
                    if (!hasTransparency)
                    {
                        byte[] fill = new byte[regionStride * drawHeight];
                        for (int p = 0; p < fill.Length; p += 4)
                        {
                            fill[p] = bgColor.B;
                            fill[p + 1] = bgColor.G;
                            fill[p + 2] = bgColor.R;
                            fill[p + 3] = 255;
                        }
                        canvas.WritePixels(new Int32Rect(drawLeft, drawTop, drawWidth, drawHeight),
                                           fill, regionStride, 0);
                    }
                    else
                    {
                        byte[] clear = new byte[regionStride * drawHeight];
                        canvas.WritePixels(new Int32Rect(drawLeft, drawTop, drawWidth, drawHeight),
                                           clear, regionStride, 0);
                    }
                    break;

                case 3:
                    // Restore the entire canvas to the saved state.
                    if (savedState is not null)
                    {
                        canvas.WritePixels(
                            new Int32Rect(0, 0, screenWidth, screenHeight),
                            savedState, screenStride, 0);
                        savedState = null;
                    }
                    break;
            }
        }
    }

    private static ImageContainer Decode(Stream stream, string? format = null)
    {
        ImageContainer container = new();
        BitmapDecoder decoder = format switch
        {
            "image/bmp" => new BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            "image/gif" => new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            "image/jpeg" => new JpegBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            "image/png" => new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            "image/tiff" => new TiffBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad),
            _ => BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
        };

        for (int i = 0; i < decoder.Frames.Count; i++)
        {
            var frame = decoder.Frames[i];
            var rawFrame = BitmapFrame.Create(frame);

            ushort rawDelay = 10; // GIF delay values are measured in centiseconds. Most software applies a default 10-centisecond delay.

            if (decoder.CodecInfo.SupportsAnimation && rawFrame.Metadata is BitmapMetadata metadata)
            {
                object? delayObj = metadata.GetQuery("/grctlext/Delay");
                if (delayObj is ushort us && us > 0)
                    rawDelay = us;
            }

            container.FrameTiming.Add(rawDelay * 100_000L); // Convert centiseconds to 100-nanosecond intervals (i.e. ticks).
            container.RawFrames.Add(rawFrame);
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

        if (container.CompositedFrames.Count == 0 && container.RawFrames.Count > 0)
            container.CompositedFrames.Add(container.RawFrames[0]); // Fallback for non-animated images.

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

    public static ImageContainer FromStream(Stream stream)
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

    public static ImageContainer FromBase64(string data)
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
            encoder.Frames.Add(frame);

        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        byte[] result = buffer.ToArray();

        if (encoder is GifBitmapEncoder)
        {
            List<BitmapMetadata?> rawMetadata = [];
            for (int i = 0; i < _rawFrames.Count; i++)
            {
                var frame = _rawFrames[i];
                if (frame.Metadata is BitmapMetadata metadata)
                    rawMetadata.Add(metadata.Clone());
                else
                    rawMetadata.Add(null);
            }
            result = BitmapUtils.RewriteGif(result, FrameTiming, rawMetadata);
        }

        return result;
    }
}
