using Microsoft.Win32;
using System.Globalization;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.FileIO;

/// <summary>
/// Static functions serving specific needs in regards to file access.
/// </summary>
public static class FileUtils
{
    public static string DocumentsFolder { get; } = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sylver Ink");
    public static string SettingsFile { get; } = Path.Join(DocumentsFolder, $"{Resources.Word_Settings.ToLower(CultureInfo.CurrentCulture)}.sis");
    public static int HighestSIDBFormat { get; } = 14;
    public static char[] InvalidPathChars { get; } = ['/', '\\', ':', '*', '"', '?', '<', '>', '|'];
    public static Dictionary<string, string> Subfolders { get; } = new([
        new(Resources.Subfolder_Databases, Path.Join(DocumentsFolder, Resources.Subfolder_Databases))
        ]);

    public static string DialogFileSelect(bool outgoing = false, int filterIndex = 3, string? defaultName = null)
    {
        FileDialog dialog = outgoing ? new SaveFileDialog()
        {
            FileName = defaultName ?? DefaultDatabase,
            Filter = Resources.DatabaseFileFilter,
        } : new OpenFileDialog()
        {
            CheckFileExists = true,
            Filter = Resources.DatabaseAndTextFileFilter,
            InitialDirectory = Subfolders[Resources.Subfolder_Databases],
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
        catch
        {
            return false;
        }
    }

    public static string GetBackupPath(Database db) => Path.Join(Subfolders[Resources.Subfolder_Databases], db.Name, db.Name);

    /// <summary>
    /// Get the default file path for a Sylver Ink database, based on its name and the working directory.
    /// </summary>
    public static string GetDatabasePath(Database db)
    {
        var index = 0;
        Match match;
        if ((match = IndexDigits().Match(db.Name ?? string.Empty)).Success)
            index = int.Parse(match.Groups[1].Value, NumberFormatInfo.InvariantInfo);

        var path = Path.Join(Subfolders[Resources.Subfolder_Databases], db.Name);
        var dbFile = Path.Join(path, $"{db.Name}.sidb");
        var uuidFile = Path.Join(path, "uuid.dat");

        while (File.Exists(dbFile))
        {
            if (File.Exists(uuidFile) && File.ReadAllText(uuidFile).Equals(db.UUID, StringComparison.Ordinal))
                return dbFile;

            Database tmpDB = new();
            try
            {
                tmpDB.Load(dbFile);
                if (tmpDB.UUID?.Equals(db.UUID, StringComparison.Ordinal) is true)
                    return dbFile;
                if (tmpDB.Format < 7) // Database object UUID was added in SIDB v7
                    return dbFile;
            }
            catch
            {
                tmpDB.Dispose();
                return string.Empty;
            }

            index++;
            db.Name = $"{db.Name} ({index})";
            dbFile = Path.Join(path, $"{db.Name}.sidb");
            uuidFile = Path.Join(path, "uuid.dat");
        }

        return dbFile;
    }

    public static string GetLockFile(string? dbFile = null) => Path.Join(Path.GetDirectoryName(dbFile ?? CurrentDatabase.DBFile) ?? ".", "_lock.sidb");
}
