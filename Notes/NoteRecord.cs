using SylverInk.FileIO;
using SylverInk.Interop;
using System.Globalization;
using System.Threading;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.Text.FlowDocumentUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.Notes;

internal struct NoteRevision(long created = -1, bool isAutosave = false, int length = -1, uint startIndex = 0, string? substring = null, Guid? uuid = null)
{
    internal long Created { get; set; } = created;
    internal bool IsAutosave { get; set; } = isAutosave;
    internal int Length { get; set; } = length;
    internal uint StartIndex { get; set; } = startIndex;
    internal string? Substring { get; set; } = substring;
    internal Guid Uuid { get; set; } = uuid ?? MakeUUID(UUIDType.Revision);
}

public sealed partial class NoteRecord
{
    private string? _cachedPreview;
    private long Created = -1;
    private int _index = -1;
    private string? Initial = string.Empty;
    private long LastChange = -1;
    private DateTime LastChangeObject = DateTime.UtcNow;
    private string LastQuery = string.Empty;
    private bool LatestDirty = true;
    private readonly LinkedStack<NoteRevision> Revisions = [];
    private readonly List<string> Tags = [];
    private bool TagsDirty = true;
    private Guid _uuid;

    internal Database? DB { get; set; }
    internal int Index { get => _index; set => _index = value; }
    internal int LastMatchCount { get; set; }
    internal string Latest { get; private set; }
    internal bool Locked { get; set; }
    internal Guid UUID { get => _uuid; set => _uuid = value; }

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
            if (_cachedPreview is not null && !LatestDirty)
                return _cachedPreview;

            // Never parse the Base64 payload just for a preview string.
            string xaml = ImageRunRegex().Replace(Reconstruct(), "<Paragraph><Run>(image)</Run></Paragraph>");

            string p = FlowDocumentPreview(TextConverter.Parse(xaml, TextFormat.Xaml))
                .ReplaceLineEndings()
                .Replace(Environment.NewLine, " ")
                .Replace('\t', ' ');

            while (p.Contains("  "))
                p = p.Replace("  ", " ");

            return _cachedPreview = string.IsNullOrEmpty(p) ? Strings.EmptyNote! : p;
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
                return diff.Days == 1 ? Strings.DayPassed! : string.Format(CultureInfo.CurrentCulture, CacheDaysPassed, diff.Days);

