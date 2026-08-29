using SylverInk.Net;
using SylverInk.Notes;
using SylverInk.Text;
using SylverInk.XAMLUtils;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using static SylverInk.CommonUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.Objects.ViewModels;

public class NoteEditorViewModel : ViewModelBase
{
	private bool _autosaving;
	private TextPointer _caretPosition;
	private FlowDocument _document;
	private bool _edited;
	private bool _finishedLoading;
	private bool _isEnabled = true;
	private string? _lastChange;
	private int _originalBlockCount;
	private int _originalRevisionCount;
	private string _originalText = string.Empty;
	private NoteRecord _record;
	private DateTime _timeSinceAutosave = DateTime.UtcNow;

	public bool Autosaving
	{
		get => _autosaving;
		set
		{
			_autosaving = value;
			OnPropertyChanged();
		}
	}

	public TextPointer CaretPosition
	{
		get => _caretPosition;
		set
		{
			_caretPosition = value;
			OnPropertyChanged();
		}
	}

	public FlowDocument Document
	{
		get => _document;
		set
		{
			_document = value;
			OnPropertyChanged();
		}
	}

	public bool Edited
	{
		get => _edited;
		set
		{
			_edited = value;
			OnPropertyChanged();
		}
	}

	public bool FinishedLoading
	{
		get => _finishedLoading;
		set
		{
			_finishedLoading = value;
			OnPropertyChanged();
		}
	}

	public bool IsEnabled
	{
		get => _isEnabled;
		set
		{
			_isEnabled = value;
			OnPropertyChanged();
		}
	}

	public string? LastChange
	{
		get => _lastChange;
		set
		{
			_lastChange = value;
			OnPropertyChanged();
		}
	}

	public int OriginalBlockCount
	{
		get => _originalBlockCount;
		set
		{
			_originalBlockCount = value;
			OnPropertyChanged();
		}
	}

	public int OriginalRevisionCount
	{
		get => _originalRevisionCount;
		set
		{
			_originalRevisionCount = value;
			OnPropertyChanged();
		}
	}

	public string OriginalText
	{
		get => _originalText;
		set
		{
			_originalText = value;
			OnPropertyChanged();
		}
	}

	public NoteRecord Record
	{
		get => _record;
		set
		{
			_record = value;
			OnPropertyChanged();
		}
	}

	public DateTime TimeSinceAutosave
	{
		get => _timeSinceAutosave;
		set
		{
			_timeSinceAutosave = value;
			OnPropertyChanged();
		}
	}

	public NoteEditorViewModel()
	{
		_record = new();
		_document = _record.GetDocument();
		_caretPosition = _document.ContentStart;
	}

	public void Autosave()
	{
		if (!FinishedLoading)
			return;

		if (Autosaving)
			return;

		Edited = Document.Blocks.Count != OriginalBlockCount || !OriginalText.Equals(TextConverter.Save(Document, TextFormat.Xaml), StringComparison.Ordinal);

		if (!Edited)
			return;

		Autosaving = true;
		Task.Factory.StartNew(() =>
		{
			SpinWait.SpinUntil(() => (DateTime.UtcNow - TimeSinceAutosave).Seconds >= 5);

			Concurrent(Record.Autosave, Document);
			RecentNotesDirty = true;
			TimeSinceAutosave = DateTime.UtcNow;
			Autosaving = false;
			return;
		}, TaskCreationOptions.LongRunning);
	}

	public virtual void Construct()
	{
		if (FinishedLoading)
			return;

		if (Record.Locked)
		{
			LastChange = "Locked by another user";
			IsEnabled = false;
		}
		else
		{
			LastChange = Record.GetLastChange();
			Record.DB?.Transmit(NetworkUtils.MessageType.RecordUnlock, Record.Index.ToByteArray());
		}

		Edited = false;
		Document = Record.GetDocument() ?? new();
		Document.Focus();

		OriginalBlockCount = Document.Blocks.Count;
		OriginalRevisionCount = Record.GetNumRevisions();
		OriginalText = TextConverter.Save(Document, TextFormat.Xaml);

		FinishedLoading = true;
	}

	public void RequestUnlock(NoteRecord source)
	{
		if (!Record.Equals(source))
			return;

		LastChange = source.GetLastChange();
		IsEnabled = true;
	}

	public void ScrollToText(string text) => FlowDocumentUtils.ScrollToText(Document, text);
}