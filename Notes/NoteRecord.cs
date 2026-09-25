using SylverInk.FileIO;
using System.Globalization;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.Text.FlowDocumentUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.Notes;

public struct NoteRevision(long created = -1, bool isAutosave = false, int startIndex = -1, string? substring = null, Guid? uuid = null)
{
    public long Created { get; set; } = created;
    public bool IsAutosave { get; set; } = isAutosave;
    public int StartIndex { get; set; } = startIndex;
    public string? Substring { get; set; } = substring;
    public Guid Uuid { get; set; } = uuid ?? MakeUUID(UUIDType.Revision);
}

public partial class NoteRecord
{
    private long Created = -1;
    private int _index = -1;
    private string? Initial = string.Empty;
    private long LastChange = -1;
    private DateTime LastChangeObject = DateTime.UtcNow;
    private string LastQuery = string.Empty;
    private readonly List<NoteRevision> Revisions = [];
    private readonly List<string> Tags = [];
    private bool TagsDirty = true;
    private Guid _uuid;

    public Database? DB { get; set; }
    public int Index { get => _index; set => _index = value; }
    public int LastMatchCount { get; set; }
    public bool Locked { get; set; }
    public Guid UUID { get => _uuid; set => _uuid = value; }

    public string FullDateChange
    {
        get
        {
            LastChangeObject = DateTime.FromBinary(LastChange);

            var dtObject = RecentEntriesSortMode switch
            {
                SortType.ByCreation => GetCreatedObject(),
                _ => LastChangeObject,
            };

            dtObject = dtObject.ToLocalTime();

            return $"{dtObject:d} {dtObject:t}";
        }
    }

    public string Preview
    {
        get
        {
            var _preview = FlowDocumentPreview(TextConverter.Parse(Reconstruct(), TextFormat.Xaml)).ReplaceLineEndings().Replace(Environment.NewLine, " ").Replace('\t', ' ');

            return string.IsNullOrEmpty(_preview) ? Strings.EmptyNote : _preview;
        }
    }

    public string ShortChange
    {
        get
        {
            LastChangeObject = DateTime.FromBinary(LastChange);

            var dtObject = RecentEntriesSortMode switch
            {
                SortType.ByCreation => GetCreatedObject(),
                _ => LastChangeObject,
            };

            var diff = DateTime.UtcNow - dtObject;

            if (diff.TotalHours < 24.0)
                return dtObject.ToLocalTime().ToShortTimeString();

            if (diff.TotalHours < 168.0)
                return diff.Days == 1 ? Strings.DayPassed : string.Format(CultureInfo.CurrentCulture, CacheDaysPassed, diff.Days);

            return dtObject.ToLocalTime().ToShortDateString();
        }
    }

    public NoteRecord(Database? DB = null)
    {
        Created = DateTime.UnixEpoch.ToBinary();
        this.DB = DB;
        Initial = string.Empty;
        LastChange = Created;
        LastChangeObject = DateTime.FromBinary(LastChange);
        _uuid = MakeUUID(UUIDType.Record);
    }

    public NoteRecord(int Index, string Initial, Database? DB = null, long Created = -1, Guid? UUID = null)
    {
        this.Created = Created == -1 ? DateTime.UtcNow.ToBinary() : Created;
        this.DB = DB;
        _index = Index;
        this.Initial = Initial;
        LastChange = this.Created;
        LastChangeObject = DateTime.FromBinary(LastChange);
        _uuid = UUID ?? MakeUUID(UUIDType.Record);
    }

    private void Add(NoteRevision revision)
    {
        if (revision.Created == -1)
            revision.Created = DateTime.UtcNow.ToBinary();

        if (DateTime.FromBinary(revision.Created).CompareTo(LastChangeObject) > 0)
        {
            LastChange = revision.Created;
            LastChangeObject = DateTime.FromBinary(LastChange);
        }

        Revisions.Add(revision);
        TagsDirty = true;
    }

    // This function doesn't consume autosaved revisions to preserve continuity.
    // The onus is on the object initiating the call chain to track which revisions to discard if the user chooses not to save their changes.
    public void Autosave(FlowDocument document)
    {
        CreateRevision(TextConverter.Save(document, TextFormat.Xaml), Autosave: true);
        DB?.Autosave();
        RefreshRecentNotes();
    }

