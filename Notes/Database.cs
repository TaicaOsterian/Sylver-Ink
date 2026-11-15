using SylverInk.FileIO;
using SylverInk.Interop;
using SylverInk.XAML.Controls;
using System.Globalization;
using System.Text;
using System.Threading;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.Notes;

internal sealed class Database : IDisposable
{
    private NoteController Controller;
    private StackPanel? HeaderPanel;

    internal bool Changed { get => Controller.Changed; set => Controller.Changed = value; }
    internal NetClient Client { get; }
    internal long? Created { get; private set; }
    internal string DBFile { get; set; } = string.Empty;
    internal int Format { get => Controller.Format; set => Controller.Format = value; }
    internal bool Loaded { get; private set; }
    internal string? Name { get => Controller.Name; set => Controller.Name = value; }
    private LinkedStack<NoteRecord> PreviousOpenNotes { get; set; } = [];
    internal int RecordCount => Controller.RecordCount;
    internal NetServer Server { get; }
    internal Guid UUID { get => Controller.UUID; set => Controller.UUID = value; }
    internal Dictionary<string, double> WordPercentages => Controller.WordPercentages;

    internal Database()
    {
        Client = new(this);
        Controller = new(this);
        Loaded = Controller.Loaded;
        Server = new(this);
    }

    internal Database(string DBFile)
    {
        Client = new(this);
        Controller = new(this);
        this.DBFile = DBFile;
        Loaded = Controller.Loaded;
        Server = new(this);
    }

    internal async Task Autosave(CancellationTokenSource? cts = null)
    {
        if (!Changed)
            return;

        if (string.IsNullOrWhiteSpace(DBFile))
            DBFile = GetDatabasePath(this);

        var lockFile = GetLockFile(DBFile);
        var lockDir = Path.GetDirectoryName(lockFile);

        try
        {
            if (!string.IsNullOrWhiteSpace(lockDir))
                Directory.CreateDirectory(lockDir);
        }
        catch (Exception e)
        {
            App.LogException(e);
        }

        Controller.Autosave(lockFile, cts);
    }

    internal static async Task Create(string dbFile)
    {
        Database? db = null;
        try
        {
            db = new(dbFile);
            AddDatabase(db);
            await Task.Run(db.Load);
        }
        catch (Exception e)
        {
            App.LogException(e);
            if (db is not null)
                RemoveDatabase(db);

            ShowTooltip(string.Format(CultureInfo.CurrentCulture, CacheCouldNotLoadDatabase, dbFile));
        }
    }

    internal Guid CreateRecord(string entry, bool local = true)
    {
        Guid uuid = Controller.CreateRecord(entry);

        if (local)
        {
            var uuidBuffer = Encoding.UTF8.GetBytes(uuid.ToString());
            var outBuffer = new List<byte>([0, 0, 0, 0, .. uuidBuffer.Length.ToByteArray(), .. uuidBuffer, .. entry.Length.ToByteArray()]);

            if (entry.Length > 0)
                outBuffer.AddRange(Encoding.UTF8.GetBytes(entry.ToString()));

            Transmit(NetworkUtils.MessageType.RecordAdd, [.. outBuffer]);
        }

        RefreshRecentNotes();

        return uuid;
    }

    internal void CreateRecord(string entry, Guid uuid, bool local = true)
    {
        Controller.CreateRecord(entry, uuid);

        if (local)
        {
            var uuidBuffer = Encoding.UTF8.GetBytes(uuid.ToString());
            var outBuffer = new List<byte>([0, 0, 0, 0, .. uuidBuffer.Length.ToByteArray(), .. uuidBuffer, .. entry.Length.ToByteArray()]);

            if (entry.Length > 0)
                outBuffer.AddRange(Encoding.UTF8.GetBytes(entry.ToString()));

            Transmit(NetworkUtils.MessageType.RecordAdd, [.. outBuffer]);
        }

        RefreshRecentNotes();
    }

