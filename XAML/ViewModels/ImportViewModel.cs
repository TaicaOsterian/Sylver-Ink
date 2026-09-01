using SylverInk.FileIO;
using SylverInk.XAMLUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.ViewModels;

public class ImportViewModel : ViewModelBase
{
    private bool _adaptiveImport;
    private string _adaptivePredicate = string.Empty;
    private bool _canImport;
    private List<string> _dataLines = [];
    private int _imported;
    private string _importTarget = string.Empty;
    private bool _isBusy;
    private int _lineTolerance;
    private double _runningAverage;
    private int _runningCount;
    private string _statusText = string.Empty;

    public bool AdaptiveImport
    {
        get => _adaptiveImport;
        set
        {
            _adaptiveImport = value;
            ManualImport = !value;
            OnPropertyChanged();
        }
    }

    public string AdaptivePredicate
    {
        get => _adaptivePredicate;
        set
        {
            _adaptivePredicate = value;
            OnPropertyChanged();
        }
    }

    public bool CanImport
    {
        get => _canImport;
        set
        {
            _canImport = value;
            OnPropertyChanged();
        }
    }

    public List<string> DataLines
    {
        get => _dataLines;
        set
        {
            _dataLines = value;
            OnPropertyChanged();
        }
    }

    public int Imported
    {
        get => _imported;
        set
        {
            _imported = value;
            OnPropertyChanged();
        }
    }

