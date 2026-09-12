using SylverInk.XAML.Objects;
using System.Threading;
using System.Windows.Data;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.ViewModels;

public class SearchViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _cts;
    private static string _queryString = string.Empty;

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

    public void CancelSearch()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task PerformSearch(CancellationToken token)
    {
        if (CommonUtils.Settings.QueryAllDatabases)
        {
            foreach (Database db in Databases)
            {
                token.ThrowIfCancellationRequested();
                await SearchDatabase(db, token);
            }
        }
        else
        {
            await SearchCurrentDatabase(token);
        }
    }

    private async void Query(object? param)
    {
        var token = StartNewSearch();

        CommonUtils.Settings.SearchResults.Clear();

        try
        {
            await PerformSearch(token);
        }
        catch { }
    }

    public async Task SearchCurrentDatabase(CancellationToken token)
    {
        await SearchDatabase(CurrentDatabase, token);
    }

    private async Task SearchDatabase(Database db, CancellationToken token)
    {
        db.UpdateWordPercentages();

        ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(CommonUtils.Settings.SearchResults);
        view.CustomSort ??= Comparer<NoteRecord>.Create(new((r1, r2) => r2.MatchTags(QueryString).CompareTo(r1.MatchTags(QueryString))));

        await Task.Run(async () =>
        {
            for (int i = 0; i < db.RecordCount; i++)
            {
                token.ThrowIfCancellationRequested();

                if (db.GetRecord(i) is not NoteRecord rec)
                    continue;

                if (!await SearchRecord(rec))
                    continue;

                rec.MatchTags(QueryString);

                Concurrent(() =>
                {
                    if (!token.IsCancellationRequested)
                        CommonUtils.Settings.SearchResults.Add(rec);
                });
            }
        }, token);
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

    internal CancellationToken StartNewSearch()
    {
        CancelSearch();
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }
}
