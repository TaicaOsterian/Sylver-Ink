using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SylverInk.XAMLUtils;

/// <summary>
/// Custom dependency property handlers for specific needs in regards to bindings that require special support.
/// </summary>
public class RichTextBoxUtils
{
    public static readonly DependencyProperty BoundCaretProperty =
        DependencyProperty.RegisterAttached(
            "BoundCaret",
            typeof(TextPointer),
            typeof(RichTextBoxUtils),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundCaretPositionChanged));

    public static TextPointer GetBoundCaret(DependencyObject source) => (TextPointer)source.GetValue(BoundCaretProperty);
    public static void SetBoundCaret(DependencyObject source, TextPointer value) => source.SetValue(BoundCaretProperty, value);

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.RegisterAttached(
            "Document",
            typeof(FlowDocument),
            typeof(RichTextBoxUtils),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDocumentChanged));

    public static FlowDocument GetDocument(DependencyObject source) => (FlowDocument)source.GetValue(DocumentProperty);
    public static void SetDocument(DependencyObject source, FlowDocument value) => source.SetValue(DocumentProperty, value);

    public static readonly DependencyProperty ObserveCaretProperty =
        DependencyProperty.RegisterAttached(
            "ObserveCaret",
            typeof(bool),
            typeof(RichTextBoxUtils),
            new PropertyMetadata(false, OnObserveCaretChanged));

    public static bool GetObserveCaret(DependencyObject source) => (bool)source.GetValue(ObserveCaretProperty);
    public static void SetObserveCaret(DependencyObject source, bool value) => source.SetValue(ObserveCaretProperty, value);

    private static void OnDocumentChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not RichTextBox rtb || e.NewValue is not FlowDocument document)
            return;

        rtb.Dispatcher.BeginInvoke(new Action(() =>
        {
            rtb.Document = document;
            rtb.CaretPosition = document.ContentStart;
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private static void OnObserveCaretChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not RichTextBox rtb)
            return;

        if ((bool)e.NewValue)
            rtb.SelectionChanged += RichTextBox_SelectionChanged;
        else
            rtb.SelectionChanged -= RichTextBox_SelectionChanged;
    }

    private static void RichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not RichTextBox rtb)
            return;

        if (GetBoundCaret(rtb) == rtb.CaretPosition)
            return;

        SetBoundCaret(rtb, rtb.CaretPosition);
    }

    private static void OnBoundCaretPositionChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
    {
        if (source is not RichTextBox rtb || e.NewValue is not TextPointer newCaret)
            return;

        if (newCaret.Parent != rtb.Document)
            return;

        if (rtb.CaretPosition == newCaret)
            return;

        rtb.Dispatcher.BeginInvoke(new Action(() =>
        {
            rtb.CaretPosition = newCaret;
            rtb.Focus();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}
