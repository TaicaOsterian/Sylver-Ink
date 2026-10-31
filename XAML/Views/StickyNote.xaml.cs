using SylverInk.Interop;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for StickyNote.xaml
/// </summary>
public partial class StickyNote : Popout
{
    public override StickyNoteViewModel ViewModel => (StickyNoteViewModel)ViewModelProperty;

    public StickyNote()
    {
        DataContext = new StickyNoteViewModel();
        base.Construct((StickyNoteViewModel)DataContext);
        ViewModel.ForceClose += (_, _) => HandleClose(true);
        ViewModel.RequestClose += (_, _) => HandleClose();
        InitializeComponent();
    }

    public void Construct()
    {
        ViewModel.Construct();
        OpenQueries.Add(this);
    }

    private void DetectImageDoubleClick(object sender, RoutedEventArgs e) => ImageUtils.DetectImageDoubleClick(sender, e);

    private void HandleClose(bool force = false)
    {
        if (ViewModel.Edited && !force && ViewModel.ConfirmExit())
            return;

        ViewModel.Deconstruct();

        if (force)
            NoteBox.Document = new(); // This window has to decouple from the underlying document before that document can be assigned to another parent.

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

    public void RequestClose(NoteRecord? source = null)
    {
        if (source is not null && !ViewModel.Record.Equals(source))
            return;

        HandleClose(true);
    }

    public bool RequestOpen(NoteRecord source)
    {
        if (!ViewModel.Record.Equals(source))
            return false;

        Activate();
        Focus();

        return true;
    }

    public void RequestUnlock(NoteRecord record) => ViewModel.RequestUnlock(record);

    protected override void WindowLoaded(object? sender, RoutedEventArgs e)
    {
        Construct();
        base.WindowLoaded(sender, e);
    }
}