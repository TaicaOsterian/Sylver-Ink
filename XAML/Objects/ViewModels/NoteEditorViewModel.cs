using System.Threading;

namespace SylverInk.XAML.Objects.ViewModels;

public class NoteEditorViewModel : ViewModelBase
{
    private bool _autosaving;
    private TextPointer _caretPosition;
    private FlowDocument _document;
    private bool _edited;
    private bool _finishedLoading;
    private bool _isEnabled = true;
    private string? _lastChange;
    private int _originalBlockCount;
    private string _originalPlaintext = string.Empty;
    private int _originalRevisionCount;
    private string _originalText = string.Empty;
    private NoteRecord _record;
    private DateTime _timeSinceAutosave = DateTime.UtcNow;

    public bool Autosaving
    {
        get => _autosaving;
        set
        {
            _autosaving = value;
            OnPropertyChanged();
        }
    }

    public TextPointer CaretPosition
    {
        get => _caretPosition;
        set
        {
            _caretPosition = value;
            OnPropertyChanged();
        }
    }

    public FlowDocument Document
    {
        get => _document;
        set
        {
            _document = value;
            OnPropertyChanged();
        }
    }

    public bool Edited
    {
        get => _edited;
        set
        {
            _edited = value;
            OnPropertyChanged();
        }
    }

    public bool FinishedLoading
    {
        get => _finishedLoading;
        set
        {
            _finishedLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public string? LastChange
    {
        get => _lastChange;
        set
        {
            _lastChange = value;
            OnPropertyChanged();
        }
    }

    public int OriginalBlockCount
    {
        get => _originalBlockCount;
        set
        {
            _originalBlockCount = value;
            OnPropertyChanged();
        }
    }

    public int OriginalRevisionCount
    {
        get => _originalRevisionCount;
        set
        {
            _originalRevisionCount = value;
            OnPropertyChanged();
        }
    }

    public string OriginalPlaintext
    {
        get => _originalPlaintext;
        set
        {
            _originalPlaintext = value;
            OnPropertyChanged();
        }
    }

    public string OriginalText
    {
        get => _originalText;
        set
        {
            _originalText = value;
            OnPropertyChanged();
        }
    }

    public NoteRecord Record
    {
        get => _record;
        set
        {
            _record = value;
            _document = value.GetDocument();
            OnPropertyChanged(null);
        }
    }

    public DateTime TimeSinceAutosave
    {
        get => _timeSinceAutosave;
        set
        {
            _timeSinceAutosave = value;
            OnPropertyChanged();
        }
    }

    public NoteEditorViewModel()
    {
        _record = new();
        _document = _record.GetDocument();
        _caretPosition = _document.ContentStart;
    }

    private void Autosave()
    {
        if (!FinishedLoading)
            return;

        if (Autosaving)
            return;

        if (!Edited)
            return;

        Autosaving = true;
        Task.Factory.StartNew(() =>
        {
            SpinWait.SpinUntil(() => (DateTime.UtcNow - TimeSinceAutosave).Seconds >= 1);

            Concurrent(Record.Autosave, Document);
            TimeSinceAutosave = DateTime.UtcNow;
            Autosaving = false;
            return;
        }, TaskCreationOptions.LongRunning);
    }

    public bool CalculateIsEdited()
    {
        if (Document.Blocks.Count != OriginalBlockCount)
            return true;

        if (!new TextRange(Document.ContentStart, Document.ContentEnd).Text.Equals(OriginalPlaintext, StringComparison.Ordinal))
            return true;

        return !OriginalText.Equals(TextConverter.Save(Document, TextFormat.Xaml), StringComparison.Ordinal);
    }

    public bool ConfirmExit()
    {
        switch (MessageBox.Show(Strings.ExitMessage_SaveWork, Strings.Title_Notification, MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
        {
            case MessageBoxResult.Cancel:
                return true;
            case MessageBoxResult.Yes:
                EraseAutosave();
                SaveRecord();
                break;
            case MessageBoxResult.No:
                Edited = false;
                EraseAutosave();
                break;
        }

        RefreshRecentNotes();

        return false;
    }

    public virtual void Construct()
    {
        if (FinishedLoading)
            return;

        Record.DB?.RemovePreviousNote(Record);

        if (Record.Locked)
        {
            LastChange = Strings.NoteLocked;
            IsEnabled = false;
        }
        else
        {
            LastChange = Record.GetLastChange();
            Record.DB?.Transmit(NetworkUtils.MessageType.RecordUnlock, Record.Index.ToByteArray());
        }

        Document = Record.GetDocument() ?? new();
        Document.Focus();

        if (CaretPosition is null || !CaretPosition.IsInSameDocument(Document.ContentStart))
            CaretPosition = Document.ContentStart;

        OriginalBlockCount = Document.Blocks.Count;
        OriginalPlaintext = new TextRange(Document.ContentStart, Document.ContentEnd).Text;
        OriginalRevisionCount = Record.GetNumRevisions();
        OriginalText = TextConverter.Save(Document, TextFormat.Xaml);

        Edited = false;
        FinishedLoading = true;
    }

    public virtual void Deconstruct()
    {
        RichTextBoxUtils.SetDocumentCaret(Document, CaretPosition.DocumentStart.GetOffsetToPosition(CaretPosition));
    }

    protected void EraseAutosave()
    {
        if (Record is null)
            return;

        for (int i = Record.GetNumRevisions() - 1; i >= Math.Max(OriginalRevisionCount - 1, 0); i--)
        {
            if (Record.IsAutosaveRevision(i))
                Record.DeleteRevision(i);
        }

        RefreshRecentNotes();
    }

    public void RequestUnlock(NoteRecord source)
    {
        if (!Record.Equals(source))
            return;

        LastChange = source.GetLastChange();
        IsEnabled = true;
    }

    public void SaveRecord()
    {
        if (Record is null)
            return;

        Record?.DB?.CreateRevision(Record, TextConverter.Save(Document, TextFormat.Xaml));
        LastChange = Record?.GetLastChange();
        RefreshRecentNotes();
    }

    public void ScrollTo(int position) => FlowDocumentUtils.ScrollToPosition(Document, position);

    public void ScrollTo(string? text) => FlowDocumentUtils.ScrollToText(Document, text);

    public virtual void TextChanged()
    {
        if (!FinishedLoading)
            return;

        // This base class contains no save function, so it's up to an inheritor (e.g. NoteTab or SearchResult) to reset the Edited variable when needed.
        Edited = Edited || CalculateIsEdited();
        Autosave();
        RefreshRecentNotes();
    }
}