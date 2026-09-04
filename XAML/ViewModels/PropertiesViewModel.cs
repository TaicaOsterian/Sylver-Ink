using System.Globalization;
using System.Windows.Markup;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.ViewModels;

public partial class PropertiesViewModel : ViewModelBase
{
    private string? _dbName;
    private string? _dbCreated;
    private string? _dbFormat;
    private string? _dbPath;
    private string? _dbNotes;
    private string? _dbAvg;
    private string? _dbTotal;
    private string? _dbLongest;
    private string _hour = "12";
    private readonly List<string> _hours = ["12", .. Enumerable.Range(1, 11).Select(n => $"{n:0,0}")];
    private string _meridian = "AM";
    private readonly List<string> _meridians = ["AM", "PM"];
    private string _minute = "00";
    private readonly List<string> _minutes = [.. Enumerable.Range(0, 60).Select(n => $"{n:0,0}")];
    private DateTime? _restoreDate;
    private string? _timeString = "12:00 AM";

    public string? DBName
    {
        get => _dbName;
        set
        {
            _dbName = value;
            OnPropertyChanged();
        }
    }
    public string? DBCreated
    {
        get => _dbCreated;
        set
        {
            _dbCreated = value;
            OnPropertyChanged();
        }
    }
    public string? DBFormat
    {
        get => _dbFormat;
        set
        {
            _dbFormat = value;
            OnPropertyChanged();
        }
    }
    public string? DBPath
    {
        get => _dbPath;
        set
        {
            _dbPath = value;
            OnPropertyChanged();
        }
    }
    public string? DBNotes
    {
        get => _dbNotes;
        set
        {
            _dbNotes = value;
            OnPropertyChanged();
        }
    }
    public string? DBAvg
    {
        get => _dbAvg;
        set
        {
            _dbAvg = value;
            OnPropertyChanged();
        }
    }
    public string? DBTotal
    {
        get => _dbTotal;
        set
        {
            _dbTotal = value;
            OnPropertyChanged();
        }
    }
    public string? DBLongest
    {
        get => _dbLongest;
        set
        {
            _dbLongest = value;
            OnPropertyChanged();
        }
    }
    public string Hour
    {
        get => _hour;
        set
        {
            _hour = value;
            TimeString = null;
            OnPropertyChanged();
        }
    }
    public List<string> Hours => _hours;
    public string Meridian
    {
        get => _meridian;
        set
        {
            _meridian = value;
            TimeString = null;
            OnPropertyChanged();
        }
    }
    public List<string> Meridians => _meridians;
    public string Minute
    {
        get => _minute;
        set
        {
            _minute = value;
            TimeString = null;
            OnPropertyChanged();
        }
    }
    public List<string> Minutes => _minutes;
    public DateTime? RestoreDate
    {
        get => _restoreDate;
        set
        {
            _restoreDate = value;
            OnPropertyChanged();
        }
    }
    public string? TimeString
    {
        get => _timeString;
        set
        {
            _timeString = $"{Hour:0,0}:{Minute:0,0} {Meridian}";
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand InitializeCommand { get; }
    public ICommand RestoreCommand { get; }

    public event EventHandler? RequestClose;

    public PropertiesViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        InitializeCommand = new RelayCommand(async _ => await Task.Run(InitializeProperties));
        RestoreCommand = new RelayCommand(Restore);
    }

    private void InitializeProperties()
    {
        DBAvg = "...";
        DBCreated = CurrentDatabase.GetCreated();
        DBFormat = $"SIDB v.{CurrentDatabase.Format}";
        DBLongest = "...";
        DBName = CurrentDatabase.Name;
        DBNotes = string.Format(CultureInfo.CurrentCulture, CacheLabelNoteNumber, CurrentDatabase.RecordCount);
        DBPath = $"{CurrentDatabase.DBFile}";
        DBTotal = "...";

        double noteAvgC = 0.0;
        double noteAvgW = 0.0;
        int noteLongestC = 0;
        int noteLongestW = 0;
        int noteTotalC = 0;
        int noteTotalW = 0;

        for (int i = 0; i < CurrentDatabase.RecordCount; i++)
        {
            var record = CurrentDatabase.GetRecord(i);
            if (record is null)
                continue;

            var recordText = Concurrent(record.ToString);
            var length = recordText.Length;
            if (length == 0)
                continue;

            var wordCount = NotWhitespace().Count(recordText);

            noteAvgC += length;
            noteAvgW += wordCount;

            // The 'longest' note is qualified strictly by character count.
            if (noteLongestC <= length)
            {
                noteLongestC = length;
                noteLongestW = wordCount;
            }

            noteTotalC += length;
            noteTotalW += wordCount;
        }

        noteAvgC /= CurrentDatabase.RecordCount;
        noteAvgW /= CurrentDatabase.RecordCount;

        DBAvg = string.Format(CultureInfo.CurrentCulture, CacheWordCount, noteAvgW, noteAvgC);
        DBLongest = string.Format(CultureInfo.CurrentCulture, CacheWordCount, noteLongestW, noteLongestC);
        DBTotal = string.Format(CultureInfo.CurrentCulture, CacheWordCount, noteTotalW, noteTotalC);
    }

    private async void Restore(object? param = null)
    {
        if (MessageBox.Show(Strings.Message_ConfirmReversion, Strings.Title_Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        DateTime reversion = RestoreDate ?? DateTime.UtcNow;
        reversion = reversion.Date.AddHours(double.Parse(Hour, NumberFormatInfo.InvariantInfo)).AddMinutes(double.Parse(Minute, NumberFormatInfo.InvariantInfo));

        CurrentDatabase.Revert(reversion);
        await Task.Run(InitializeProperties);
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex NotWhitespace();
}