    public string ImportTarget
    {
        get => _importTarget;
        set
        {
            _importTarget = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public int LineTolerance
    {
        get => _lineTolerance;
        set
        {
            _lineTolerance = Math.Clamp(value, 0, 36);
            OnPropertyChanged();
        }
    }

    public bool ManualImport
    {
        get => !AdaptiveImport;
        set
        {
            OnPropertyChanged();
        }
    }

    public double RunningAverage
    {
        get => _runningAverage;
        set
        {
            _runningAverage = value;
            OnPropertyChanged();
        }
    }

    public int RunningCount
    {
        get => _runningCount;
        set
        {
            _runningCount = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand LineToleranceChangeCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand ToggleAdaptiveCommand { get; }

    public event EventHandler? RequestClose;

    public ImportViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        ImportCommand = new RelayCommand(async _ => await ImportAsync(), _ => CanImport && !IsBusy);
        LineToleranceChangeCommand = new RelayCommand(ChangeLineToleranceAsync);
        OpenCommand = new RelayCommand(async _ => await OpenFileAsync());
        ToggleAdaptiveCommand = new RelayCommand(async _ => await ToggleAdaptiveAsync());

        LineTolerance = CommonUtils.Settings.LineTolerance;
        StatusText = "Select a file to import.";
    }

    private async Task ChangeLineToleranceAsync(object? param = null)
    {
        if (param is not Button button)
            return;

        LineTolerance += button.Content.Equals("+") ? 1 : -1;

        CommonUtils.Settings.LineTolerance = LineTolerance;
        await Task.Run(Measure);
    }

    private async Task ImportAsync()
    {
        if (CurrentDatabase == null || RunningCount == 0)
            return;

        CanImport = false;
        IsBusy = true;
        StatusText = "Importing...";

        try
        {
            await Task.Run(PerformImport);

            StatusText = $"Notes imported: {Imported:N0}";
            ImportTarget = string.Empty;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to import: {ex.Message}", "Sylver Ink: Error", MessageBoxButton.OK);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Measure()
    {
        if (string.IsNullOrWhiteSpace(ImportTarget))
            return;

        if (!ReadFromStream(ImportTarget))
        {
            StatusText = "Failed to read file.";
            return;
        }

        StatusText = "Measuring...";

        if (AdaptiveImport)
            MeasureNotesAdaptive();
        else
            MeasureNotesManual();

        ReportMeasurement();
    }

    private void MeasureNotesAdaptive()
    {
        string[] classes = [@"\p{L}+", @"\p{Nd}+", @"[\p{Zs}\t]+", @"[\p{P}\p{S}]+"];
        var frequencies = new Dictionary<string, double>();
        var tokenCounts = new Dictionary<string, int>();

        int lastPredicateSequence = 0;
        double lastPredicateValue = 0.0;
        double lineTotal = 0.0;
        string newPredicate = string.Empty;

        for (int length = 3; ; length++)
        {
            double total = 0.0;
            frequencies.Clear();
            frequencies.Add(string.Empty, 0.0);
            tokenCounts.Clear();
            tokenCounts.Add(string.Empty, 0);

            for (int line = 0; line < DataLines.Count; line++, lineTotal++)
            {
                var key = DataLines[line].Trim();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                for (var (c, t) = (0, 0); c < Math.Max(0, Math.Min(key.Length, length) - 1); t++)
                {
                    if (t >= classes.Length)
                    {
                        c++;
                        t = 0;
                    }
                    string type = classes[t];
                    if (!Regex.IsMatch(key.AsSpan(c, 1), type))
                        continue;

                    for (int k = frequencies.Keys.Count - 1; k > -1; k--)
                    {
                        var pattern = frequencies.Keys.ElementAt(k);
                        if (c + 1 < tokenCounts[pattern])
                            continue;

                        if (pattern.EndsWith(type, StringComparison.Ordinal))
                        {
                            frequencies[pattern]++;
                            total++;
                            continue;
                        }

                        var pBrute = pattern + type;
                        var keySpan = key.AsSpan(0, Math.Min(c + 1, key.Length));
                        if (!Regex.IsMatch(keySpan, pBrute))
                            continue;
                        if (string.IsNullOrWhiteSpace(pBrute.Trim()))
                            continue;

                        total++;
                        if (frequencies.TryAdd(pBrute, 1.0))
                        {
                            tokenCounts.TryAdd(pBrute, tokenCounts[pattern] + 1);
                            frequencies.Remove(string.Empty);
                            tokenCounts.Remove(string.Empty);
                        }
                        else
                        {
                            frequencies[pBrute]++;
                            frequencies.Remove(pattern);
                            tokenCounts.Remove(pattern);
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(frequencies.Keys.ElementAt(0)))
                continue;

            foreach (string key in frequencies.Keys)
                frequencies[key] /= total;

            var orderedEnum = frequencies.OrderByDescending(pair => pair.Value).GetEnumerator();
            if (!orderedEnum.MoveNext())
                break;

            while (orderedEnum.Current.Value >= 0.001)
            {
                if (orderedEnum.Current.Value >= lastPredicateValue
                    || (!orderedEnum.Current.Key.StartsWith(classes[0], StringComparison.Ordinal)
                        && newPredicate.StartsWith("^" + classes[0], StringComparison.Ordinal)))
                {
                    newPredicate = "^" + orderedEnum.Current.Key;
                    if (AdaptivePredicate == newPredicate)
                        break;

                    AdaptivePredicate = newPredicate;
                    lastPredicateSequence = 0;
                    lastPredicateValue = orderedEnum.Current.Value;
                }

                if (!orderedEnum.MoveNext())
                    break;
            }

            if (AdaptivePredicate == newPredicate)
                lastPredicateSequence++;

            if (lastPredicateSequence > 6)
                break;
        }

        if (!string.IsNullOrWhiteSpace(AdaptivePredicate.Trim()))
        {
            StringBuilder recordData = new();
            RunningAverage = 0.0;
            RunningCount = 0;

            for (int i = 0; i < DataLines.Count; i++)
            {
                var line = DataLines[i].Trim();
                if (Regex.IsMatch(line, AdaptivePredicate))
                {
                    if (recordData.Length > 0)
                    {
                        RunningAverage += recordData.Length;
                        RunningCount++;
                    }
                    recordData.Clear();
                    recordData.Append(line);
                }
                else
                {
                    if (i > 0)
                        recordData.AppendLine();
                    recordData.Append(DataLines[i]);
                }
            }

            RunningAverage /= RunningCount;
            return;
        }

        MessageBox.Show("Failed to autodetect the note format.", "Sylver Ink: Error", MessageBoxButton.OK);
        AdaptivePredicate = string.Empty;
        RunningCount = 0;
    }

    private void MeasureNotesManual()
    {
        int blankCount = 0;
        StringBuilder recordData = new();
        RunningAverage = 0.0;
        RunningCount = 0;

        for (int i = 0; i < DataLines.Count; i++)
        {
            var line = DataLines[i];
            if (i > 0)
                recordData.AppendLine();
            recordData.Append(line);

            if (line.Trim().Length == 0)
                blankCount++;
            else
                blankCount = 0;

            if (i % 100 == 0)
                StatusText = $"{i * 100.0 / DataLines.Count:N2}% scanned...";

            if (recordData.Length == 0 || blankCount < LineTolerance)
                continue;

            blankCount = 0;
            RunningAverage += recordData.Length;
            recordData.Clear();
            RunningCount++;
        }

        if (recordData.Length > 0)
        {
            RunningAverage += recordData.Length;
            RunningCount++;
        }

        RunningAverage /= RunningCount;
    }

    private async Task OpenFileAsync()
    {
        string file = FileUtils.DialogFileSelect(outgoing: false, filterIndex: 3);
        if (string.IsNullOrEmpty(file))
            return;

        ImportTarget = file;
        await RefreshAsync();
    }

    private void PerformImport()
    {
        if (CurrentDatabase == null)
            return;

        int blankCount = 0;
        DelayVisualUpdates = true;
        Imported = 0;
        StringBuilder recordData = new();

        for (int i = 0; i < DataLines.Count; i++)
        {
            string line = DataLines[i];

            if (AdaptiveImport)
            {
                if (Regex.IsMatch(line, AdaptivePredicate) && recordData.Length > 0)
                {
                    CurrentDatabase.CreateRecord(recordData.ToString());
                    Imported++;
                    recordData.Clear();
                }
                if (i > 0) recordData.AppendLine();
                recordData.Append(line);
                continue;
            }

            if (i > 0) recordData.AppendLine();
            recordData.Append(line);
            if (line.Length == 0)
                blankCount++;
            else
                blankCount = 0;

            StatusText = $"{i * 100.0 / DataLines.Count:N2}% imported...";

            if (blankCount < LineTolerance && i < DataLines.Count - 1)
                continue;

            if (recordData.Length > 0)
            {
                CurrentDatabase.CreateRecord(recordData.ToString());
                Imported++;
                recordData.Clear();
                blankCount = 0;
            }
        }

        if (recordData.Length > 0)
        {
            CurrentDatabase.CreateRecord(recordData.ToString());
            Imported++;
        }

        DelayVisualUpdates = false;
        Concurrent(DeferUpdateRecentNotes);
    }

    private async Task RefreshAsync()
    {
        if (CurrentDatabase == null)
            return;

        CanImport = false;
        IsBusy = true;
        StatusText = "Processing...";

        try
        {
            if (ImportTarget.EndsWith(".sidb", StringComparison.Ordinal) ||
                ImportTarget.EndsWith(".sibk", StringComparison.Ordinal))
            {
                var result = MessageBox.Show(
                    "You have selected an existing Sylver Ink database. Its contents will be merged with your current database.\n\nDo you want to overwrite your current database instead?",
                    "Sylver Ink: Warning",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (!CurrentDatabase.Open(ImportTarget))
                {
                    MessageBox.Show("Failed to import the selected file.", "Sylver Ink: Error", MessageBoxButton.OK);
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    CurrentDatabase.MakeBackup(true);
                    CurrentDatabase.Erase();
                }

                CurrentDatabase.Initialize(false);
                Imported = CurrentDatabase.RecordCount;
                StatusText = $"Notes imported: {Imported:N0}";
                return;
            }

            await Task.Run(Measure);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to process file: {ex.Message}", "Sylver Ink: Error", MessageBoxButton.OK);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ReadFromStream(string filename)
    {
        try
        {
            using var reader = new StreamReader(filename);
            if (reader.EndOfStream)
                return false;
            string content = reader.ReadToEnd();
            DataLines = [.. content.Split('\n')];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ReportMeasurement()
    {
        CanImport = RunningCount > 0;
        StatusText = $"Estimated new notes: {RunningCount:N0}\nAverage length: {RunningAverage:N0} characters per note\n\nRemember to press Import to finalize your changes!";
    }

    private async Task ToggleAdaptiveAsync()
    {
        IsBusy = true;
        await Task.Run(Measure);
        IsBusy = false;
    }
}