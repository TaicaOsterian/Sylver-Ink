using SylverInk.Mvvm;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for StickyNote.xaml
/// </summary>
public sealed partial class StickyNote : Popout
{
    internal override StickyNoteViewModel ViewModel => (StickyNoteViewModel)ViewModelProperty;

    internal StickyNote()
    {
        DataContext = new StickyNoteViewModel();
        base.Construct((StickyNoteViewModel)DataContext);
        ViewModel.ForceClose += (_, _) => HandleClose(true);
        ViewModel.RequestClose += (_, _) => HandleClose();
        InitializeComponent();
    }

    internal void Construct()
    {
        ViewModel.Construct();
        OpenQueries.Add(this);
    }

    private void DetectImageDoubleClick(object sender, RoutedEventArgs e) => ImageContainer.DetectImageDoubleClick(sender, e);

    private void HandleClose(bool force = false)
    {
        if (ViewModel.Edited && !force && ViewModel.ConfirmExit())
            return;

        ViewModel.Deconstruct();

        if (force)
            NoteBox.Document = new(); // This window has to decouple from the underlying document before that document can be assigned to another parent.
        else
            ViewModel.Dispose();

        OpenQueries.Remove(this);
        Close();
    }

    private void ResultBlock_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ViewModel.TextChanged();

        var caret = RichTextBoxUtils.GetDocumentCaret(NoteBox.Document);
        if (caret != 0)
            RichTextBoxUtils.SetDocumentCaret(NoteBox, caret); // Raise the event and let it fully go through once the document's parent reference is set.
    }

    internal void RequestClose(NoteRecord? source = null)
    {
        if (source is not null && !ViewModel.Record.Equals(source))
            return;

        HandleClose(true);
    }

    internal bool RequestOpen(NoteRecord source)
    {
        if (!ViewModel.Record.Equals(source))
            return false;

        Activate();
        Focus();

        return true;
    }

    internal void RequestUnlock(NoteRecord record) => ViewModel.RequestUnlock(record);

    private void RichTextBox_Pasting(object? sender, DataObjectPastingEventArgs e) => RichTextBoxUtils.HandlePaste(sender, e);

    protected override void WindowLoaded(object? sender, RoutedEventArgs e)
    {
        Construct();
        base.WindowLoaded(sender, e);
    }
}
