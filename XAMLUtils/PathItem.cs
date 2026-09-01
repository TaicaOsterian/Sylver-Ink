using SylverInk.FileIO;

namespace SylverInk.XAMLUtils;

/// <summary>
/// Encapsulates a file path and provides properties for ease of display purposes.
/// </summary>
public class PathItem : IEquatable<PathItem>
{
    private const int MaxLength = 40;

    public string DisplayPath
    {
        get
        {
            string path = FullPath.Replace(FileUtils.Subfolders["Databases"], string.Empty);

            char sep = Path.DirectorySeparatorChar;
            if (path.StartsWith(sep))
                path = path[1..];

            List<string> components = [.. path.Split(sep)];
            if (components.Count > 1 && components[^1].Contains(components[0], StringComparison.Ordinal))
            {
                components.RemoveAt(0);
                path = string.Join(sep, components);
            }

            if (string.IsNullOrEmpty(path) || path.Length <= MaxLength)
                return path;

            if (components.Count < 3 && path.Length >= MaxLength)
                return string.Concat(path.AsSpan(0, MaxLength - 3), "...");

            string TruncatedPath = path;
            while (TruncatedPath.Length > MaxLength)
            {
                // The NUL byte is the only illegal path character shared across all major platforms, so it's the only one we can reasonably use for a placeholder.
                if (components.Contains("\0"))
                    components.RemoveAt(components.Count >= 5 ? 3 : 1);
                else
                    components[components.Count == 3 ? 1 : 2] = "\0";

                TruncatedPath = string.Join(sep, components);
            }

            return TruncatedPath.Replace("\0", "...");
        }
    }

    public string FullPath { get; set; } = string.Empty;

    public PathItem()
    {
    }

    bool IEquatable<PathItem>.Equals(PathItem? other)
    {
        if (other is null)
            return false;

        if (string.IsNullOrEmpty(FullPath) || string.IsNullOrEmpty(other.FullPath))
            return false;

        var fullPath1 = Path.GetFullPath(FullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath2 = Path.GetFullPath(other.FullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(fullPath1, fullPath2, comparison);
    }

    public override bool Equals(object? obj)
    {
        return ((IEquatable<PathItem>)this).Equals(obj as PathItem);
    }

    public override int GetHashCode()
    {
        return FullPath.GetHashCode();
    }

    public override string ToString()
    {
        return DisplayPath;
    }
}