    internal void CreateRevision(Guid uuid, string newVersion, bool local = true)
    {
        Controller.CreateRevision(uuid, newVersion);

        if (!local)
            return;

        var textBuffer = Encoding.UTF8.GetBytes(uuid.ToString());
        var outBuffer = new List<byte>([
            .. textBuffer.Length.ToByteArray(),
            .. textBuffer,
            .. newVersion.Length.ToByteArray()
        ]);

        if (newVersion.Length > 0)
            outBuffer.AddRange(Encoding.UTF8.GetBytes(newVersion));

        Transmit(NetworkUtils.MessageType.TextInsert, [.. outBuffer]);
    }

    internal void CreateRevision(NoteRecord record, string newVersion, bool local = true) => CreateRevision(record.UUID, newVersion, local);

    internal void DeleteRecord(Guid uuid, bool local = true)
    {
        Controller.DeleteRecord(uuid);

        if (local)
            Transmit(NetworkUtils.MessageType.RecordRemove, uuid.ToString());
    }

    internal void DeleteRecord(NoteRecord record, bool local = true)
    {
        Controller.DeleteRecord(record.UUID);

        if (local)
            Transmit(NetworkUtils.MessageType.RecordRemove, record.UUID.ToString());
    }

    public void Dispose()
    {
        Controller.Dispose();
        GC.SuppressFinalize(this);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Database otherDB)
        {
            if (!otherDB.Name?.Equals(Name, StringComparison.OrdinalIgnoreCase) is true)
                return false;

            if (!otherDB.UUID.Equals(UUID))
                return false;

            return true;
        }

        if (obj is NoteController otherController)
        {
            if (!otherController.Name?.Equals(Name, StringComparison.OrdinalIgnoreCase) is true)
                return false;

            if (!otherController.UUID.Equals(UUID))
                return false;

            return true;
        }

