using System.Globalization;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.Objects.ViewModels;

public class NoteTabViewModel : NoteEditorViewModel
{
    private FlowDocument _historicalDocument;
    private bool _isLive = true;
    private int _revisionIndex;
    private string _saveLabel = Strings.Word_Save;
    private string _searchText = string.Empty;

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
        SaveCommand = new RelayCommand(Save);

        _historicalDocument = new();
    }

    private bool CanNavigateNext(object? param) => RevisionIndex > 0;

    private bool CanNavigatePrevious(object? param) => RevisionIndex + 1 <= Record.GetNumRevisions();

    public override void Construct()
    {
        if (FinishedLoading)
            return;

        base.Construct();

        IsEnabled = !Record.Locked;

        Edited = false;
        LastChange = Record.Locked ? Strings.NoteLocked : Record.GetNumRevisions() == 0 ? string.Format(CultureInfo.CurrentCulture, CacheNoteEntryCreated, Record.GetCreated()) : string.Format(CultureInfo.CurrentCulture, CacheNoteEntryModified, Record.GetLastChange());
    }

    public override void Deconstruct()
    {
        base.Deconstruct();

        if (!Record.Locked)
            Record.DB?.Unlock(Record.Index, true);

        var ChildPanel = GetChildPanel("DatabasesPanel");

        RemoveRecordTab(Record);

        for (int i = ChildPanel.Items.Count - 1; i > 0; i--)
        {
            var item = (TabItem)ChildPanel.Items[i];

            if (item.Content is not NoteTab otherTab)
                continue;

            if (!otherTab.ViewModel.Record.Equals(Record))
                continue;

            if (ChildPanel.SelectedIndex == i)
                ChildPanel.SelectedIndex = Math.Max(0, Math.Min(i - 1, ChildPanel.Items.Count - 1));

            ChildPanel.Items.RemoveAt(i);
        }

        RefreshRecentNotes();
    }

    private void Delete(object? param)
    {
        if (MessageBox.Show(Strings.ConfirmDeleteNote, Strings.Title_Notification, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
            return;

        Deconstruct();
        Concurrent(CurrentDatabase.DeleteRecord, Record, true);
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
        } while (Record.IsAutosaveRevision(RevisionIndex));

        string revisionTime = RevisionIndex == 0 ? Record.GetLastChange() : Record.GetRevisionTime(RevisionIndex);

        if (RevisionIndex != 0)
            HistoricalDocument = Record.GetDocument(RevisionIndex);

        Edited = RevisionIndex != 0 || CalculateIsEdited();
        IsLive = RevisionIndex == 0;
        LastChange = RevisionIndex != 0
            ? string.Format(CultureInfo.CurrentCulture, CacheNoteRevisionID, Record.GetNumRevisions() - RevisionIndex, revisionTime)
            : string.Format(CultureInfo.CurrentCulture, CacheNoteEntryModified, revisionTime);
        SaveLabel = RevisionIndex != 0 ? Strings.Word_Restore : Strings.Word_Save;
    }

    private void NavigatePrevious(object? param)
    {
        do
        {
            RevisionIndex++;
        } while (Record.IsAutosaveRevision(RevisionIndex));

        string revisionTime = RevisionIndex == Record.GetNumRevisions() ? Record.GetCreated() : Record.GetRevisionTime(RevisionIndex);

        Edited = true;
        HistoricalDocument = Record.GetDocument(RevisionIndex);
        IsLive = false;
        LastChange = RevisionIndex == Record.GetNumRevisions()
            ? string.Format(CultureInfo.CurrentCulture, CacheNoteEntryCreated, revisionTime)
            : string.Format(CultureInfo.CurrentCulture, CacheNoteRevisionID, Record.GetNumRevisions() - RevisionIndex, revisionTime);
        SaveLabel = Strings.Word_Restore;
    }

    private void Return(object? param)
    {
        if (Edited && ConfirmExit())
            return;

        if (Record is null)
            return;

        CurrentDatabase.Transmit(NetworkUtils.MessageType.RecordUnlock, Record.Index.ToByteArray());
        CurrentDatabase.PushPreviousNote(Record);

        Deconstruct();
    }

    private void Save(object? param)
    {
        EraseAutosave();

        if (RevisionIndex != 0)
        {
            Document.Blocks.Clear();

            for (int i = 0; i < HistoricalDocument.Blocks.Count; i++)
            {
                var item = HistoricalDocument.Blocks.ElementAt(i);
                HistoricalDocument.Blocks.Remove(item);
                Document.Blocks.Add(item);
            }
        }

        var newText = TextConverter.Save(Document, TextFormat.Xaml);
        Record.DB?.CreateRevision(Record, newText);

        Edited = false;
        IsEnabled = true;
        IsLive = true;
        LastChange = string.Format(CultureInfo.CurrentCulture, CacheNoteEntryModified, Record.GetLastChange());
        OriginalBlockCount = Document.Blocks.Count;
        OriginalPlaintext = new TextRange(Document.ContentStart, Document.ContentEnd).Text;
        OriginalRevisionCount = Record.GetNumRevisions();
        OriginalText = newText;
        RevisionIndex = 0;
        SaveLabel = Strings.Word_Save;
    }

    public override void TextChanged()
    {
        if (!IsLive)
            return;

        base.TextChanged();
    }
}
