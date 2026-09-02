using System.Windows.Data;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.ViewModels;

public class SearchViewModel : ViewModelBase
{
    private bool _canQuery = true;
    private string _queryString = string.Empty;

    public bool CanQuery
    {
        get => _canQuery;
        set
        {
            _canQuery = value;
            OnPropertyChanged();
        }
    }

    public string QueryString
    {
        get => _queryString;
        set
        {
            _queryString = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand QueryCommand { get; }

    public event EventHandler? RequestClose;

    public SearchViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        QueryCommand = new RelayCommand(Query);
    }

    private async Task PerformSearch()
    {
        foreach (Database db in Databases)
            await SearchDatabase(db);
    }

    private async Task SearchDatabase(Database db)
    {
        CommonUtils.Settings.SearchResults.Clear();
        db.UpdateWordPercentages();

        List<NoteRecord> results = [];

        ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(CommonUtils.Settings.SearchResults);
        view.CustomSort ??= Comparer<NoteRecord>.Create(new((r1, r2) => r2.MatchTags(QueryString).CompareTo(r1.MatchTags(QueryString))));

        for (int i = 0; i < db.RecordCount; i++)
        {
            if (db.GetRecord(i) is not NoteRecord newRecord)
                continue;

            bool textFound = await SearchRecord(newRecord);

            if (!textFound)
                continue;

            results.Add(newRecord);
        }

        for (int i = 0; i < results.Count; i++)
            CommonUtils.Settings.SearchResults.Add(results[i]);
    }

    private Task<bool> SearchRecord(NoteRecord record) => Task.Run(() =>
    {
        var document = Concurrent(record.GetDocument);
        TextPointer? pointer = document.ContentStart;
        while (pointer is not null && pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.None)
        {
            while (pointer is not null && pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);

            if (pointer is null)
                break;

            string recordText = pointer.GetTextInRun(LogicalDirection.Forward);
            if (recordText.Contains(QueryString, StringComparison.OrdinalIgnoreCase))
                return true;

            while (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }

        return false;
    });

    private async void Query(object? param)
    {
        CanQuery = false;
        await PerformSearch();
        CanQuery = true;
    }
}