        return false;
    }

    internal void Erase()
    {
        if (Client.Connected || Server.Serving)
            return;

        Controller.EraseDatabase();
    }

    internal void FormatName(bool newDB = false)
    {
        if (string.IsNullOrWhiteSpace(Name))
            Name = DefaultDatabase;

        var index = GetFirstAvailableIndex(this, newDB);
        if (index != 0)
            Name = $"{Name} ({index})";
    }

    internal string GetCreated()
    {
        if (Created is not null)
            return DateTime.FromBinary((long)Created).ToLocalTime().ToString(DateFormat, CultureInfo.InvariantCulture);

        var CreatedObject = DateTime.UtcNow;
        foreach (NoteRecord otherRecord in Controller.Records.Values)
        {
            var other = otherRecord.GetCreatedObject();

            if (CreatedObject.CompareTo(other) > 0)
                CreatedObject = other;
        }

        Created = CreatedObject.ToBinary();
        return CreatedObject.ToLocalTime().ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    public override int GetHashCode() => Controller.GetHashCode();

    internal StackPanel GetHeader()
    {
        string? headerContent;
        Label label;
        if (HeaderPanel is not null)
        {
            HeaderPanel.Dispatcher.Invoke(() =>
            {
                label = (Label)HeaderPanel.Children[0];

                headerContent = Name;
                if (headerContent?.Length > 12)
                    headerContent = $"{headerContent[..10]}...";

                HeaderPanel.ToolTip = Name;
                label.Content = headerContent;
            });

            return HeaderPanel;
        }

        headerContent = Name;
        if (headerContent?.Length > 12)
            headerContent = $"{headerContent[..10]}...";

        label = new Label()
        {
            Content = headerContent,
            Margin = new(0),
        };

        HeaderPanel = new StackPanel()
        {
            Margin = new(0, -4, 0, 0),
            Orientation = Orientation.Horizontal,
            ToolTip = Name,
        };

        // As a courtesy to the user, if the active database's header is clicked, bring them to the Plus tab for that database.
        HeaderPanel.MouseDown += (_, _) =>
        {
            if (!CurrentDatabase.Equals(this))
                return;

            if (GetCurrentDatabaseControl() is not DatabaseControl control)
                return;

            control.Controller.SelectedItem = control.PlusTab;
        };

        HeaderPanel.Children.Add(label);
        HeaderPanel.Children.Add((Client.Active ? Client.Indicator : Server.Indicator) ?? new System.Windows.Shapes.Ellipse());

        return HeaderPanel;
    }

    internal int GetPreviousNoteCount() => PreviousOpenNotes.Count();

    internal NoteRecord? GetRecord(int index) => Controller.GetRecord(index);

    internal NoteRecord? GetRecord(Guid uuid) => Controller.GetRecord(uuid);

    internal bool HasRecord(Guid uuid) => Controller.HasRecord(uuid);

    internal void Initialize(bool newDatabase = true)
    {
        Controller.InitializeRecords(newDatabase);
        RefreshRecentNotes();
    }

    internal bool Load()
    {
        if (string.IsNullOrWhiteSpace(DBFile))
            return false;

        Load(DBFile);
        Concurrent(GetHeader);
        return true;
    }

    internal void Load(string dbFile)
    {
        Controller = new(DBFile = dbFile, this);

        if (Controller.EnforceNoForwardCompatibility)
        {
            Loaded = false;
            throw new NotSupportedException(Strings.DatabaseTooNew);
        }

        var lockFile = GetLockFile(dbFile);
        if (File.Exists(lockFile) && (
            File.Exists(SessionFile)
            || MessageBox.Show(string.Format(
                    CultureInfo.CurrentCulture,
                    CacheMessageDatabaseAutosaved,
                    Path.GetFileName(dbFile)),
                Strings.Title_Notification,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information) == MessageBoxResult.Yes)) // Ew.
        {
            Controller.RestoreAutosave(lockFile);
            Changed = true;
        }

        Loaded = Controller.Loaded;

        if (string.IsNullOrWhiteSpace(Name))
            Name = Path.GetFileNameWithoutExtension(DBFile);

        if (Path.GetExtension(DBFile).Equals(".sibk", SystemStringComparison))
            Name = $"{Strings.Word_Backup}: {Name}";

        RefreshRecentNotes();
    }

    internal void Lock(Guid uuid, bool local = false)
    {
        var record = Controller.GetRecord(uuid);

        if (local)
        {
            Transmit(NetworkUtils.MessageType.RecordLock, uuid.ToString());
            return;
        }

        record?.Lock();
    }

    internal void MakeBackup(bool auto = false)
    {
        var DBPath = GetDatabasePath(this);
        var BKPath = GetBackupPath(this);
        var BKFolder = Path.GetDirectoryName(BKPath);

        try
        {
            if (!string.IsNullOrWhiteSpace(BKFolder))
                Directory.CreateDirectory(BKFolder);
        }
        catch (Exception e)
        {
            App.LogException(e);
        }

        if (!auto)
        {
            Controller.MakeBackup();
            return;
        }

        for (int i = 2; i > 0; i--)
        {
            if (File.Exists($"{BKPath}_{i}.sibk"))
                File.Move($"{BKPath}_{i}.sibk", $"{BKPath}_{i + 1}.sibk", true);
        }

        if (File.Exists($"{DBPath}"))
            File.Copy($"{DBPath}", $"{BKPath}_1.sibk", true);
    }

    internal bool Open(string path, bool writing = false) => Controller.Open(path, writing);

    internal void PopPreviousNote()
    {
        if (!PreviousOpenNotes.Any())
            return;

        OpenQuery(PreviousOpenNotes.Pop());
    }

    internal void PushPreviousNote(NoteRecord record) => PreviousOpenNotes.Push(record);

    internal void RemovePreviousNote(int index) => PreviousOpenNotes.RemoveAt(index);

    internal void RemovePreviousNote(NoteRecord record) => PreviousOpenNotes.Remove(record);

    internal void Rename(string newName)
    {
        var oldFile = DBFile;
        var oldName = Name;

        Name = newName;
        GetHeader();

        if (!File.Exists(oldFile))
            return;

        var directorySearch = Directory.GetDirectories(
            Subfolders[Subfolder.Databases],
            "*",
            new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                MaxRecursionDepth = 3
            });

        var oldPath = Path.GetDirectoryName(oldFile) ?? Subfolders[Subfolder.Databases];
        var isStandardDirectory = directorySearch.Contains(oldPath) && !Subfolders[Subfolder.Databases].Equals(oldPath, SystemStringComparison);

        var newFile = isStandardDirectory
            ? Path.Join(Subfolders[Subfolder.Databases], Name, $"{Name}.sidb")
            : Path.Combine(oldPath, $"{Name}.sidb");
        var newPath = Path.GetDirectoryName(newFile);

        if (File.Exists(newFile) && MessageBox.Show(string.Format(CultureInfo.CurrentCulture, CacheRenameDatabaseAlreadyExists, newPath), Strings.Title_Warning, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
        {
            Name = oldName;
            GetHeader();
            return;
        }

        DBFile = newFile;

        if (isStandardDirectory)
            Directory.Delete(oldPath, true);
        else
            FileUtils.Erase(oldFile);

        Save();
    }

    internal void Revert(DateTime targetDate) => Controller.Revert(targetDate);

    /// <summary>
    /// Write the database object to a file using its systemic filename.
    /// </summary>
    internal void Save() => Save(DBFile);

    /// <summary>
    /// Write the database object to a file using a provided filename.
    /// </summary>
    internal void Save(string? targetFile = null)
    {
        if (!Changed)
            return;

        if (string.IsNullOrWhiteSpace(DBFile))
            DBFile = GetDatabasePath(this);

        if (string.IsNullOrWhiteSpace(targetFile))
            targetFile = DBFile;

        var targetDir = Path.GetDirectoryName(targetFile);

        try
        {
            if (!string.IsNullOrWhiteSpace(targetDir))
                Directory.CreateDirectory(targetDir);
        }
        catch (Exception e)
        {
            App.LogException(e);
        }

        if (DBFile.Equals(targetFile, SystemStringComparison))
            MakeBackup(true);

        if (!Controller.Open($"{targetFile}", true))
            return;

        Controller.SerializeRecords();

        FileUtils.Erase(GetLockFile(targetFile));

        if (targetFile.Contains(Subfolders[Subfolder.Databases]))
            File.WriteAllText(Path.Join(targetDir, "uuid.dat"), UUID.ToString());
    }

    internal byte[]? SerializeRecords(bool inMemory = false) => Controller.SerializeRecords(inMemory);

    internal List<NoteRecord> Sort(SortType type = SortType.ByIndex)
    {
        if (type == SortType.ByIndex)
            Controller.PropagateIndices();

        return Controller.Sort(type);
    }

    internal void Transmit(NetworkUtils.MessageType type, byte[]? data)
    {
        if (data is null)
            return;

        if (Client.Connected)
            Client.Send(type, data);

        if (Server.Serving)
            Server.Broadcast(type, data);
    }

    internal void Transmit(NetworkUtils.MessageType type, string data)
    {
        var buffer = Encoding.UTF8.GetBytes(data);

        if (Client.Connected)
            Client.Send(type, [.. buffer.Length.ToByteArray(), .. buffer]);

        if (Server.Serving)
            Server.Broadcast(type, [.. buffer.Length.ToByteArray(), .. buffer]);
    }

    internal void Unlock(Guid uuid, bool local = false)
    {
        if (local)
        {
            Transmit(NetworkUtils.MessageType.RecordUnlock, uuid.ToString());
            return;
        }

        Controller.GetRecord(uuid)?.Unlock();
    }

    internal void UpdateWordPercentages() => Controller.UpdateWordPercentages();
}
