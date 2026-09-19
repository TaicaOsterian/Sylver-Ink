using SylverInk.Mvvm;
using SylverInk.XAML.Controls;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.ViewModels;

public class SearchResultViewModel : NoteEditorViewModel
{
    private bool _isFocused;

    public bool IsFocused
    {
        get => _isFocused;
        set
        {
            _isFocused = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand ViewCommand { get; }

    public event EventHandler? ForceClose;
    public event EventHandler? RequestClose;

    public SearchResultViewModel() : base()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        ViewCommand = new RelayCommand(View);
    }

    public override void Construct()
    {
        base.Construct();

        RemoveRecordTab(Record);
    }

    public override void Deconstruct()
    {
        base.Deconstruct();

        Record.DB?.PushPreviousNote(Record);

        if (Edited)
            SaveRecord();

        Record?.DB?.Transmit(NetworkUtils.MessageType.RecordUnlock, Record?.UUID.ToString() ?? string.Empty);

        OpenQueries.RemoveAll(query => query.ViewModel.Record.Equals(Record));
    }

    private void View(object? param)
    {
        // To avoid cluttering the user's view
        SearchWindow?.Close();

        ForceClose?.Invoke(this, EventArgs.Empty);

        if (Record is null)
            return;

        if (Record.DB is null)
            return;

        SwitchDatabase(Record.DB);

        var tab = AddRecordTab(Record);

        tab.ViewModel.Document = Document;
        tab.ViewModel.CaretPosition = CaretPosition;
        tab.ViewModel.Edited = Edited;
    }
}
