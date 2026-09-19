using SylverInk.FileIO;
using System.Collections;
using System.Globalization;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.Notes;

public partial class NoteController : IDisposable
{
    private short _canCompress; // -1 = Cannot compress, 1 = Can compress, 0 = Not tested.
    private bool _changed;
    private Serializer? _serializer;
    private byte? Structure;

    public bool Changed
    {
        get => _changed;
        set
        {
            _changed = value;
            DatabaseChanged = DatabaseChanged || value;
        }
    }
    public Database? DB { get; set; }
    public bool EnforceNoForwardCompatibility { get; private set; }
    public int Format { get; set; } = HighestSIDBFormat;
    private bool IndicesDirty { get; set; } = true;
    public List<NoteRecord> IndexedRecords { get; private set; } = [];
    public bool Loaded { get; set; }
    public string? Name { get; set; }
    public int RecordCount => Records.Count;
    private readonly Dictionary<SortType, Comparison<NoteRecord>> Sortings = new([
        new(SortType.ByChange, new Comparison<NoteRecord>((_rev1, _rev2) => _rev2.GetLastChangeObject().CompareTo(_rev1.GetLastChangeObject()))),
        new(SortType.ByCreation, new Comparison<NoteRecord>((_rev1, _rev2) => _rev2.GetCreatedObject().CompareTo(_rev1.GetCreatedObject()))),
        new(SortType.ByIndex, new Comparison<NoteRecord>((_rev1, _rev2) => _rev1.Index.CompareTo(_rev2.Index)))
        ]);
    public Hashtable Records { get; } = [];
    public Guid UUID { get; set; } = MakeUUID(UUIDType.Database);
    public Dictionary<string, double> WordPercentages { get; } = [];

    public NoteController(Database? DB = null)
    {
        this.DB = DB;
        InitializeRecords();
        Loaded = true;
    }

