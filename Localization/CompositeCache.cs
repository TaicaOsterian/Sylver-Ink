using System.Text;

namespace SylverInk.Localization;

/// <summary>
/// A static collection of CompositeFormat objects computed at runtime to save energy.
/// </summary>
internal static class CompositeCache
{
    internal static readonly CompositeFormat CacheBackupSuccess = CompositeFormat.Parse(Strings.Backup_Success!);
    internal static readonly CompositeFormat CacheCouldNotLoadDatabase = CompositeFormat.Parse(Strings.CouldNotLoadDatabase!);
    internal static readonly CompositeFormat CacheDaysPassed = CompositeFormat.Parse(Strings.DaysPassed!);
    internal static readonly CompositeFormat CacheFailedBackup = CompositeFormat.Parse(Strings.FailedBackup!);
    internal static readonly CompositeFormat CacheFailedToOpenServer = CompositeFormat.Parse(Strings.FailedToOpenServer!);
    internal static readonly CompositeFormat CacheFailedToProcessFile = CompositeFormat.Parse(Strings.FailedToProcessFile!);
    internal static readonly CompositeFormat CacheKeyInUse = CompositeFormat.Parse(Strings.Tooltip_KeyInUse!);
    internal static readonly CompositeFormat CacheImageSuccessfullyExported = CompositeFormat.Parse(Strings.Tooltip_ImageSuccessfullyExported!);
    internal static readonly CompositeFormat CacheImportFailed = CompositeFormat.Parse(Strings.ImportFailed!);
    internal static readonly CompositeFormat CacheImportMeasurementText = CompositeFormat.Parse(Strings.Import_MeasurementText!);
    internal static readonly CompositeFormat CacheLabelNoteNumber = CompositeFormat.Parse(Strings.Label_NoteNumber!);
    internal static readonly CompositeFormat CacheLabelNotesImported = CompositeFormat.Parse(Strings.Label_NotesImported!);
    internal static readonly CompositeFormat CacheMessageDatabaseAutosaved = CompositeFormat.Parse(Strings.Message_DatabaseAutosaved!);
    internal static readonly CompositeFormat CacheMessageFileMovedOrDeleted = CompositeFormat.Parse(Strings.Message_FileMovedOrDeleted!);
    internal static readonly CompositeFormat CacheMessageInvalidPath = CompositeFormat.Parse(Strings.Message_InvalidPath!);
    internal static readonly CompositeFormat CacheMessageUpdateAvailable = CompositeFormat.Parse(Strings.Message_UpdateAvailable!);
    internal static readonly CompositeFormat CacheNewNoteButtonTooltip = CompositeFormat.Parse(Strings.NewNoteButtonTooltip!);
    internal static readonly CompositeFormat CacheNewNoteTooltip = CompositeFormat.Parse(Strings.NewNoteTooltip!);
    internal static readonly CompositeFormat CacheNoteEntryCreated = CompositeFormat.Parse(Strings.Note_EntryCreated!);
    internal static readonly CompositeFormat CacheNoteEntryModified = CompositeFormat.Parse(Strings.Note_EntryModified!);
    internal static readonly CompositeFormat CacheNoteIndexLabel = CompositeFormat.Parse(Strings.Note_IndexLabel!);
    internal static readonly CompositeFormat CacheNoteRevisionID = CompositeFormat.Parse(Strings.Note_RevisionID!);
    internal static readonly CompositeFormat CacheRecentNotesButtonTooltip = CompositeFormat.Parse(Strings.RecentNotesButtonTooltip!);
    internal static readonly CompositeFormat CacheRecentNotesTooltip = CompositeFormat.Parse(Strings.RecentNotesTooltip!);
    internal static readonly CompositeFormat CacheRenameDatabaseAlreadyExists = CompositeFormat.Parse(Strings.Rename_DatabaseAlreadyExists!);
    internal static readonly CompositeFormat CacheTextNoConverterRegistered = CompositeFormat.Parse(Strings.Text_NoConverterRegistered!);
    internal static readonly CompositeFormat CacheTextNoParserRegistered = CompositeFormat.Parse(Strings.Text_NoParserRegistered!);
    internal static readonly CompositeFormat CacheTextNoSaverRegistered = CompositeFormat.Parse(Strings.Text_NoSaverRegistered!);
    internal static readonly CompositeFormat CacheUnableToLoadDatabase = CompositeFormat.Parse(Strings.UnableToLoadDatabase!);
    internal static readonly CompositeFormat CacheUnableToUpdate = CompositeFormat.Parse(Strings.UnableToUpdate!);
    internal static readonly CompositeFormat CacheWordCount = CompositeFormat.Parse(Strings.WordCount!);
}
