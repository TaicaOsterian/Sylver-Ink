using SylverInk.XAML;
using SylverInk.XAML.Objects;
using System.Globalization;
using static SylverInk.CommonUtils;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.Notes;

/// <summary>
/// Static functions assisting in general-purpose access to the roster of databases.
/// </summary>
public static class DatabaseUtils
{
    private static Database? _currentDatabase;

    public static Database CurrentDatabase { get => _currentDatabase ??= new(); set => _currentDatabase = value; }
    public static bool DatabaseChanged { get; set; }
    public static List<string> DatabaseFiles { get => [.. Databases.Select(db => db.DBFile)]; }
    public static List<Database> Databases { get; } = [];
    public static string DefaultDatabase { get; } = "New";
    public static string ShellDB { get; set; } = string.Empty;

    public static void AddDatabase(Database db)
    {
        static object PanelLabel(TabItem item) => ((Label)((StackPanel)item.Header).Children[0]).Content;

        if (Application.Current.MainWindow.FindName("DatabasesPanel") is not TabControl control)
            return;

        if (string.IsNullOrWhiteSpace(db.Name))
            db.Name = DefaultDatabase;

        var tabs = control.Items.Cast<TabItem>();

        if (tabs.Any(item => PanelLabel(item).Equals(db.Name)))
        {
            var index = 1;
            Match match = IndexDigits().Match(db.Name);
            if (match.Success)
                index = int.Parse(match.Groups[1].Value, NumberFormatInfo.InvariantInfo);
            while (tabs.Any(item => PanelLabel(item).Equals($"{db.Name} ({index})")))
                index++;
            db.Name = $"{db.Name} ({index})";
        }

        if (string.IsNullOrWhiteSpace(db.DBFile))
            db.DBFile = GetDatabasePath(db);

        var content = new DatabaseControl();
        var header = db.GetHeader();

        // As a courtesy to the user, if the active database's header is clicked, bring them to the Plus tab for that database.
        header.MouseDown += (_, _) =>
        {
            if (CurrentDatabase.Equals(db))
                content.Controller.SelectedItem = content.PlusTab;
        };

        TabItem item = new()
        {
            Content = content,
            Header = header,
            Margin = new(0, 2, 0, 0),
            Tag = db,
        };

        item.MouseRightButtonDown += (_, _) => control.SelectedItem = item;

        Databases.Add(db);
        db.Sort();

        control.Items.Add(item);
        control.SelectedItem = item;

        RecentNotesDirty = true;
        DeferUpdateRecentNotes();

        PathItem recentItem = new() { FullPath = db.DBFile };

        if (!CommonUtils.Settings.RecentDatabases.Contains(recentItem))
            return;

        CommonUtils.Settings.RecentDatabases.Remove(recentItem);
    }

    public static void CreateNewNote()
    {
        var firstRecord = CurrentDatabase.GetRecord(0);
        var lastRecord = CurrentDatabase.GetRecord(CurrentDatabase.RecordCount - 1);

        // If the database's first note is empty, open it.
        if (firstRecord is not null && string.IsNullOrEmpty(firstRecord.ToString()))
        {
            OpenQuery(firstRecord);
            return;
        }

        // Else, if the database's last note is empty, open it.
        if (lastRecord is not null && string.IsNullOrEmpty(lastRecord.ToString()))
        {
            OpenQuery(lastRecord);
            return;
        }

        // Else, and only then, make a new note entirely.
        if (CurrentDatabase.GetRecord(CurrentDatabase.CreateRecord(string.Empty)) is not NoteRecord newRecord)
            return;

        OpenQuery(newRecord);
    }

    public static SearchResult? OpenQuery(NoteRecord record, bool show = true)
    {
        foreach (SearchResult result in OpenQueries)
        {
            if (result.RequestOpen(record))
                return result;
        }

        RemoveRecordTab(record);

        SearchResult resultWindow = new();
        resultWindow.ViewModel.Record = record;

        if (!show)
            return resultWindow;

        resultWindow.Show();
        OpenQueries.Add(resultWindow);
        if (!record?.Locked is true)
            record?.DB?.Lock(record.Index, true);

        DeferUpdateRecentNotes();

        return resultWindow;
    }

    public static void RemoveDatabase(Database db)
    {
        if (Application.Current.MainWindow.FindName("DatabasesPanel") is not TabControl control)
            return;

        if (control.SelectedItem is not TabItem item)
            return;

        if (item.Tag is not Database tabDB)
            return;

        if (control.Items.Count < 2)
            AddDatabase(new());

        if (tabDB.Equals(db))
        {
            control.Items.RemoveAt(control.SelectedIndex);
            control.SelectedIndex = Math.Max(0, Math.Min(control.Items.Count - 1, control.SelectedIndex));
        }

        for (int i = OpenQueries.Count - 1; i > -1; i--)
        {
            if (OpenQueries[i].ViewModel.Record.DB?.Equals(db) is true)
                OpenQueries[i].Close();
        }

        for (int i = Databases.Count - 1; i > -1; i--)
        {
            if ((Databases[i].Name ?? string.Empty).Equals(db.Name, StringComparison.Ordinal))
                Databases.RemoveAt(i);
        }

        RecentNotesDirty = true;
        DeferUpdateRecentNotes();

        if (!Path.Exists(db.DBFile))
            return;

        var recentItem = new PathItem() { FullPath = db.DBFile };

        if (!CommonUtils.Settings.RecentDatabases.Contains(recentItem))
            CommonUtils.Settings.RecentDatabases.Insert(0, recentItem);
    }

    public static void RemoveRecordTab(NoteRecord? record)
    {
        for (int i = OpenTabs.Count - 1; i > -1; i--)
        {
            var item = OpenTabs[i];

            if (item.Content is not NoteTab tab)
                continue;

            if (!tab.ViewModel.Record.Equals(record))
                continue;

            OpenTabs.RemoveAt(i);
            tab.ViewModel.Deconstruct();
        }
    }

    public static async Task SaveDatabases()
    {
        foreach (Database db in Databases)
            await Task.Run(db.Save);
    }

    public static void SwitchDatabase(Database db)
    {
        if (Application.Current.MainWindow.FindName("DatabasesPanel") is not TabControl control)
            return;

        foreach (TabItem item in control.Items)
        {
            if (item.Tag is not Database tabDB)
                continue;

            if (!tabDB.Equals(db))
                continue;

            control.SelectedItem = item;
            CurrentDatabase = tabDB;
        }
    }

    public static void SwitchDatabase(string dbID)
    {
        var div = dbID.Split(':', 2);

        foreach (Database db in Databases)
        {
            var tag = div[0] switch
            {
                "~N" => db.Name,
                "~F" => Path.GetFullPath(db.DBFile),
                _ => string.Empty
            };

            if (div[1].Equals(tag, StringComparison.Ordinal))
                SwitchDatabase(db);
        }
    }
}
