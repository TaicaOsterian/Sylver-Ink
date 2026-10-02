using SylverInk.FileIO;
using SylverInk.Mvvm;
using System.Globalization;
using System.Text;
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
        StatusText = Strings.SelectFile;
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
        StatusText = $"{Strings.Status_Importing}...";

        try
        {
            await Task.Run(PerformImport);

            StatusText = string.Format(CultureInfo.CurrentCulture, CacheLabelNotesImported, Imported);
            ImportTarget = string.Empty;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, CacheImportFailed, ex.Message), Strings.Title_Error, MessageBoxButton.OK);
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
            StatusText = Strings.FailedToReadFile;
            return;
        }

        StatusText = $"{Strings.Status_Measuring}...";

        if (AdaptiveImport)
            MeasureNotesAdaptive();
        else
            MeasureNotesManual();

        ReportMeasurement();
    }

    private void MeasureNotesAdaptive()
    {
        // We scan for the following character classes in order to build predicates.
        string[] classes = [@"\p{L}", @"\p{Nd}", @"[\p{Zs}\t]", @"[\p{P}\p{S}]"];
        var weights = new Dictionary<string, double> {
            [@"\p{L}"] = 1.0,
            [@"\p{Nd}"] = 2.0,
            [@"[\p{Zs}\t]"] = 2.0,
            [@"[\p{P}\p{S}]"] = 4.0,
        };

        var predicates = new List<string> { string.Empty };
        var predicateSet = new HashSet<string> { string.Empty };
        var tokenCounts = new Dictionary<string, int> { [string.Empty] = 0 };
        var matchCounts = new Dictionary<string, int> { [string.Empty] = 0 };

        string bestPredicate = string.Empty;
        double bestScore = double.NegativeInfinity;
        int lineCount = DataLines.Count;
        int sequenceCount = 0;

        for (int limit = 2; ; limit++)
        {
            int predicateCount = predicateSet.Count;

            // Build new predicates by extending the surviving seeds.
            for (int line = 0; line < lineCount; line++)
            {
                var key = DataLines[line];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                for (var (c, t) = (0, 0); c < Math.Max(0, Math.Min(key.Length, limit) - 1); t++)
                {
                    if (t >= classes.Length)
                    {
                        c++;
                        t = 0;
                    }

                    string type = classes[t];
                    if (!Regex.IsMatch(key.AsSpan(c, 1), type))
                        continue;

                    for (int k = predicates.Count - 1; k > -1; k--)
                    {
                        var pattern = predicates[k];

                        if (c + 1 < tokenCounts[pattern])
                            continue;

                        // We insert a special character to later serve as a separator.
                        var pBrute = $"{pattern}={type}";

                        if (predicateSet.Contains(pBrute))
                            continue;

                        // Sanity check: Does the newly constructed predicate match the string it was just built from?
                        var keySpan = key.AsSpan(0, Math.Min(c + 1, key.Length));
                        if (!Regex.IsMatch(keySpan, pBrute.Replace("=", string.Empty)))
                            continue;

                        predicates.Add(pBrute);
                        predicateSet.Add(pBrute);
                        tokenCounts.TryAdd(pBrute, tokenCounts[pattern] + 1);
                    }
                }
            }

            // If we didn't build any new predicates this iteration, the search has exhausted and it's time to abort.
            if (predicateCount == predicateSet.Count)
                break;

            predicates = [.. predicates.Distinct()];

            // Count matches, cached. Only newly-added predicates need to be counted.
            foreach (string predicate in predicates)
            {
                if (matchCounts.ContainsKey(predicate))
                    continue;

                var regex = new Regex($@"^{predicate.Replace("=", string.Empty)}");
                int matches = 0;

                for (int i = 0; i < lineCount; i++)
                {
                    if (regex.IsMatch(DataLines[i]))
                        matches++;
                }

                matchCounts[predicate] = matches;
            }

            // Score viable predicates.
            var entropies = new Dictionary<string, double>();
            var tfScores = new Dictionary<string, double>();

            foreach (string predicate in predicates)
            {
                if (string.IsNullOrEmpty(predicate))
                    continue;

                // IDF score over predicate.

                int matches = matchCounts[predicate];

                // Hard floor / ceiling on what can be a separator.
                if (matches < 2 || matches > lineCount * 0.75)
                    continue;

                // W(x) = log10(L / l(x)), where: x is a predicate; L is the total number of text lines; and l(x) is the number of text lines matching x.
                double tfScore = Math.Log10(1.0 + (double)lineCount / matches);
                tfScores[predicate] = tfScore * weights.Sum(pair => Regex.Count(predicate, pair.Key) * pair.Value);

                // Shannon entropy over class tokens.
                var probabilities = new Dictionary<string, double>();
                var split = predicate.Split('=');

                for (int i = 0; i < split.Length; i++)
                {
                    string token = split[i];

                    if (string.IsNullOrWhiteSpace(token))
                        continue;

                    if (!probabilities.TryAdd(token, 1.0))
                        probabilities[token] += 1.0;
                }

                // H(x) = -sum(p(x) * log2(p(x))), where: x is a predicate; and p(x) is the probability distribution of each single character (or in this case, token) within x.
                double sum = 0.0;

                foreach (double val in probabilities.Values)
                {
                    double prob = val / tokenCounts[predicate];
                    sum += prob * Math.Log2(prob);
                }

                entropies[predicate] = -sum;
            }

            // Prune seeds for the next iteration.
            var survivors = predicates.Where(p => matchCounts[p] >= 2).ToList();
            predicates = survivors;
            predicateSet = [.. survivors];

            if (entropies.Count == 0)
                continue;

            // Final score: S(x) = W(x) * (1 - H(x) / Hmax), where: x is a predicate; W(x) is the TF-IDF score of x; H(x) is the Shannon entropy of x; and Hmax is the highest Shannon entropy among all predicates.

            double highestEntropy = entropies.Values.Max();

            foreach (var (predicate, tfScore) in tfScores)
            {
                double factor = highestEntropy > 0
                    ? 1.0 - entropies[predicate] / highestEntropy
                    : 1.0;

                double score = tfScore * factor;

                if (score <= bestScore)
                    continue;

                // Our chosen predicate is the one with the best combined TF-IDF and Shannon entropy score.
                bestScore = score;
                bestPredicate = predicate;
                sequenceCount = 0;
            }

            // If no new best predicate has been found in a certain number of steps, take what we've got and don't loop forever.
            sequenceCount++;
            if (sequenceCount > 5)
                break;
        }

        AdaptivePredicate = bestPredicate.Replace("=", string.Empty);

        if (!string.IsNullOrWhiteSpace(AdaptivePredicate.Trim()))
        {
            StringBuilder recordData = new();
            RunningAverage = 0.0;
            RunningCount = 1;

            for (int i = 0; i < DataLines.Count; i++)
            {
                var line = DataLines[i];
                RunningAverage += line.Length;

                if (!Regex.IsMatch(line, $@"^{AdaptivePredicate}"))
                {
                    if (recordData.Length > 0)
                    {
                        recordData.AppendLine();
                        RunningAverage += Environment.NewLine.Length;
                    }

                    recordData.Append(line);
                    continue;
                }

                if (recordData.Length > 0)
                    RunningCount++;

                recordData.Clear();
                recordData.Append(line);
            }

            RunningAverage /= RunningCount;
            return;
        }

        Concurrent(ShowTooltip, Strings.FailedAutodetect);
        AdaptivePredicate = string.Empty;
        RunningAverage = 0.0;
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
            var line = DataLines[i].Trim();
            RunningAverage += line.Length;

            if (recordData.Length > 0)
            {
                recordData.AppendLine();
                RunningAverage += Environment.NewLine.Length;
            }

            recordData.Append(line);

            if (line.Length == 0)
                blankCount++;
            else
                blankCount = 0;

            if (i % 100 == 0)
                StatusText = $"{i * 100.0 / DataLines.Count:N2}% {Strings.Word_Scanned}...";

            if (recordData.Length == 0 || blankCount < LineTolerance)
                continue;

            recordData.Clear();

            blankCount = 0;
            RunningCount++;
        }

        RunningAverage = RunningCount > 0 ? RunningAverage / RunningCount : 0.0;
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

            // Adaptive
            if (AdaptiveImport)
            {
                if (recordData.Length > 0 && Regex.IsMatch(line, $@"^{AdaptivePredicate}"))
                {
                    CurrentDatabase.CreateRecord(recordData.ToString());
                    Imported++;
                    recordData.Clear();
                }

                if (recordData.Length > 0)
                    recordData.AppendLine();

                recordData.Append(line);
                continue;
            }

            // Manual
            line = line.Trim();

            if (recordData.Length > 0)
                recordData.AppendLine();

            recordData.Append(line);

            if (line.Length == 0)
                blankCount++;
            else
                blankCount = 0;

            StatusText = $"{i * 100.0 / DataLines.Count:N2}% {Strings.Word_Imported}...";

            if (i < DataLines.Count - 1 && blankCount < LineTolerance)
                continue;

            if (recordData.Length == 0)
                continue;

            CurrentDatabase.CreateRecord(recordData.ToString());
            blankCount = 0;
            Imported++;
            recordData.Clear();
        }

        if (recordData.Length > 0)
        {
            CurrentDatabase.CreateRecord(recordData.ToString());
            Imported++;
        }

        DelayVisualUpdates = false;
        RefreshRecentNotes();
    }

    private async Task RefreshAsync()
    {
        if (CurrentDatabase == null)
            return;

        CanImport = false;
        IsBusy = true;
        StatusText = $"{Strings.Status_Processing}...";

        try
        {
            if (ImportTarget.EndsWith(".sidb", StringComparison.Ordinal) ||
                ImportTarget.EndsWith(".sibk", StringComparison.Ordinal))
            {
                var result = MessageBox.Show(
                    Strings.Message_MergeDatabases,
                    Strings.Title_Warning,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (!CurrentDatabase.Open(ImportTarget))
                {
                    MessageBox.Show(Strings.FailedImport, Strings.Title_Error, MessageBoxButton.OK);
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    CurrentDatabase.MakeBackup(true);
                    CurrentDatabase.Erase();
                }

                CurrentDatabase.Initialize(false);
                Imported = CurrentDatabase.RecordCount;
                StatusText = string.Format(CultureInfo.CurrentCulture, CacheLabelNotesImported, Imported);
                return;
            }

            await Task.Run(Measure);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            MessageBox.Show(string.Format(CultureInfo.CurrentCulture, CacheFailedToProcessFile, ex.Message), Strings.Title_Error, MessageBoxButton.OK);
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
            DataLines = [.. content.ReplaceLineEndings().Split(Environment.NewLine)];
            return true;
        }
        catch (Exception e)
        {
            App.LogException(e);
            return false;
        }
    }

    private void ReportMeasurement()
    {
        CanImport = RunningCount > 0;
        StatusText = string.Format(CultureInfo.CurrentCulture, CacheImportMeasurementText, RunningCount, RunningAverage);
    }

    private async Task ToggleAdaptiveAsync()
    {
        IsBusy = true;
        await Task.Run(Measure);
        IsBusy = false;
    }
}