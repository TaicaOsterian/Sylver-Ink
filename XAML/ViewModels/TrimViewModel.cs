using SylverInk.Mvvm;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.ViewModels;

internal sealed class TrimViewModel : ViewModelBase
{
    private bool _shouldDeleteEmptyNotes;
    private bool _shouldDeleteOldRevisions;

    public bool ShouldDeleteEmptyNotes
    {
        get => _shouldDeleteEmptyNotes;
        set
        {
            _shouldDeleteEmptyNotes = value;
            OnPropertyChanged();
        }
    }

    public bool ShouldDeleteOldRevisions
    {
        get => _shouldDeleteOldRevisions;
        set
        {
            _shouldDeleteOldRevisions = value;
            OnPropertyChanged();
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand TrimCommand { get; }

    internal event EventHandler? RequestClose;

    internal TrimViewModel()
    {
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(this, EventArgs.Empty));
        TrimCommand = new RelayCommand(InvokeTrim, CanInvokeTrim);
    }

    private bool CanInvokeTrim(object? param) => ShouldDeleteEmptyNotes || ShouldDeleteOldRevisions;

    private void DeleteEmptyNotes()
    {
        if (!ShouldDeleteEmptyNotes)
            return;

        for (int i = CurrentDatabase.RecordCount - 1; i > -1; i--)
        {
            var record = CurrentDatabase.GetRecord(i);
            if (record is null)
                continue;

            if (string.IsNullOrWhiteSpace(record.ToString()))
                CurrentDatabase.DeleteRecord(record);
        }
    }

    private void DeleteOldRevisions()
    {
        if (!ShouldDeleteOldRevisions)
            return;

        for (int i = CurrentDatabase.RecordCount - 1; i > -1; i--)
        {
            var record = CurrentDatabase.GetRecord(i);
            if (record is null)
                continue;

            record.PurgeRevisions();
        }
    }

    private void InvokeTrim(object? param)
    {
        if (MessageBox.Show(Strings.Message_ConfirmTrim, Strings.Title_Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            return;

        DeleteEmptyNotes();
        DeleteOldRevisions();
        RefreshRecentNotes();

        ShowTooltip(Strings.Tooltip_TrimComplete!);
    }
}