    public void CreateRevision(string NewVersion, bool Autosave = false)
    {
        string Current = ToXaml();
        int StartIndex = 0;

        if (NewVersion.Equals(Current, StringComparison.Ordinal))
            return;

        for (int i = 0; i < Math.Min(Current.Length, NewVersion.Length); i++)
        {
            if (!Current[i].Equals(NewVersion[i]))
                break;
            StartIndex = i;
        }

        Add(new()
        {
            Created = DateTime.UtcNow.ToBinary(),
            IsAutosave = Autosave,
            StartIndex = StartIndex,
            Substring = StartIndex >= NewVersion.Length ? string.Empty : NewVersion[StartIndex..],
            Uuid = MakeUUID(UUIDType.Revision)
        });

        if (DB is null)
            return;

        DB.Changed = true;
    }

    public void Delete()
    {
        Index = -1;
        Initial = string.Empty;
        LastChange = DateTime.UtcNow.ToBinary();
        Revisions.Clear();
        TagsDirty = true;

        RefreshRecentNotes();
    }

    // In its current state, this function is only well-behaved when removing all subsequent revisions in addition to the one marked for deletion.
    public void DeleteRevision(int index)
    {
        if (index >= GetNumRevisions())
            return;

        if (GetNumRevisions() > 0)
            Revisions.RemoveAt(index);

        LastChange = GetNumRevisions() == 0 ? Created : Revisions[GetNumRevisions() - 1].Created;
        LastChangeObject = DateTime.FromBinary(LastChange);

        RefreshRecentNotes();
    }

    public NoteRecord Deserialize(Serializer? serializer)
    {
        if (serializer?.DatabaseFormat >= 5)
        {
            string uuidString = serializer?.ReadShortString() ?? string.Empty;
            if (!Guid.TryParse(uuidString, out var uuid))
                uuid = MakeUUID(UUIDType.Record);
            UUID = uuid;
        }
        Created = serializer?.ReadLong() ?? DateTime.UtcNow.ToBinary();
        Index = serializer?.ReadInt32() ?? -1;
        Initial = serializer?.ReadString();
        LastChange = serializer?.ReadLong() ?? DateTime.UtcNow.ToBinary();

        int RevisionsCount = serializer?.ReadInt32() ?? 0;
        for (int i = 0; i < RevisionsCount; i++)
        {
            NoteRevision _revision = new();
            if (serializer?.DatabaseFormat >= 7)
            {
                string uuidString = serializer?.ReadShortString() ?? string.Empty;
                if (!Guid.TryParse(uuidString, out var uuid))
                    uuid = MakeUUID(UUIDType.Revision);
                _revision.Uuid = uuid;
            }
            _revision.Created = serializer?.ReadLong() ?? DateTime.UtcNow.ToBinary();
            if (serializer?.DatabaseFormat >= 16)
            {
                byte flags = serializer?.ReadByte() ?? 0;

                _revision.IsAutosave = (flags & 1) == 0;
            }
            _revision.StartIndex = serializer?.ReadInt32() ?? 0;
            _revision.Substring = serializer?.ReadString();
            Add(_revision);
        }

        // SIDB v.9 introduced XAML rich text formatting. Its absence in earlier versions must be accounted for.
        if (serializer?.DatabaseFormat < 9)
            TargetXaml();

        return this;
    }

    public override bool Equals(object? obj)
    {
        if (!GetType().Equals(obj?.GetType()))
            return false;

        var recordObj = (NoteRecord?)obj;
        return base.Equals(obj)
            || (Created.Equals(recordObj?.Created)
                && Index.Equals(recordObj?.Index)
                && Initial?.Equals(recordObj?.Initial, StringComparison.Ordinal) is true
                && LastChange.Equals(recordObj?.LastChange))
            || (UUID.Equals(recordObj?.UUID) is true);
    }

