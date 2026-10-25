namespace SylverInk.Interop;

public class HotKey
{
    private Key _key;
    private int _keyID;
    private ModifierKeys _modifiers;

    public Key Key
    {
        get => _key;
        set => _key = value;
    }

    public int KeyID
    {
        get => _keyID;
        set => _keyID = value;
    }

    public ModifierKeys Modifiers
    {
        get => _modifiers;
        set => _modifiers = value;
    }

    public bool CombinationEquals(HotKey obj)
    {
        return obj.Key.Equals(this.Key) && obj.Modifiers.Equals(this.Modifiers);
    }

    public static HotKey FromString(string format)
    {
        HotKey result = new();

        var split = format.Split('+');
        foreach (string val in split)
        {
            switch (val)
            {
                case "Win":
                    result.Modifiers |= ModifierKeys.Windows;
                    break;
                case "Ctrl":
                    result.Modifiers |= ModifierKeys.Control;
                    break;
                case "Alt":
                    result.Modifiers |= ModifierKeys.Alt;
                    break;
                case "Shift":
                    result.Modifiers |= ModifierKeys.Shift;
                    break;
                default:
                    if (Enum.TryParse(typeof(Key), val, true, out var key))
                        result.Key = (Key)key;
                    break;
            }
        }

        return result;
    }

    public override string ToString()
    {
        var modString = string.Empty;

        if ((Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
            modString += "Win+";

        if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            modString += "Ctrl+";

        if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            modString += "Alt+";

        if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            modString += "Shift+";

        return $"{modString}{Key}";
    }
}
