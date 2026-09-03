using System.Text;

namespace SylverInk.Localization;

/// <summary>
/// A static collection of CompositeFormat objects computed at runtime to save energy.
/// </summary>
public static class CompositeCache
{
    public static readonly CompositeFormat CacheCouldNotLoadDatabase = CompositeFormat.Parse(Resources.CouldNotLoadDatabase);
    public static readonly CompositeFormat CacheDaysPassed = CompositeFormat.Parse(Resources.DaysPassed);
    public static readonly CompositeFormat CacheFailedToOpenServer = CompositeFormat.Parse(Resources.FailedToOpenServer);
    public static readonly CompositeFormat CacheFailedToProcessFile = CompositeFormat.Parse(Resources.FailedToProcessFile);
    public static readonly CompositeFormat CacheImportFailed = CompositeFormat.Parse(Resources.ImportFailed);
    public static readonly CompositeFormat CacheImportMeasurementText = CompositeFormat.Parse(Resources.Import_MeasurementText);
    public static readonly CompositeFormat CacheMessageDatabaseAutosaved = CompositeFormat.Parse(Resources.Message_DatabaseAutosaved);
    public static readonly CompositeFormat CacheMessageFileMovedOrDeleted = CompositeFormat.Parse(Resources.Message_FileMovedOrDeleted);
    public static readonly CompositeFormat CacheMessageInvalidPath = CompositeFormat.Parse(Resources.Message_InvalidPath);
    public static readonly CompositeFormat CacheMessageUpdateAvailable = CompositeFormat.Parse(Resources.Message_UpdateAvailable);
    public static readonly CompositeFormat CacheNoteEntryCreated = CompositeFormat.Parse(Resources.Note_EntryCreated);
    public static readonly CompositeFormat CacheNoteEntryModified = CompositeFormat.Parse(Resources.Note_EntryModified);
    public static readonly CompositeFormat CacheNoteIndexLabel = CompositeFormat.Parse(Resources.Note_IndexLabel);
    public static readonly CompositeFormat CacheNoteNumber = CompositeFormat.Parse(Resources.NoteNumber);
    public static readonly CompositeFormat CacheNoteRevisionID = CompositeFormat.Parse(Resources.Note_RevisionID);
    public static readonly CompositeFormat CacheNotesImported = CompositeFormat.Parse(Resources.NotesImported);
    public static readonly CompositeFormat CacheRenameDatabaseAlreadyExists = CompositeFormat.Parse(Resources.Rename_DatabaseAlreadyExists);
    public static readonly CompositeFormat CacheTextNoConverterRegistered = CompositeFormat.Parse(Resources.Text_NoConverterRegistered);
    public static readonly CompositeFormat CacheTextNoParserRegistered = CompositeFormat.Parse(Resources.Text_NoParserRegistered);
    public static readonly CompositeFormat CacheTextNoSaverRegistered = CompositeFormat.Parse(Resources.Text_NoSaverRegistered);
    public static readonly CompositeFormat CacheUnableToLoadDatabase = CompositeFormat.Parse(Resources.UnableToLoadDatabase);
    public static readonly CompositeFormat CacheUnableToUpdate = CompositeFormat.Parse(Resources.UnableToUpdate);
    public static readonly CompositeFormat CacheWordCount = CompositeFormat.Parse(Resources.WordCount);
}
