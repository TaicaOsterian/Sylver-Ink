using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;
using SylverInk.XAML.Objects;

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

    public event EventHandler? RequestClose;

    public SearchResultViewModel()
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

    public void SaveRecord()
    {
        if (Record is null)
            return;

        Record?.DB?.CreateRevision(Record, TextConverter.Save(Document, TextFormat.Xaml));
        LastChange = Record?.GetLastChange();
    }

    private void View(object? param)
    {
        SearchWindow?.Close();

        if (Record is null)
            return;

        if (Record.DB is null)
            return;

        SwitchDatabase(Record.DB);

        NoteTab tab = new();
        tab.ViewModel.InitialPointer = CaretPosition;
        tab.ViewModel.Record = Record;

        TabItem item = new()
        {
            Content = tab,
            Header = GetRibbonHeader(Record),
            Margin = new(0, 2, 0, 0)
        };

        var ChildPanel = GetChildPanel("DatabasesPanel");
        ChildPanel.SelectedIndex = ChildPanel.Items.Add(item);
        OpenTabs.Add(item);

        Application.Current.MainWindow.WindowState = WindowState.Normal;

        if (!Application.Current.MainWindow.IsActive)
            Application.Current.MainWindow.Activate();

        Application.Current.MainWindow.Focus();

        CloseCommand.Execute(null);
    }
}