    private int ExtractTags()
    {
        if (!TagsDirty)
            return Tags.Count;

        Tags.Clear();

        var recordText = ToString();
        var matches = Lowercase().Matches(recordText.ToLowerInvariant());
        foreach (Match match in matches)
        {
            foreach (Group group in match.Groups.Values)
            {
                var val = group.Value.ToLowerInvariant();

                if (Tags.Contains(val))
                    continue;

                foreach (Database db in Databases)
                {
                    if (!db.WordPercentages.ContainsKey(val))
                        continue;

                    // To be treated as a tag, a word must be less common than 0.1% of all words in at least one database.
                    if (db.WordPercentages[val] < Math.Max(0.1, 100.0 - db.WordPercentages.Count))
                        Tags.Add(val);
                }
            }
        }

        TagsDirty = false;
        return Tags.Count;
    }

    public string GetCreated() => GetCreatedObject().ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture);

    public DateTime GetCreatedObject() => DateTime.FromBinary(Created);

    public FlowDocument GetDocument() => TextConverter.Parse(Reconstruct(), TextFormat.Xaml);

    public FlowDocument GetDocument(int backsteps = 0) => TextConverter.Parse(Reconstruct(backsteps), TextFormat.Xaml);

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

    public string GetLastChange() => GetLastChangeObject().ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture);

    public DateTime GetLastChangeObject() => DateTime.FromBinary(LastChange);

    public int GetNumRevisions() => Revisions.Count;

    private string GetPlaintext() => TextConverter.Convert(Reconstruct(), TextFormat.Xaml, TextFormat.Plaintext);

    public NoteRevision GetRevision(int index) => Revisions[Revisions.Count - 1 - index];

    public string GetRevisionTime(int index) => index < Revisions.Count
        ? DateTime.FromBinary(GetRevision(index).Created).ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture)
        : GetCreated();

    public Label GetRibbonHeader()
    {
        var tooltip = GetRibbonTooltip();
        var content = tooltip;

        if (content.Contains(Environment.NewLine))
            content = content[..content.IndexOf(Environment.NewLine, StringComparison.OrdinalIgnoreCase)];

        if (content.Length >= 13)
            content = $"{content[..10]}...";

        return new()
        {
            Content = content,
            Margin = new(0, -4, 0, 0),
            ToolTip = tooltip[..Math.Min(40, tooltip.Length)]
        };
    }

    private string GetRibbonTooltip() => RibbonTabContent switch
    {
        DisplayType.Change => $"{ShortChange} — {Preview}",
        DisplayType.Content => Preview,
        DisplayType.Creation => $"{GetCreated()} — {Preview}",
        DisplayType.Index => string.Format(CultureInfo.CurrentCulture, CacheNoteIndexLabel, Index + 1, Preview),
        _ => Preview
    };

    public bool IsAutosaveRevision(int index) => index < Revisions.Count && Revisions[index].IsAutosave;

    public void Lock()
    {
        Locked = true;
    }

    public int MatchTags(string text)
    {
        var format = text.Trim();
        if (!TagsDirty && format.Equals(LastQuery, StringComparison.Ordinal))
            return LastMatchCount;

        var matches = Lowercase().Matches(format.ToLowerInvariant());
        int outCount = 0;

        ExtractTags();

        foreach (Match match in matches)
        {
            foreach (Group group in match.Groups.Values)
            {
                if (Tags.Contains(group.Value.ToLowerInvariant()))
                    outCount++;
            }
        }

        LastQuery = format;
        return LastMatchCount = outCount;
    }

    /// <summary>
    /// <para>Reverts this record to a previous state by applying each of its stored revisions while leaving a requested count undone, specified by <paramref name="backsteps"/>.</para>
    /// </summary>
    /// <param name="backsteps">The number of revisions to undo, or 0 for the current state of the record.</param>
    /// <returns>The text of this record after undoing the requested number of revisions.</returns>
    public string Reconstruct(int backsteps = 0)
    {
        // We don't use an unsigned int here because the number of upstream conversions would increase exponentially if we did.
        backsteps = Math.Max(backsteps, 0);

        var latest = Initial ?? string.Empty;
        if (Revisions.Count == 0)
            return latest;

        for (int i = 0; i < Revisions.Count - Math.Min(backsteps, Revisions.Count); i++)
        {
            if (Revisions[i].StartIndex > -1 && Revisions[i].StartIndex < latest.Length)
                latest = latest[..Revisions[i].StartIndex];

            latest += Revisions[i].Substring;
        }

        return latest ?? string.Empty;
    }

    private void ReconstructRevisions(List<long> CreatedTags, List<string> Substrings)
    {
        for (int i = 0; i < Substrings.Count; i++)
        {
            var Created = CreatedTags[i];
            var RString = Substrings[i];
            var StartIndex = -1;
            var Substring = string.Empty;
            var ToCompare = i == 0 ? Initial : Substrings[i - 1];
            for (int j = 0; j < ToCompare?.Length; j++)
            {
                if (j >= RString.Length)
                    break;

                if (!RString[j].Equals(ToCompare[j]))
                    break;

                StartIndex = j + 1;
                if (StartIndex < RString.Length)
                    Substring = RString[StartIndex..];
            }

            Add(new NoteRevision()
            {
                Created = Created,
                StartIndex = StartIndex,
                Substring = Substring,
                Uuid = MakeUUID(UUIDType.Revision)
            });
        }
    }

    public void Serialize(Serializer? serializer)
    {
        if (serializer?.DatabaseFormat < 9)
            TargetPlaintext();

        if (serializer?.DatabaseFormat >= 5)
            serializer?.WriteShortString(UUID.ToString());
        serializer?.WriteLong(Created);
        serializer?.WriteInt32(Index);
        serializer?.WriteString(Initial);
        serializer?.WriteLong(LastChange);

        serializer?.WriteInt32(Revisions.Count);
        for (int i = 0; i < Revisions.Count; i++)
        {
            if (serializer?.DatabaseFormat >= 7)
                serializer?.WriteShortString(Revisions[i].Uuid.ToString());
            serializer?.WriteLong(Revisions[i].Created);
            if (serializer?.DatabaseFormat > 15)
            {
                byte flags = (byte)(
                    //0 << 7 |
                    //0 << 6 |
                    //0 << 5 |
                    //0 << 4 |
                    //0 << 3 |
                    //0 << 2 |
                    //0 << 1 |
                    (Revisions[i].IsAutosave ? 1 : 0) // << 0
                );

                serializer?.WriteByte(flags);
            }
            serializer?.WriteInt32(Revisions[i].StartIndex);
            serializer?.WriteString(Revisions[i].Substring);
        }
    }

    private void TargetPlaintext()
    {
        List<long> CreatedTags = [];
        var ParsedInitial = TextConverter.Convert(Initial ??= string.Empty, TextFormat.Xaml, TextFormat.Plaintext);
        var RCount = Revisions.Count;
        List<string> ReconstructedSubstrings = [];

        for (int i = RCount - 1; i > -1; i--)
        {
            var oldText = Reconstruct(i);
            CreatedTags.Add(Revisions[i].Created);
            ReconstructedSubstrings.Add(TextConverter.Convert(oldText, TextFormat.Xaml, TextFormat.Plaintext));
        }

        Revisions.Clear();
        Initial = ParsedInitial;

        ReconstructRevisions(CreatedTags, ReconstructedSubstrings);
    }

    private void TargetXaml()
    {
        List<long> CreatedTags = [];
        var ParsedInitial = TextConverter.Convert(Initial ?? string.Empty, TextFormat.Plaintext, TextFormat.Xaml);
        var RCount = Revisions.Count;
        List<string> ReconstructedSubstrings = [];

        for (int i = RCount - 1; i > -1; i--)
        {
            var oldText = Reconstruct(i);
            CreatedTags.Add(Revisions[i].Created);
            ReconstructedSubstrings.Add(TextConverter.Convert(oldText, TextFormat.Plaintext, TextFormat.Xaml));
        }

        Revisions.Clear();
        Initial = ParsedInitial;

        ReconstructRevisions(CreatedTags, ReconstructedSubstrings);
    }

    public override string ToString() => GetPlaintext();

    public string ToXaml() => Reconstruct();

    public void Unlock()
    {
        Locked = false;
    }

    [GeneratedRegex(@"(\p{Ll}+)")]
    private static partial Regex Lowercase();
}
