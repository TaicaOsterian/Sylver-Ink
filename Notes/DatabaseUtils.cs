using SylverInk.FileIO;
using SylverInk.XAML;
using SylverInk.XAML.Controls;
using System.Globalization;
using static SylverInk.FileIO.FileUtils;

namespace SylverInk.Notes;

/// <summary>
/// Static functions assisting in general-purpose access to the roster of databases.
/// </summary>
internal static class DatabaseUtils
{
    private static Database? _currentDatabase;

    internal static Database CurrentDatabase { get => _currentDatabase ??= new(); set => _currentDatabase = value; }
    internal static bool DatabaseChanged { get; set; }
    internal static List<string> DatabaseFiles { get => [.. Databases.Select(db => db.DBFile)]; }
    internal static List<Database> Databases { get; } = [];
    internal static string DefaultDatabase { get; } = $"{Strings.Word_New}";
    internal static List<TabItem> OpenTabs { get; } = [];
    internal static string ShellDB { get; set; } = string.Empty;

    internal static void AddDatabase() => AddDatabase(new(), true);

    internal static void AddDatabase(Database db, bool newDB = false)
    {
        var panel = GetDatabasesPanel()!;
        db.FormatName(newDB);

        if (string.IsNullOrWhiteSpace(db.DBFile))
            db.DBFile = GetDatabasePath(db);

        var content = new DatabaseControl();
        DatabaseControl.SetDatabase(content, db);

        TabItem item = new()
        {
            Content = content,
            Header = db.GetHeader(),
            Margin = new(0, 2, 0, 0),
        };

        item.MouseRightButtonDown += (_, _) => panel.SelectedItem = item;

        Databases.Add(db);
        db.Sort();

        panel.Items.Add(item);
        panel.SelectedItem = item;

        RefreshRecentNotes();

        PathItem recentItem = new() { FullPath = db.DBFile };

        if (!CommonUtils.Settings.RecentDatabases.Contains(recentItem))
            return;

        CommonUtils.Settings.RecentDatabases.Remove(recentItem);
    }

    internal static NoteTab AddRecordTab(NoteRecord record)
    {
        NoteTab tab = new();
        tab.ViewModel.Record = record;

        TabItem item = new()
        {
            Content = tab,
            Header = record.GetRibbonHeader(),
            Margin = new(0, 2, 0, 0)
        };

        var ChildPanel = Concurrent(GetCurrentTabControl)!;
        ChildPanel.SelectedIndex = ChildPanel.Items.Add(item);
        OpenTabs.Add(item);

        return tab;
    }

    internal static async Task AutosaveDatabases()
    {
        foreach (Database db in Databases)
            await db.Autosave();
    }

    internal static void CreateNewNote()
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

    /// <summary>
    /// Obtains a reference to the content of a <seealso cref="DatabaseControl"/>; that is, a <seealso cref="TabControl"/> containing the PlusTab and the open <seealso cref="NoteTab"/>s in an open database.
    /// </summary>
    internal static TabControl? GetCurrentTabControl() => (TabControl?)GetCurrentDatabaseControl()?.Content;

    internal static DatabaseControl? GetCurrentDatabaseControl()
    {
        if (GetDatabasesPanel() is not TabControl control)
            return null;

        var dbItem = (TabItem)control.SelectedItem;
        return (DatabaseControl)dbItem.Content;
    }

    internal static TabControl? GetDatabasesPanel() => (TabControl)Application.Current.MainWindow.FindName("DatabasesPanel");

