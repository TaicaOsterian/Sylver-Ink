using SylverInk.Interop;
using SylverInk.Visuals;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SylverInk.XAMLUtils;

/// <summary>
/// Custom dependency property handlers for specific needs in regards to bindings that require special support.
/// </summary>
public class RichTextBoxUtils
{
    private static RichTextBox? _cachedRTB;
    private static bool _refreshQueued;

    public static readonly DependencyProperty AtomicBlocksProperty =
        DependencyProperty.RegisterAttached(
            "AtomicBlocks",
            typeof(IEnumerable<Block>),
            typeof(RichTextBoxUtils),
            new PropertyMetadata(null, OnAtomicBlocksChanged));

    public static readonly DependencyProperty BoundCaretProperty =
        DependencyProperty.RegisterAttached(
            "BoundCaret",
            typeof(TextPointer),
            typeof(RichTextBoxUtils),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundCaretChanged));

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.RegisterAttached(
            "Document",
            typeof(FlowDocument),
            typeof(RichTextBoxUtils),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDocumentChanged));

    public static readonly DependencyProperty DocumentCaretProperty =
        DependencyProperty.RegisterAttached(
            "DocumentCaret",
            typeof(int),
            typeof(RichTextBoxUtils),
            new PropertyMetadata(0, OnDocumentCaretChanged));

    public static readonly DependencyProperty ObserveCaretProperty =
        DependencyProperty.RegisterAttached(
            "ObserveCaret",
            typeof(bool),
            typeof(RichTextBoxUtils),
            new PropertyMetadata(false, OnObserveCaretChanged));

    public static IEnumerable<Block> GetAtomicBlocks(DependencyObject element) => (IEnumerable<Block>)element.GetValue(AtomicBlocksProperty);

    public static TextPointer GetBoundCaret(DependencyObject source) => (TextPointer)source.GetValue(BoundCaretProperty);

    public static FlowDocument GetDocument(DependencyObject source) => (FlowDocument)source.GetValue(DocumentProperty);

    public static int GetDocumentCaret(DependencyObject source) => (int)source.GetValue(DocumentCaretProperty);

    public static bool GetObserveCaret(DependencyObject source) => (bool)source.GetValue(ObserveCaretProperty);

    public static void HandlePaste(object? sender, DataObjectPastingEventArgs e)
    {
        if (sender is not RichTextBox rtb)
            return;

        if (!e.DataObject.GetDataPresent(DataFormats.Bitmap))
            return;

        e.CancelCommand();

        try
        {
            var src = e.DataObject.GetData(DataFormats.Bitmap, true);

            Image? img = src switch
            {
                _ when src is BitmapSource bitmap => new() { Source = bitmap },
                _ when src is MemoryStream stream => ImageUtils.DecodeEmbed(stream),
                _ => null,
            };

            if (img is null)
                return;

            BlockUIContainer blockContainer = new BlockUIContainer(img);

            TextPointer caretPosition = rtb.CaretPosition;
            Block currentBlock = caretPosition.Paragraph;

            if (currentBlock is null)
                rtb.Document.Blocks.Add(blockContainer);
            else
                rtb.Document.Blocks.InsertAfter(currentBlock, blockContainer);

            rtb.CaretPosition = blockContainer.ElementEnd;
            rtb.Focus();
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }

    private static void OnAtomicBlocksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox rtb || e.NewValue is not IEnumerable<Block> blocks)
            return;

        using (rtb.DeclareChangeBlock())
        {
            rtb.Document.Blocks.Clear();
            rtb.Document.Blocks.AddRange(blocks);
        }
    }

    private static void OnBoundCaretChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not RichTextBox rtb || e.NewValue is not TextPointer newCaret)
            return;

        // If there is a live selection, do not clear it by reassigning the caret.
        if (!rtb.Selection.IsEmpty)
            return;

        if (!newCaret.IsInSameDocument(rtb.Document.ContentStart))
            return;

        if (rtb.CaretPosition == newCaret)
            return;

        rtb.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                rtb.CaretPosition = newCaret;
            }
            catch (Exception e)
            {
                // Rarely, confusion can occur if the user clicks in the box the very moment it opens.
                App.LogException(e);
            }
        }), DispatcherPriority.Background);
    }

    private static void OnDocumentChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not RichTextBox rtb || e.NewValue is not FlowDocument document)
            return;

        if (document.Parent is not null)
            return;

        _cachedRTB = rtb;

        rtb.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                rtb.Document = document;
            }
            catch (Exception e)
            {
                /* Same rare race as with the caret binding. */
                App.LogException(e);
            }
        }), DispatcherPriority.Background);
    }

    private static void OnDocumentCaretChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not RichTextBox rtb || e.NewValue is not int position)
            return;

        _cachedRTB = rtb;

        source.Dispatcher.BeginInvoke(new Action(() =>
        {
            FlowDocumentUtils.ScrollToPosition(rtb.Document, position);
            SetDocumentCaret(rtb.Document, 0);
        }), DispatcherPriority.Background);
    }

    private static void OnObserveCaretChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not RichTextBox rtb)
            return;

        _cachedRTB = rtb;

        if ((bool)e.NewValue)
        {
            rtb.SelectionChanged += RichTextBox_SelectionChanged;
            CommandManager.RequerySuggested += OnRequerySuggested;
        }
        else
        {
            rtb.SelectionChanged -= RichTextBox_SelectionChanged;
            CommandManager.RequerySuggested -= OnRequerySuggested;
        }
    }

    private static void OnRequerySuggested(object? sender, EventArgs e)
    {
        if (_refreshQueued)
            return;

        if (_cachedRTB is null)
            return;

        _refreshQueued = true;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            RefreshToolbarStates(_cachedRTB);
            _refreshQueued = false;
        }), DispatcherPriority.Background);
    }

    /// <summary>
    /// Update each of the editing toolbar buttons to be checked or unchecked depending on the state of the selected text.
    /// </summary>
    private static void RefreshToolbarStates(RichTextBox rtb)
    {
        var window = Window.GetWindow(rtb);
        if (window is null)
            return;

        var selection = rtb.Selection;
        object weight = selection.GetPropertyValue(TextElement.FontWeightProperty);
        object style = selection.GetPropertyValue(TextElement.FontStyleProperty);
        object decoration = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        object alignment = selection.GetPropertyValue(Paragraph.TextAlignmentProperty);
        TextMarkerStyle markerStyle = TextMarkerStyle.None;

        DependencyObject? pointer = selection.Start.Parent;
        while (pointer != null && pointer != rtb)
        {
            if (pointer is List listElement)
            {
                markerStyle = listElement.MarkerStyle;
                break;
            }

            pointer = LogicalTreeHelper.GetParent(pointer) ?? (pointer as FrameworkElement)?.Parent;
        }

        foreach (var button in VisualUtils.FindVisualChildren<ToggleButton>(window))
        {
            if (button.CommandTarget != rtb)
                continue;

            ICommand cmd = button.Command;

            button.IsChecked = cmd switch
            {
                _ when cmd == EditingCommands.AlignCenter => alignment != DependencyProperty.UnsetValue && (TextAlignment)alignment == TextAlignment.Center,
                _ when cmd == EditingCommands.AlignJustify => alignment != DependencyProperty.UnsetValue && (TextAlignment)alignment == TextAlignment.Justify,
                _ when cmd == EditingCommands.AlignLeft => alignment != DependencyProperty.UnsetValue && (TextAlignment)alignment == TextAlignment.Left,
                _ when cmd == EditingCommands.AlignRight => alignment != DependencyProperty.UnsetValue && (TextAlignment)alignment == TextAlignment.Right,
                _ when cmd == EditingCommands.ToggleBold => weight != DependencyProperty.UnsetValue && (FontWeight)weight == FontWeights.Bold,
                _ when cmd == EditingCommands.ToggleBullets => alignment != DependencyProperty.UnsetValue && markerStyle is TextMarkerStyle.Box or TextMarkerStyle.Circle or TextMarkerStyle.Disc or TextMarkerStyle.Square,
                _ when cmd == EditingCommands.ToggleItalic => style != DependencyProperty.UnsetValue && (FontStyle)style == FontStyles.Italic,
                _ when cmd == EditingCommands.ToggleNumbering => alignment != DependencyProperty.UnsetValue && markerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperLatin or TextMarkerStyle.UpperRoman,
                _ when cmd == EditingCommands.ToggleUnderline => decoration != DependencyProperty.UnsetValue && decoration == TextDecorations.Underline,
                _ => false
            };
        }
    }

    private static void RichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not RichTextBox rtb)
            return;

        _cachedRTB = rtb;

        if (GetBoundCaret(rtb) == rtb.CaretPosition)
            return;

        SetBoundCaret(rtb, rtb.CaretPosition);
        RefreshToolbarStates(rtb);
    }

    public static void SetAtomicBlocks(DependencyObject element, IEnumerable<Block> value) => element.SetValue(AtomicBlocksProperty, value);

    public static void SetBoundCaret(DependencyObject source, TextPointer value) => source.SetValue(BoundCaretProperty, value);

    public static void SetDocument(DependencyObject source, FlowDocument value) => source.SetValue(DocumentProperty, value);

    public static void SetDocumentCaret(DependencyObject source, int value) => source.SetValue(DocumentCaretProperty, value);

    public static void SetObserveCaret(DependencyObject source, bool value) => source.SetValue(ObserveCaretProperty, value);
}
