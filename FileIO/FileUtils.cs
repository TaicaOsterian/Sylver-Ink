using Microsoft.Win32;
using System.Globalization;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.FileIO;

/// <summary>
/// Static functions serving specific needs in regards to file access.
/// </summary>
public static class FileUtils
{
    public enum Subfolder
    {
        Backups,
        Databases,
    }

    public static string DocumentsFolder { get; private set; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sylver Ink");
    public static int HighestSIDBFormat { get; } = 16;
    public static char[] InvalidPathChars { get; } = ['/', '\\', ':', '*', '"', '?', '<', '>', '|'];
    public static Dictionary<Subfolder, string> Subfolders { get; } = new([
        new(Subfolder.Backups, Path.Join(LocalAppDataFolder, Strings.Subfolder_Backups)),
        new(Subfolder.Databases, Path.Join(DocumentsFolder, Strings.Subfolder_Databases)),
        ]);
    public static string LocalAppDataFolder { get; private set; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sylver Ink");
    public static string LocalizationFile { get; } = Path.Join(DocumentsFolder, "loc.sis"); // A single source of truth for other file and folder targets.
    public static string SessionFile { get; } = Path.Join(LocalAppDataFolder, "session.lock"); // On a soft reboot (subscribed in App.xaml.cs), we touch this file. This indicates to the next session that it should silently reload a set of autosaves without prompting the user.
    public static string SettingsFile { get; private set; } = Path.Join(DocumentsFolder, $"{Strings.Word_Settings.ToLower(CultureInfo.CurrentCulture)}.sis");

    public static string DialogFileSelect(bool outgoing = false, int filterIndex = 3, string? defaultName = null)
    {
        FileDialog dialog = outgoing ? new SaveFileDialog()
        {
            FileName = defaultName ?? DefaultDatabase,
            Filter = Strings.DatabaseFileFilter,
        } : new OpenFileDialog()
        {
            CheckFileExists = true,
            Filter = Strings.DatabaseAndTextFileFilter,
            InitialDirectory = Subfolders[Subfolder.Databases],
        };

        dialog.FilterIndex = filterIndex;
        dialog.ValidateNames = true;

        return dialog.ShowDialog() is true ? dialog.FileName : string.Empty;
    }

    /// <summary>
    /// Deletes a file if it exists.
    /// </summary>
    /// <returns><see langword="true"/> if the file existed and was deleted; else, <see langword="false"/>.</returns>
    public static bool Erase(string filename)
    {
        try
        {
            if (!File.Exists(filename))
                return false;

            File.Delete(filename);
            return true;
        }
        catch (Exception e)
        {
            App.LogException(e);
            ShowTooltip(Strings.FailedDelete);
            return false;
        }
    }

    public static string GetBackupPath(Database db) => Path.Join(Subfolders[Subfolder.Backups], db.Name, db.Name);

    /// <summary>
    /// Get the default file path for a Sylver Ink database, based on its name and the working directory.
    /// </summary>
    public static string GetDatabasePath(Database db)
    {
        var index = 0;
        Match match;
        if ((match = IndexDigits().Match(db.Name ?? string.Empty)).Success)
            index = int.Parse(match.Groups[1].Value, NumberFormatInfo.InvariantInfo);

        var path = Path.Join(Subfolders[Subfolder.Databases], db.Name);
        var dbFile = Path.Join(path, $"{db.Name}.sidb");
        var uuidFile = Path.Join(path, "uuid.dat");

        while (File.Exists(dbFile))
        {
            if (File.Exists(uuidFile))
            {
                string uuidData = File.ReadAllText(uuidFile);
                if (Guid.TryParse(uuidData, out var uuid) && uuid.Equals(db.UUID))
                    return dbFile;
            }

            using Database tmpDB = new();
            try
            {
                tmpDB.Load(dbFile);
                if (tmpDB.UUID.Equals(db.UUID))
                    return dbFile;
                if (tmpDB.Format < 7) // Database object UUID was added in SIDB v7
                    return dbFile;
            }
            catch (Exception e)
            {
                App.LogException(e);
                return string.Empty;
            }

            index++;
            db.Name = $"{db.Name} ({index})";
            dbFile = Path.Join(path, $"{db.Name}.sidb");
            uuidFile = Path.Join(path, "uuid.dat");
        }

        return dbFile;
    }

    public static string GetLockFile(string? dbFile = null) => Path.Join(Path.GetDirectoryName(dbFile ?? CurrentDatabase.DBFile) ?? ".", "_lock.sid_");

    public static void LoadPaths(string? source = null)
    {
        source ??= LocalizationFile;

        if (!File.Exists(source))
            return;

        foreach (string setting in File.ReadAllLines(source))
        {
            var keyValue = setting.Trim().Split(':', 2);

            if (string.IsNullOrWhiteSpace(keyValue[0]))
                continue;

            if (string.IsNullOrWhiteSpace(keyValue[1]))
                continue;

            switch (keyValue[0])
            {
                case "Settings":
                    SettingsFile = keyValue[1].Replace("?\\", DocumentsFolder);
                    break;
                default:
                    if (!Enum.TryParse<Subfolder>(keyValue[0], true, out var folder))
                        break;

                    if (!Subfolders.ContainsKey(folder))
                        break;

                    Subfolders[folder] = keyValue[1].Replace("?\\", DocumentsFolder);
                    break;
            }
        }

        return;
    }

    public static void SavePaths(string? target = null)
    {
        target ??= LocalizationFile;

        var items = Subfolders.ToList().Select(item => $"{Enum.GetName(item.Key)}:{item.Value.Replace(DocumentsFolder, "?\\")}");
        items = items.Append($"Settings:{SettingsFile.Replace(DocumentsFolder, "?\\")}");

        File.WriteAllLines(LocalizationFile, items);
    }
}