    internal static int GetFirstAvailableIndex(Database db, bool newDB = false)
    {
        var name = db.Name ?? DefaultDatabase;
        var index = 0;
        Match match = IndexDigits().Match(name);
        if (match.Success)
        {
            index = int.Parse(match.Groups[1].Value, NumberFormatInfo.InvariantInfo);
            name = IndexDigits().Replace(name, string.Empty).TrimEnd();
            db.Name = name;
        }

        var success = false;

        do
        {
            var formattedName = index == 0 ? name : $"{name} ({index})";
            bool nameExists = false;

            foreach (var otherDB in Databases)
            {
                if (formattedName.Equals(otherDB.Name, StringComparison.OrdinalIgnoreCase))
                    nameExists = true;
            }

            if (nameExists)
            {
                index++;
                continue;
            }

            var path = Path.Join(Subfolders[Subfolder.Databases], formattedName);
            var dbFile = Path.Join(path, $"{formattedName}.sidb");
            var uuidFile = Path.Join(path, "uuid.dat");

            if (File.Exists(uuidFile)
                && Guid.TryParse(File.ReadAllText(uuidFile), out var uuid)
                && (newDB || uuid.Equals(db.UUID)))
            {
                index++;
                continue;
            }

            if (File.Exists(dbFile))
            {
                if (newDB)
                {
                    index++;
                    continue;
                }

                using Database tmpDB = new();
                try
                {
                    tmpDB.Load(dbFile);
                    if (!tmpDB.Equals(db))
                    {
                        index++;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    App.LogException(ex);
                    index++;
                    continue;
                }
            }

            success = true;
        } while (!success);

        return index;
    }

    internal static StickyNote? OpenQuery(NoteRecord record, string? scrollTo = null)
    {
        foreach (StickyNote result in OpenQueries)
        {
            if (!result.RequestOpen(record))
                continue;

            result.ViewModel.ScrollTo(scrollTo);
            return result;
        }

        RemoveRecordTab(record);

        StickyNote resultWindow = new();
        resultWindow.ViewModel.Record = record;
        resultWindow.ViewModel.ScrollTo(scrollTo);

        resultWindow.Show();
        if (!record?.Locked is true)
            record?.DB?.Lock(record.UUID, true);

        RefreshRecentNotes();

        return resultWindow;
    }

    internal static void RemoveDatabase(Database db)
    {
        if (Application.Current.MainWindow.FindName("DatabasesPanel") is not TabControl control)
            return;

        if (control.SelectedItem is not TabItem item)
            return;

        if (item.Content is not DatabaseControl dbControl)
            return;

        if (DatabaseControl.GetDatabase(dbControl) is not Database tabDB)
            return;

        if (control.Items.Count < 2)
            AddDatabase();

        if (tabDB.Equals(db))
        {
            control.Items.RemoveAt(control.SelectedIndex);
            control.SelectedIndex = Math.Max(0, Math.Min(control.Items.Count - 1, control.SelectedIndex));
        }

        for (int i = OpenQueries.Count - 1; i > -1; i--)
        {
            var note = OpenQueries[i];
            if (note.ViewModel.Record.DB?.Equals(db) is true)
                note.RequestClose();
        }

        for (int i = Databases.Count - 1; i > -1; i--)
        {
            if ((Databases[i].Name ?? string.Empty).Equals(db.Name, StringComparison.Ordinal))
                Databases.RemoveAt(i);
        }

        RefreshRecentNotes();

        if (!Path.Exists(db.DBFile))
            return;

        var recentItem = new PathItem() { FullPath = db.DBFile };

        if (!CommonUtils.Settings.RecentDatabases.Contains(recentItem))
            CommonUtils.Settings.RecentDatabases.Insert(0, recentItem);
    }

    internal static void RemoveRecordTab(NoteRecord? record)
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

        var panel = Concurrent(GetCurrentTabControl)!;
        foreach (TabItem item in panel.Items)
        {
            if (item.Content is not NoteTab tab)
                continue;

            if (tab.ViewModel.Record.Equals(record))
            {
                panel.Items.Remove(item);
                break;
            }
        }

        RefreshRecentNotes();
    }

    internal static async Task SaveDatabases()
    {
        foreach (Database db in Databases)
            await Task.Run(db.Save);
    }

    internal static void SwitchDatabase(Database db)
    {
        if (Application.Current.MainWindow.FindName("DatabasesPanel") is not TabControl control)
            return;

        foreach (TabItem item in control.Items)
        {
            if (item.Content is not DatabaseControl dbControl)
                continue;

            if (DatabaseControl.GetDatabase(dbControl) is not Database tabDB)
                continue;

            if (!tabDB.Equals(db))
                continue;

            control.SelectedItem = item;
            CurrentDatabase = tabDB;
        }
    }

    internal static void SwitchDatabase(string dbID)
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

    internal static void UpdateRibbonTabs()
    {
        foreach (var item in OpenTabs)
        {
            if (item.Content is not NoteTab tab)
                continue;

            item.Header = tab.ViewModel.Record.GetRibbonHeader();
        }
    }
}
