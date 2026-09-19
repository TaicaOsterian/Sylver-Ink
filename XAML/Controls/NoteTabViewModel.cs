using SylverInk.Mvvm;
using SylverInk.Mvvm.Events;
using System.Globalization;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Controls;

public class NoteTabViewModel : NoteEditorViewModel
{
    private FlowDocument _historicalDocument;
    private bool _isLive = true;
    private int _revisionIndex;
    private string _saveLabel = Strings.Word_Save;
    private string _searchText = string.Empty;

    private int BackstepIndex => Record.GetNumRevisions() - RevisionIndex + 1;
    private bool Saving;
    public Label TabHeader => Record.GetRibbonHeader();

    public FlowDocument HistoricalDocument
    {
        get => _historicalDocument;
        set
        {
            _historicalDocument = value;
            OnPropertyChanged();
        }
    }
    public bool IsLive
    {
        get => _isLive;
        set
        {
            _isLive = value;
            OnPropertyChanged();
        }
    }
    public int RevisionIndex
    {
        get => _revisionIndex;
        set
        {
            _revisionIndex = value;
            OnPropertyChanged();
        }
    }
    public string SaveLabel
    {
        get => _saveLabel;
        set
        {
            _saveLabel = value;
            OnPropertyChanged();
        }
    }
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseSearchPopupCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand FindNextCommand { get; }
    public ICommand FindPreviousCommand { get; }
    public ICommand NavigateNextCommand { get; }
    public ICommand NavigatePreviousCommand { get; }
    public ICommand ReturnCommand { get; }
    public ICommand SaveCommand { get; }

    public event EventHandler<DocumentRefreshEventArgs>? RequestRefresh;
    public event EventHandler? RequestCloseSearchPopup;

    public NoteTabViewModel() : base()
    {
        CloseSearchPopupCommand = new RelayCommand(_ => RequestCloseSearchPopup?.Invoke(this, EventArgs.Empty));
        DeleteCommand = new RelayCommand(Delete);
        FindNextCommand = new RelayCommand(FindNext);
        FindPreviousCommand = new RelayCommand(FindPrevious);
        NavigateNextCommand = new RelayCommand(NavigateNext, CanNavigateNext);
        NavigatePreviousCommand = new RelayCommand(NavigatePrevious, CanNavigatePrevious);
        ReturnCommand = new RelayCommand(Return);
        SaveCommand = new RelayCommand(BeginSave);

        _historicalDocument = new();
    }

    private void BeginSave(object? param)
    {
        Saving = true;
        EraseAutosave();

        if (RevisionIndex == 0)
        {
            EndSave();
            return;
        }

        var blocks = new Block[HistoricalDocument.Blocks.Count];

        HistoricalDocument.Blocks.CopyTo(blocks, 0);
        HistoricalDocument.Blocks.Clear();

        RequestRefresh?.Invoke(this, new(blocks));
    }

    private bool CanNavigateNext(object? param) => RevisionIndex > 0;

    private bool CanNavigatePrevious(object? param) => RevisionIndex < Record.GetNumRevisions() + 1;

    public override void Construct()
    {
        if (FinishedLoading)
            return;

        base.Construct();

        IsEnabled = !Record.Locked;

        Edited = false;
        LastChange = Record.Locked ? Strings.NoteLocked : Record.GetNumRevisions() == 0 ? string.Format(CultureInfo.CurrentCulture, CacheNoteEntryCreated, Record.GetCreated()) : string.Format(CultureInfo.CurrentCulture, CacheNoteEntryModified, Record.GetLastChange());

        RefreshRecentNotes();
    }

    public override void Deconstruct()
    {
        base.Deconstruct();

        if (!Record.Locked)
            Record.DB?.Unlock(Record.UUID, true);

        RemoveRecordTab(Record);
        RefreshRecentNotes();
    }

    private void Delete(object? param)
    {
        if (MessageBox.Show(Strings.ConfirmDeleteNote, Strings.Title_Notification, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
            return;

        Deconstruct();
        Concurrent(CurrentDatabase.DeleteRecord, Record, true);
        RefreshRecentNotes();
    }

    public void EndSave()
    {
        var newText = TextConverter.Save(Document, TextFormat.Xaml);
        Record.DB?.CreateRevision(Record, newText);

        Edited = false;
        IsLive = true;
        LastChange = string.Format(CultureInfo.CurrentCulture, CacheNoteEntryModified, Record.GetLastChange());
        OriginalBlockCount = Document.Blocks.Count;
        OriginalPlaintext = new TextRange(Document.ContentStart, Document.ContentEnd).Text;
        OriginalRevisionCount = Record.GetNumRevisions();
        OriginalText = newText;
        RevisionIndex = 0;
        SaveLabel = Strings.Word_Save;
        Saving = false;

        RefreshRecentNotes();
    }

    private void FindNext(object? param)
    {
        FlowDocumentUtils.ScrollToText(Document, SearchText);
    }

    private void FindPrevious(object? param)
    {
        FlowDocumentUtils.ScrollToText(Document, SearchText, LogicalDirection.Backward);
    }

    private void NavigateNext(object? param)
    {
        do
        {
            RevisionIndex--;
        } while (RevisionIndex > 0 && Record.IsAutosaveRevision(BackstepIndex - 1));

        string revisionTime = RevisionIndex == 0 ? Record.GetLastChange() : Record.GetRevisionTime(RevisionIndex);

        if (RevisionIndex != 0)
            HistoricalDocument = Record.GetDocument(RevisionIndex);

        Edited = RevisionIndex != 0 || CalculateIsEdited();
        IsLive = RevisionIndex == 0;
        LastChange = RevisionIndex != 0
            ? string.Format(CultureInfo.CurrentCulture, CacheNoteRevisionID, BackstepIndex, revisionTime)
            : string.Format(CultureInfo.CurrentCulture, CacheNoteEntryModified, revisionTime);
        SaveLabel = RevisionIndex != 0 ? Strings.Word_Restore : Strings.Word_Save;
    }

    private void NavigatePrevious(object? param)
    {
        do
        {
            RevisionIndex++;
        } while (RevisionIndex < Record.GetNumRevisions() + 1 && Record.IsAutosaveRevision(BackstepIndex - 1));

        string revisionTime = RevisionIndex == Record.GetNumRevisions() ? Record.GetCreated() : Record.GetRevisionTime(RevisionIndex);

        Edited = true;
        HistoricalDocument = Record.GetDocument(RevisionIndex);
        IsLive = false;
        LastChange = BackstepIndex == 0
            ? string.Format(CultureInfo.CurrentCulture, CacheNoteEntryCreated, revisionTime)
            : string.Format(CultureInfo.CurrentCulture, CacheNoteRevisionID, BackstepIndex, revisionTime);
        SaveLabel = Strings.Word_Restore;
    }

    private void Return(object? param)
    {
        if (Edited && ConfirmExit())
            return;

        if (Record is null)
            return;

        CurrentDatabase.Transmit(NetworkUtils.MessageType.RecordUnlock, Record.UUID.ToString());
        CurrentDatabase.PushPreviousNote(Record);

        Deconstruct();
        RefreshRecentNotes();
    }

    public override void TextChanged()
    {
        if (!IsLive && !Saving)
            return;

        if (Saving)
            EndSave();

        base.TextChanged();
    }
}
