using SylverInk.Net;
using SylverInk.Text;
using SylverInk.XAMLUtils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.Objects.ViewModels;

public class NoteTabViewModel : NoteEditorViewModel
{
	private bool _canNavigateNext;
	private bool _canNavigatePrevious;
	private bool _canSave;
	private TextPointer _initialPointer;
	private bool _isReadOnly;
	private uint _revisionIndex;
	private bool _revisionView;
	private string _saveLabel = "Save";
	private string _searchText = string.Empty;

	public bool CanNavigateNext
	{
		get => _canNavigateNext;
		set
		{
			_canNavigateNext = value;
			OnPropertyChanged();
		}
	}
	public bool CanNavigatePrevious
	{
		get => _canNavigatePrevious;
		set
		{
			_canNavigatePrevious = value;
			OnPropertyChanged();
		}
	}
	public bool CanSave
	{
		get => _canSave;
		set
		{
			_canSave = value;
			OnPropertyChanged();
		}
	}
	public TextPointer InitialPointer
	{
		get => _initialPointer;
		set
		{
			_initialPointer = value;
			OnPropertyChanged();
		}
	}
	public bool IsReadOnly
	{
		get => _isReadOnly;
		set
		{
			_isReadOnly = value;
			OnPropertyChanged();
		}
	}
	public uint RevisionIndex
	{
		get => _revisionIndex;
		set
		{
			_revisionIndex = value;
			OnPropertyChanged();
		}
	}
	public bool RevisionView
	{
		get => _revisionView;
		set
		{
			_revisionView = value;
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

	public NoteTabViewModel()
	{
		CloseSearchPopupCommand = new RelayCommand(_ => RequestCloseSearchPopup?.Invoke(this, EventArgs.Empty));
		DeleteCommand = new RelayCommand(Delete);
		FindNextCommand = new RelayCommand(FindNext);
		FindPreviousCommand = new RelayCommand(FindPrevious);
		NavigateNextCommand = new RelayCommand(NavigateNext);
		NavigatePreviousCommand = new RelayCommand(NavigatePrevious);
		ReturnCommand = new RelayCommand(Return);
		SaveCommand = new RelayCommand(Save);

		_initialPointer = Document.ContentStart;
	}

	private void CalculateCanSave()
	{
		CanSave = RevisionIndex != 0 || Document.Blocks.Count != OriginalBlockCount || !TextConverter.Save(Document, TextFormat.Xaml).Equals(OriginalText, StringComparison.Ordinal);
	}

	public override void Construct()
	{
		if (FinishedLoading)
			return;

		base.Construct();

		var offset = InitialPointer.DocumentStart.GetOffsetToPosition(InitialPointer);
		CaretPosition = Document.ContentStart.GetPositionAtOffset(offset);

		IsEnabled = !Record.Locked;
		Document.Focus();

		CanNavigateNext = false;
		CanNavigatePrevious = Record.GetNumRevisions() > 0;
		CanSave = false;
		LastChange = Record.Locked ? "Note locked by another user" : Record.GetNumRevisions() == 0 ? $"Entry created: {Record.GetCreated()}" : $"Entry last modified: {Record.GetLastChange()}";
	}

	public void Deconstruct()
	{
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
	}

	private void Delete(object? param)
	{
		if (MessageBox.Show("Are you sure you want to permanently delete this note?", "Sylver Ink: Notification", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.No)
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
		RevisionIndex--;
		string revisionTime = RevisionIndex == 0U ? Record.GetLastChange() : Record.GetRevisionTime(RevisionIndex);

		CanNavigateNext = RevisionIndex > 0;
		CanNavigatePrevious = RevisionIndex < Record.GetNumRevisions();
		Document = Record.GetDocument(RevisionIndex);
		IsReadOnly = RevisionIndex != 0;
		LastChange = (RevisionIndex == 0U ? "Entry last modified: " : $"Revision {Record.GetNumRevisions() - RevisionIndex} from ") + revisionTime;
		RevisionView = true;
		SaveLabel = RevisionIndex != 0 ? "Restore" : "Save";
		CalculateCanSave();
	}

	private void NavigatePrevious(object? param)
	{
		RevisionIndex++;
		string revisionTime = RevisionIndex == Record.GetNumRevisions() ? Record.GetCreated() : Record.GetRevisionTime(RevisionIndex);

		RevisionView = true;

		CanNavigateNext = RevisionIndex > 0;
		CanNavigatePrevious = RevisionIndex + 1 <= Record.GetNumRevisions();
		CanSave = true;
		Document = Record.GetDocument(RevisionIndex);
		IsReadOnly = RevisionIndex != 0;
		LastChange = (RevisionIndex == Record.GetNumRevisions() ? "Entry created " : $"Revision {Record.GetNumRevisions() - RevisionIndex} from ") + revisionTime;
		SaveLabel = "Restore";
	}

	private void Return(object? param)
	{
		if (CanSave && SaveLabel.Equals("Save", StringComparison.Ordinal))
		{
			switch (MessageBox.Show("You have unsaved changes. Save before closing this note?", "Sylver Ink: Notification", MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
			{
				case MessageBoxResult.Cancel:
					return;
				case MessageBoxResult.Yes:
					CurrentDatabase.CreateRevision(Record, TextConverter.Save(Document, TextFormat.Xaml));
					DeferUpdateRecentNotes();
					break;
			}
		}
		CurrentDatabase.Transmit(NetworkUtils.MessageType.RecordUnlock, Record.Index.ToByteArray());
		PreviousOpenNote = Record;
		Deconstruct();
	}

	private void Save(object? param)
	{
		var newText = TextConverter.Save(Document, TextFormat.Xaml);
		Record.DB?.CreateRevision(Record, newText);
		DeferUpdateRecentNotes();

		CanNavigateNext = false;
		CanNavigatePrevious = true;
		CanSave = false;
		IsEnabled = true;
		LastChange = "Entry last modified: " + Record.GetLastChange();
		OriginalBlockCount = Document.Blocks.Count;
		OriginalRevisionCount = Record.GetNumRevisions();
		OriginalText = newText;
		RevisionIndex = 0U;
	}

	public void TextChanged()
	{
		if (!FinishedLoading)
			return;

		if (RevisionView)
			return;

		CalculateCanSave();
		Autosave();
	}
}
