using SylverInk.XAML.Objects;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

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

        var tabPanel = GetChildPanel("DatabasesPanel");
        for (int i = tabPanel.Items.Count - 1; i > 0; i--)
        {
            if (tabPanel.Items[i] is not TabItem item)
                continue;

            if (item.Tag is not NoteRecord record)
                continue;

            if (record.Equals(Record))
                tabPanel.Items.RemoveAt(i);
        }
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

        NoteTab tab = new();
        tab.ViewModel.Record = Record;

        tab.ViewModel.Document = Document;
        tab.ViewModel.CaretPosition = CaretPosition;
        tab.ViewModel.Edited = Edited;

        TabItem item = new()
        {
            Content = tab,
            Header = GetRibbonHeader(Record),
            Margin = new(0, 2, 0, 0)
        };

        var ChildPanel = GetChildPanel("DatabasesPanel");
        ChildPanel.SelectedIndex = ChildPanel.Items.Add(item);
        OpenTabs.Add(item);
    }
}
