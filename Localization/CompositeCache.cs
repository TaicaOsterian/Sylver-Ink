using System.Text;

namespace SylverInk.Localization;

/// <summary>
/// A static collection of CompositeFormat objects computed at runtime to save energy.
/// </summary>
public static class CompositeCache
{
    public static readonly CompositeFormat CacheCouldNotLoadDatabase = CompositeFormat.Parse(Strings.CouldNotLoadDatabase);
    public static readonly CompositeFormat CacheDaysPassed = CompositeFormat.Parse(Strings.DaysPassed);
    public static readonly CompositeFormat CacheFailedToOpenServer = CompositeFormat.Parse(Strings.FailedToOpenServer);
    public static readonly CompositeFormat CacheFailedToProcessFile = CompositeFormat.Parse(Strings.FailedToProcessFile);
    public static readonly CompositeFormat CacheImportFailed = CompositeFormat.Parse(Strings.ImportFailed);
    public static readonly CompositeFormat CacheImportMeasurementText = CompositeFormat.Parse(Strings.Import_MeasurementText);
    public static readonly CompositeFormat CacheLabelNoteNumber = CompositeFormat.Parse(Strings.Label_NoteNumber);
    public static readonly CompositeFormat CacheLabelNotesImported = CompositeFormat.Parse(Strings.Label_NotesImported);
    public static readonly CompositeFormat CacheMessageDatabaseAutosaved = CompositeFormat.Parse(Strings.Message_DatabaseAutosaved);
    public static readonly CompositeFormat CacheMessageFileMovedOrDeleted = CompositeFormat.Parse(Strings.Message_FileMovedOrDeleted);
    public static readonly CompositeFormat CacheMessageInvalidPath = CompositeFormat.Parse(Strings.Message_InvalidPath);
    public static readonly CompositeFormat CacheMessageUpdateAvailable = CompositeFormat.Parse(Strings.Message_UpdateAvailable);
    public static readonly CompositeFormat CacheNoteEntryCreated = CompositeFormat.Parse(Strings.Note_EntryCreated);
    public static readonly CompositeFormat CacheNoteEntryModified = CompositeFormat.Parse(Strings.Note_EntryModified);
    public static readonly CompositeFormat CacheNoteIndexLabel = CompositeFormat.Parse(Strings.Note_IndexLabel);
    public static readonly CompositeFormat CacheNoteRevisionID = CompositeFormat.Parse(Strings.Note_RevisionID);
    public static readonly CompositeFormat CacheRenameDatabaseAlreadyExists = CompositeFormat.Parse(Strings.Rename_DatabaseAlreadyExists);
    public static readonly CompositeFormat CacheTextNoConverterRegistered = CompositeFormat.Parse(Strings.Text_NoConverterRegistered);
    public static readonly CompositeFormat CacheTextNoParserRegistered = CompositeFormat.Parse(Strings.Text_NoParserRegistered);
    public static readonly CompositeFormat CacheTextNoSaverRegistered = CompositeFormat.Parse(Strings.Text_NoSaverRegistered);
    public static readonly CompositeFormat CacheUnableToLoadDatabase = CompositeFormat.Parse(Strings.UnableToLoadDatabase);
    public static readonly CompositeFormat CacheUnableToUpdate = CompositeFormat.Parse(Strings.UnableToUpdate);
    public static readonly CompositeFormat CacheWordCount = CompositeFormat.Parse(Strings.WordCount);
}
