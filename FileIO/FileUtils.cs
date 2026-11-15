using Microsoft.Win32;
using System.Globalization;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.FileIO;

/// <summary>
/// Static functions serving specific needs in regards to file access.
/// </summary>
internal static class FileUtils
{
    internal enum Subfolder
    {
        Backups,
        Databases,
    }

    internal static string DocumentsFolder { get; private set; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sylver Ink");
    internal static int HighestSIDBFormat { get; } = 16;
    internal static char[] InvalidPathChars { get; } = ['/', '\\', ':', '*', '"', '?', '<', '>', '|'];
    internal static Dictionary<Subfolder, string> Subfolders { get; } = new([
        new(Subfolder.Backups, Path.Join(LocalAppDataFolder, Strings.Subfolder_Backups!)),
        new(Subfolder.Databases, Path.Join(DocumentsFolder, Strings.Subfolder_Databases!)),
        ]);
    internal static string LocalAppDataFolder { get; private set; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sylver Ink");
    internal static string LocalizationFile { get; } = Path.Join(DocumentsFolder, "loc.sis"); // A single source of truth for other file and folder targets.
    internal static string SessionFile { get; } = Path.Join(LocalAppDataFolder, "session.lock"); // On a soft reboot (subscribed in App.xaml.cs), we touch this file. This indicates to the next session that it should silently reload an autosave delta without prompting the user.
    internal static string SettingsFile { get; private set; } = Path.Join(DocumentsFolder, $"{Strings.Word_Settings!.ToLower(CultureInfo.CurrentCulture)}.sis");

    internal static string DialogFileSelect(bool outgoing = false, int filterIndex = 3, string? defaultName = null)
    {
        FileDialog dialog = outgoing ? new SaveFileDialog()
        {
            FileName = defaultName ?? DefaultDatabase,
            Filter = Strings.FileFilter_Database,
        } : new OpenFileDialog()
        {
            CheckFileExists = true,
            Filter = Strings.FileFilter_DatabaseAndText,
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
    internal static bool Erase(string filename)
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
            ShowTooltip(Strings.FailedDelete!);
            return false;
        }
    }

    internal static string GetBackupPath(Database db) => Path.Join(Subfolders[Subfolder.Backups], db.Name, db.Name);

    /// <summary>
    /// Get the default file path for a Sylver Ink database, based on its name and the working directory.
    /// </summary>
    internal static string GetDatabasePath(Database db)
    {
        db.FormatName();
        return Path.Join(Subfolders[Subfolder.Databases], db.Name, $"{db.Name}.sidb");
    }

    internal static string GetLockFile(string? dbFile = null) => Path.Join(Path.GetDirectoryName(dbFile ?? CurrentDatabase.DBFile) ?? ".", "_lock.sid_");

    internal static void LoadPaths(string? source = null)
    {
        source ??= LocalizationFile;

        if (!File.Exists(source))
            return;

        foreach (string setting in File.ReadAllLines(source))
        {
            var keyValue = setting.Trim().Split(':', 2);

            if (keyValue.Length < 2)
                continue;

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

    internal static void SavePaths(string? target = null)
    {
        target ??= LocalizationFile;

        var items = Subfolders.ToList().Select(item => $"{Enum.GetName(item.Key)}:{item.Value.Replace(DocumentsFolder, "?\\")}");
        items = items.Append($"Settings:{SettingsFile.Replace(DocumentsFolder, "?\\")}");

        File.WriteAllLines(LocalizationFile, items);
    }
}