            return dtObject.ToLocalTime().ToShortDateString();
        }
    }

    internal NoteRecord(Database? DB = null)
    {
        Created = DateTime.UnixEpoch.ToBinary();
        this.DB = DB;
        Initial = string.Empty;
        LastChange = Created;
        LastChangeObject = DateTime.FromBinary(LastChange);
        Latest = Initial;
        _uuid = MakeUUID(UUIDType.Record);
    }

    internal NoteRecord(int Index, string Initial, Database? DB = null, long Created = -1, Guid? UUID = null)
    {
        this.Created = Created == -1 ? DateTime.UtcNow.ToBinary() : Created;
        this.DB = DB;
        _index = Index;
        this.Initial = Initial;
        LastChange = this.Created;
        LastChangeObject = DateTime.FromBinary(LastChange);
        Latest = Initial;
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

        Revisions.Push(revision);
        LatestDirty = true;
        TagsDirty = true;
    }

    internal async Task BeginAutosave(FlowDocument document, CancellationTokenSource cts)
    {
        if (cts.IsCancellationRequested)
            return;

        DelayVisualUpdates = true;
        EraseAutosave();
        CreateRevision(TextConverter.Save(document, TextFormat.Xaml), Autosave: true);
        DB?.Autosave(cts);
        DelayVisualUpdates = false;
        RefreshRecentNotes();
    }

    internal void CreateRevision(string NewVersion, bool Autosave = false)
    {
        string Current = ToXaml();

        if (NewVersion.Equals(Current, StringComparison.Ordinal))
            return;

        var (StartIndex, ReplacementLength, Substring) = StringDiff(Current, NewVersion);

        Add(new()
        {
            Created = DateTime.UtcNow.ToBinary(),
            IsAutosave = Autosave,
            Length = ReplacementLength,
            StartIndex = StartIndex,
            Substring = Substring,
            Uuid = MakeUUID(UUIDType.Revision)
        });

        if (DB is null)
            return;

        DB.Changed = true;
    }

    internal void Delete()
    {
        DB = null;
        Index = -1;
        Initial = string.Empty;
        LastChange = DateTime.UtcNow.ToBinary();
        LatestDirty = true;
        Revisions.Clear();
        TagsDirty = true;

        RefreshRecentNotes();
    }

    internal void DeleteRevision()
    {
        Revisions.Pop();

        LastChange = GetNumRevisions() == 0 ? Created : Revisions.Peek().Created;
        LastChangeObject = DateTime.FromBinary(LastChange);
        LatestDirty = true;

        RefreshRecentNotes();
    }

    internal NoteRecord Deserialize(Serializer? serializer)
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

                _revision.IsAutosave = (flags & 1) == 1;
            }

            _revision.StartIndex = serializer?.ReadUInt32() ?? 0;

            if (serializer?.DatabaseFormat >= 17)
                _revision.Length = serializer.ReadInt32() ?? -1;

            _revision.Substring = serializer?.ReadString();

            Add(_revision);
        }

        // SIDB v.9 introduced XAML rich text formatting. Its absence in earlier versions must be accounted for.
        if (serializer?.DatabaseFormat < 9)
            TargetXaml();

        return this;
    }

    internal void EndAutosave(Serializer? serializer)
    {
        if (serializer?.DatabaseFormat < 9)
            TargetPlaintext();

        if (serializer?.DatabaseFormat >= 5)
            serializer?.WriteShortString(UUID.ToString());

        serializer?.WriteLong(Created);
        serializer?.WriteInt32(Index);
        serializer?.WriteString(Initial);
        serializer?.WriteLong(LastChange);

        int RevisionCount = 0;

        for (int i = 0; i < Revisions.Count(); i++)
        {
            var revision = Revisions.Seek(i);

            if (!revision.IsAutosave)
                continue;

            RevisionCount++;
        }

        serializer?.WriteInt32(RevisionCount);
        for (int i = 0; i < Revisions.Count(); i++)
        {
            var revision = Revisions.Seek(i);

            if (!revision.IsAutosave)
                continue;

            if (serializer?.DatabaseFormat >= 7)
                serializer?.WriteShortString(revision.Uuid.ToString());

            serializer?.WriteLong(revision.Created);
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
                    1 // << 0
                );

                serializer?.WriteByte(flags);
            }

            serializer?.WriteUInt32(revision.StartIndex);

            if (serializer?.DatabaseFormat >= 17)
                serializer?.WriteInt32(revision.Length);

            serializer?.WriteString(revision.Substring);
        }
    }

    public override bool Equals(object? obj)
    {
        if (!GetType().Equals(obj?.GetType()))
            return false;

        var recordObj = (NoteRecord?)obj;
        return base.Equals(obj)
            || (UUID.Equals(recordObj?.UUID) is true)
            || (Created.Equals(recordObj?.Created)
                && Index.Equals(recordObj?.Index)
                && Initial?.Equals(recordObj?.Initial, StringComparison.Ordinal) is true
                && LastChange.Equals(recordObj?.LastChange));
    }

    internal void EraseAutosave()
    {
        while (IsAutosaveRevision())
            DeleteRevision();
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

    internal string GetCreated() => GetCreatedObject().ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture);

    internal DateTime GetCreatedObject() => DateTime.FromBinary(Created);

    internal FlowDocument GetDocument() => TextConverter.Parse(Reconstruct(), TextFormat.Xaml);

    internal FlowDocument GetDocument(int backsteps = 0) => TextConverter.Parse(Reconstruct(backsteps), TextFormat.Xaml);

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

    internal string GetLastChange() => GetLastChangeObject().ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture);

    internal DateTime GetLastChangeObject() => DateTime.FromBinary(LastChange);

    internal int GetNumRevisions() => Revisions.Count();

    private string GetPlaintext() => TextConverter.Convert(Reconstruct(), TextFormat.Xaml, TextFormat.Plaintext);

    internal NoteRevision GetRevision() => Revisions.Peek();

    internal NoteRevision GetRevision(int index) => Revisions.Peek(index);

    internal string GetRevisionTime() => DateTime.FromBinary(GetRevision().Created).ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture);

    internal string GetRevisionTime(int index) => index < Revisions.Count()
        ? DateTime.FromBinary(GetRevision(index).Created).ToLocalTime().ToString(DateFormat, CultureInfo.CurrentCulture)
        : GetCreated();

    internal Label GetRibbonHeader()
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

    private bool IsAutosaveRevision() => Revisions.Peek().IsAutosave;

    internal bool IsAutosaveRevision(int index) => Revisions.Peek(index).IsAutosave;

    internal void Lock() => Locked = true;

    internal int MatchTags(string text)
    {
        var format = text.Trim();
        if (!TagsDirty && format.Equals(LastQuery, StringComparison.OrdinalIgnoreCase))
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
    /// Erase this record's revision history, while taking its current state as the new initial state.
    /// </summary>
    internal void PurgeRevisions()
    {
        var text = Reconstruct();

        Revisions.Clear();
        Created = LastChange;
        Initial = text;
        Latest = text;
        LatestDirty = false;
        TagsDirty = true;
    }

    /// <summary>
    /// <para>Reverts this record to a previous state by applying each of its stored revisions while leaving a requested count undone, specified by <paramref name="backsteps"/>.</para>
    /// </summary>
    /// <param name="backsteps">The number of revisions to undo, or 0 for the current state of the record.</param>
    /// <returns>The text of this record after undoing the requested number of revisions.</returns>
    internal string Reconstruct(int backsteps = 0)
    {
        if (backsteps == 0 && !LatestDirty)
            return Latest;

        // We don't use an unsigned int here because the number of upstream conversions would increase exponentially if we did.
        backsteps = Math.Max(backsteps, 0);

        var final = Initial ?? string.Empty;
        if (!Revisions.Any())
            return final;

        for (int i = 0; i < Revisions.Count() - Math.Min(backsteps, Revisions.Count()); i++)
        {
            var revision = Revisions.Seek(i);
            var (start, len, sub) = (revision.StartIndex, revision.Length, revision.Substring ?? string.Empty);
            int len0 = (int)Math.Min(start, (uint)final.Length);
            int tailStart = (int)Math.Min(len0 + (uint)Math.Max(len, 0), (uint)final.Length);
            final = final[..len0] + sub + final[tailStart..];
        }

        final ??= string.Empty;

        if (backsteps == 0)
        {
            Latest = final;
            LatestDirty = false;
        }

        return final;
    }

    private void ReconstructRevisions(List<long> CreatedTags, List<string> Substrings)
    {
        for (int i = 0; i < Substrings.Count; i++)
        {
            var Created = CreatedTags[i];
            var RString = Substrings[i];
            var ToCompare = i == 0 ? Initial ?? string.Empty : Substrings[i - 1];

            var (StartIndex, ReplacementLength, Substring) = StringDiff(RString, ToCompare);

            Add(new NoteRevision()
            {
                Created = Created,
                Length = ReplacementLength,
                StartIndex = StartIndex,
                Substring = Substring,
                Uuid = MakeUUID(UUIDType.Revision)
            });
        }
    }

    internal NoteRecord RestoreFromAutosave(Serializer? serializer)
    {
        // The serializer has already read this record's UUID during the NoteController's workflow.
        // That is how the NoteController knew to call RestoreFromAutosave on this particular record.

        Created = serializer?.ReadLong() ?? DateTime.UtcNow.ToBinary();
        Index = serializer?.ReadInt32() ?? -1;
        Initial = serializer?.ReadString() ?? string.Empty;
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

                _revision.IsAutosave = (flags & 1) == 1;
            }

            _revision.StartIndex = serializer?.ReadUInt32() ?? 0;
            if (serializer?.DatabaseFormat >= 17)
                _revision.Length = serializer?.ReadInt32() ?? -1;
            _revision.Substring = serializer?.ReadString();

            Add(_revision);
        }

        // SIDB v.9 introduced XAML rich text formatting. Its absence in earlier versions must be accounted for.
        if (serializer?.DatabaseFormat < 9)
            TargetXaml();

        return this;
    }

    internal void Serialize(Serializer? serializer)
    {
        if (serializer?.DatabaseFormat <= 8)
            TargetPlaintext();

        if (serializer?.DatabaseFormat >= 5)
            serializer?.WriteShortString(UUID.ToString());

        serializer?.WriteLong(Created);
        serializer?.WriteInt32(Index);
        serializer?.WriteString(Initial);
        serializer?.WriteLong(LastChange);

        serializer?.WriteInt32(Revisions.Count());
        for (int i = 0; i < Revisions.Count(); i++)
        {
            var revision = Revisions.Seek(i);

            if (serializer?.DatabaseFormat >= 7)
                serializer?.WriteShortString(revision.Uuid.ToString());

            serializer?.WriteLong(revision.Created);

            if (serializer?.DatabaseFormat >= 16)
            {
                byte flags = (byte)(
                    //0 << 7 |
                    //0 << 6 |
                    //0 << 5 |
                    //0 << 4 |
                    //0 << 3 |
                    //0 << 2 |
                    //0 << 1 |
                    (revision.IsAutosave ? 1 : 0) // << 0
                );

                serializer?.WriteByte(flags);
            }

            serializer?.WriteUInt32(revision.StartIndex);

            if (serializer?.DatabaseFormat >= 17)
                serializer?.WriteInt32(revision.Length);

            serializer?.WriteString(revision.Substring);
        }
    }

    private static (uint, int, string) StringDiff(string oldVersion, string newVersion)
    {
        var limit = Math.Min(oldVersion.Length, newVersion.Length);
        uint StartIndex = 0;
        int ReplacementLength = -1;
        string Substring = string.Empty;

        for (int i = 0; i < limit; i++)
        {
            if (!oldVersion[i].Equals(newVersion[i]))
                break;

            StartIndex = (uint)i + 1U;
        }

        if (StartIndex == 0)
            return (StartIndex, ReplacementLength, newVersion);

        int i1 = oldVersion.Length - 1;
        int i2 = newVersion.Length - 1;

        while (i1 > StartIndex && oldVersion[i1].Equals(newVersion[i2]))
        {
            i1--;
            i2--;
        }

        if (i1 > StartIndex)
            ReplacementLength = (int)(i1 - StartIndex + 1);
        else if (i1 == StartIndex)
            ReplacementLength = 0;

        if (ReplacementLength == -1)
            Substring = newVersion[(int)StartIndex..];
        else if (i1 != i2)
            Substring = newVersion[(int)StartIndex..(i2 + 1)];

        return (StartIndex, ReplacementLength, Substring);
    }

    private void TargetPlaintext()
    {
        List<long> CreatedTags = [];
        var ParsedInitial = TextConverter.Convert(Initial ?? string.Empty, TextFormat.Xaml, TextFormat.Plaintext);
        var RCount = Revisions.Count();
        List<string> ReconstructedSubstrings = [];

        for (int i = 0; i < RCount; i++)
        {
            var revision = Revisions.Seek(i);
            var oldText = Reconstruct(i);
            CreatedTags.Add(revision.Created);
            ReconstructedSubstrings.Add(TextConverter.Convert(oldText, TextFormat.Xaml, TextFormat.Plaintext));
        }

        Revisions.Clear();
        Initial = ParsedInitial;
        LatestDirty = true;

        ReconstructRevisions(CreatedTags, ReconstructedSubstrings);
    }

    private void TargetXaml()
    {
        List<long> CreatedTags = [];
        var ParsedInitial = TextConverter.Convert(Initial ?? string.Empty, TextFormat.Plaintext, TextFormat.Xaml);
        var RCount = Revisions.Count();
        List<string> ReconstructedSubstrings = [];

        for (int i = 0; i < RCount; i++)
        {
            var revision = Revisions.Seek(i);
            var oldText = Reconstruct(i);
            CreatedTags.Add(revision.Created);
            ReconstructedSubstrings.Add(TextConverter.Convert(oldText, TextFormat.Plaintext, TextFormat.Xaml));
        }

        Revisions.Clear();
        Initial = ParsedInitial;
        LatestDirty = true;

        ReconstructRevisions(CreatedTags, ReconstructedSubstrings);
    }

    public override string ToString() => GetPlaintext();

    internal string ToXaml() => Reconstruct();

    internal void Unlock() => Locked = false;

    [GeneratedRegex(@"<Paragraph><Run Tag=""base64"">[^<]*</Run></Paragraph>|<Paragraph Tag=""base64"">[\s\S]*?</Paragraph>", RegexOptions.Singleline)]
    private static partial Regex ImageRunRegex();

    [GeneratedRegex(@"(\p{Ll}+)")]
    private static partial Regex Lowercase();
}