    public NoteController(string dbFile, Database? DB = null)
    {
        this.DB = DB;

        ReloadSerializer();

        if (!File.Exists(dbFile) || !_serializer?.OpenRead(dbFile) is true)
        {
            string backup = FindBackup(dbFile);
            if (!string.IsNullOrWhiteSpace(backup))
            {
                ReloadSerializer();
                if (!_serializer?.OpenRead(backup) is true)
                {
                    MessageBox.Show(string.Format(CultureInfo.CurrentCulture, CacheUnableToLoadDatabase, dbFile), Strings.Title_Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
        }

        InitializeRecords();
        ReloadSerializer();
        Loaded = true;
    }

    private Guid AddRecord(NoteRecord record)
    {
        Records.Add(record.UUID, record);
        IndicesDirty = true;
        RefreshRecentNotes();
        return record.UUID;
    }

    public void Autosave(string filename)
    {
        PropagateIndices();
        ReloadSerializer();

        if (!Open(filename, true, true))
            return;

        if (_serializer?.DatabaseFormat >= 7)
            _serializer?.WriteShortString(UUID.ToString());

        if (!_serializer?.Headless is true)
            _serializer?.WriteShortString(Name);

        if (_serializer?.DatabaseFormat >= 9)
            _serializer?.WriteByte(Structure ??= 0);

        _serializer?.WriteInt32(Records.Count);
        foreach (NoteRecord record in Records.Values)
            record.Serialize(_serializer);

        ReloadSerializer();
    }

    public Guid CreateRecord(string entry, Guid? uuid = null)
    {
        Changed = true;
        IndicesDirty = true;

        NoteRecord Record = new(
            Index: Records.Count,
            Initial: TextConverter.Convert(entry, TextFormat.Plaintext, TextFormat.Xaml),
            DB: DB,
            UUID: uuid);

        return AddRecord(Record);
    }

    public void CreateRevision(Guid uuid, string NewVersion) => CreateRevision(GetRecord(uuid), NewVersion);

    public static void CreateRevision(NoteRecord? record, string NewVersion) => record?.CreateRevision(NewVersion);

    public void DeleteRecord(Guid uuid)
    {
        NoteRecord? record = GetRecord(uuid);
        if (record is null)
            return;

        Records.Remove(uuid);

        for (int i = OpenQueries.Count - 1; i > -1; i--)
            OpenQueries[i].RequestClose(record);

        DB?.RemovePreviousNote(record);
        RemoveRecordTab(record);
        record.Delete();

        Changed = true;
        IndicesDirty = true;
        PropagateIndices();

        RefreshRecentNotes();
    }

    public void DeserializeRecords()
    {
        if (_serializer is null)
            ReloadSerializer();

        Format = _serializer?.DatabaseFormat ?? HighestSIDBFormat;

        if (Format > HighestSIDBFormat)
        {
            EnforceNoForwardCompatibility = true;
            _serializer?.Close();
            MessageBox.Show(Strings.Message_DatabaseTooNew, Strings.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (Format >= 7)
        {
            string uuidString = _serializer?.ReadShortString() ?? string.Empty;
            if (!Guid.TryParse(uuidString, out var uuid))
                uuid = MakeUUID(UUIDType.Database);
            UUID = uuid;
        }

        if (!_serializer?.Headless is true)
            Name = _serializer?.ReadShortString();

        if (Format >= 9)
            Structure = _serializer?.ReadByte();

        int recordCount = _serializer?.ReadInt32() ?? 0;
        for (int i = 0; i < recordCount; i++)
        {
            NoteRecord record = new(DB);
            AddRecord(record.Deserialize(_serializer));
        }

        _serializer?.Close();
        Changed = false;
        IndicesDirty = true;
        PropagateIndices();
    }

    public void Dispose()
    {
        _serializer?.Dispose();
        GC.SuppressFinalize(this);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Database otherDB)
        {
            if (!otherDB.Name?.Equals(Name, StringComparison.Ordinal) is true)
                return false;

            if (!otherDB.UUID.Equals(UUID))
                return false;

            return true;
        }

        if (obj is NoteController otherController)
        {
            if (!otherController.Name?.Equals(Name, StringComparison.Ordinal) is true)
                return false;

            if (!otherController.UUID.Equals(UUID))
                return false;

            return true;
        }

        return false;
    }

    public void EraseDatabase()
    {
        foreach (NoteRecord record in Records.Values)
            DeleteRecord(record.UUID);

        IndicesDirty = true;
        RefreshRecentNotes();
    }

    private static string FindBackup(string dbFile)
    {
        var Extensionless = Path.GetFileNameWithoutExtension(dbFile);
        for (int i = 1; i < 4; i++)
        {
            string backup = Path.Join(Path.GetDirectoryName(dbFile), $"{Extensionless}_{i}.sibk");
            if (File.Exists(backup))
                return backup;
        }

        return string.Empty;
    }

    public override int GetHashCode()
    {
        int parse = 0;
        var arr = UUID.ToByteArray();
        for (int i = 0; i < 4; i++)
        {
            var span = arr.AsSpan(i, 4).ToArray();
            parse ^= IntFromBytes(span);
        }
        return parse;
    }

    public NoteRecord? GetRecord(int index)
    {
        PropagateIndices();

        try
        {
            return IndexedRecords[index];
        }
        catch
        {
            return null;
        }
    }

    public NoteRecord? GetRecord(Guid uuid) => Records[uuid] as NoteRecord;

    public bool HasRecord(Guid uuid)
    {
        return Records[uuid] != null;
    }

    public void InitializeRecords(bool newDatabase = true)
    {
        for (int i = (OpenQueries ?? []).Count; i > 0; i--)
            OpenQueries?[i - 1].RequestClose();

        if (newDatabase)
            Records.Clear();

        DeserializeRecords();
    }

    public void MakeBackup()
    {
        ReloadSerializer();

        string file = DialogFileSelect(true, 1, Name);

        if (!_serializer?.OpenWrite($"{file}") is true)
            return;

        SerializeRecords();
        ReloadSerializer();
    }

    public bool Open(string path, bool writing = false, bool hidden = false)
    {
        _serializer = new()
        {
            DatabaseFormat = (byte)Format,
            Flags = 1, // UseLZW
            Hidden = hidden,
        };

        if (writing)
            return _serializer.OpenWrite(path);

        return _serializer.OpenRead(path);
    }

    public void PropagateIndices()
    {
        if (!IndicesDirty)
            return;

        IndexedRecords.Clear();

        foreach (NoteRecord record in Records.Values)
            IndexedRecords.Add(record);

        IndexedRecords.Sort((r1, r2) => r1.GetCreatedObject().CompareTo(r2.GetCreatedObject()));

        for (int i = 0; i < IndexedRecords.Count; i++)
            IndexedRecords[i].Index = i;

        IndicesDirty = false;
    }

    public void ReloadSerializer()
    {
        _serializer?.Close();
        _serializer = new()
        {
            DatabaseFormat = (byte)Format,
            Flags = 1 // UseLZW
        };

        if (_canCompress == -1 || (_canCompress == 0 && !TestCanCompress()))
        {
            if (_serializer.DatabaseFormat > 14)
                _serializer.Flags = 0; // !UseLZW
            else
                _serializer.DatabaseFormat--;
        }
    }

    public void Revert(DateTime targetDate)
    {
        foreach (NoteRecord record in Records.Values)
        {
            for (int j = OpenQueries.Count - 1; j > -1; j--)
                Concurrent(OpenQueries[j].RequestClose, record);

            RemoveRecordTab(record);

            var RecordDate = record.GetCreatedObject().ToLocalTime();
            var comparison = RecordDate.CompareTo(targetDate);
            if (comparison > 0)
            {
                DeleteRecord(record.UUID);
                continue;
            }

            for (int j = record.GetNumRevisions(); j > 0; j--)
            {
                var RevisionDate = DateTime.FromBinary(record.GetRevision(j - 1).Created).ToLocalTime();
                comparison = RevisionDate.CompareTo(targetDate);
                if (comparison <= 0)
                    continue;

                record.DeleteRevision(j - 1);
                Changed = true;
            }
        }

        Changed = true;
        IndicesDirty = true;
        PropagateIndices();
        RefreshRecentNotes();
    }

    public byte[]? SerializeRecords(bool inMemory = false)
    {
        PropagateIndices();

        if (inMemory)
        {
            ReloadSerializer();
            _serializer?.OpenWrite();
        }

        if (_serializer?.DatabaseFormat >= 7)
            _serializer?.WriteShortString(UUID.ToString());

        if (!_serializer?.Headless is true)
            _serializer?.WriteShortString(Name);

        if (_serializer?.DatabaseFormat >= 9)
            _serializer?.WriteByte(Structure ??= 0);

        _serializer?.WriteInt32(Records.Count);
        foreach (NoteRecord record in Records.Values)
            record.Serialize(_serializer);

        if (inMemory)
            return _serializer?.GetOutgoingStream();

        Changed = false;
        ReloadSerializer();
        return null;
    }

    public List<NoteRecord> Sort(SortType type = SortType.ByIndex)
    {
        List<NoteRecord> orderedList = [];

        foreach (NoteRecord record in Records.Values)
            orderedList.Add(record);

        orderedList.Sort(Sortings[type]);

        return orderedList;
    }

    public bool TestCanCompress()
    {
        if (Changed)
            _canCompress = 0;

        if (_canCompress != 0)
            return _canCompress == 1;

        try
        {
            string? _name = Name;
            byte? _structure = Structure;
            int recordCount = 0;

            _serializer?.BeginCompressionTest();

            _serializer?.WriteInt32(Records.Count);
            foreach (NoteRecord record in Records.Values)
                record.Serialize(_serializer);
            _serializer?.WriteString(_name);
            _serializer?.WriteByte(_structure ??= 1);

            _serializer?.EndCompressionTest();

            recordCount = _serializer?.ReadInt32() ?? 0;
            for (int i = 0; i < recordCount; i++)
                new NoteRecord().Deserialize(_serializer);
            _serializer?.ReadString();
            _structure = _serializer?.ReadByte();
        }
        catch
        {
            _serializer?.ClearCompressionTest();
            _canCompress = -1;
            ReloadSerializer();
            return false;
        }

        _serializer?.ClearCompressionTest();
        _canCompress = 1;
        ReloadSerializer();
        return true;
    }

    public void UpdateWordPercentages()
    {
        uint total = 0U;
        WordPercentages.Clear();

        foreach (NoteRecord record in Records.Values)
        {
            string recordText = record.ToString();
            var matches = Lowercase().Matches(recordText.ToLowerInvariant());
            foreach (Match m in matches)
            {
                foreach (Group group in m.Groups.Values)
                {
                    WordPercentages.TryAdd(group.Value, 0.0);
                    WordPercentages[group.Value]++;
                    total++;
                }
            }
        }

        foreach (string key in WordPercentages.Keys.ToList())
        {
            double value = WordPercentages[key];
            WordPercentages[key] = 100.0 * value / total;
        }
    }

    [GeneratedRegex(@"(\p{Ll}+)")]
    private partial Regex Lowercase();
}