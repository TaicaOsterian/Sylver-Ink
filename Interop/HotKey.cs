using System.Runtime.InteropServices;

namespace SylverInk.Interop;

public sealed class HotKey
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private Key _key;
    private int _keyID;
    private ModifierKeys _modifiers;

    private const uint MAPVK_VK_TO_CHAR = 2;

    internal Key Key
    {
        get => _key;
        set => _key = value;
    }

    internal int KeyID
    {
        get => _keyID;
        set => _keyID = value;
    }

    internal ModifierKeys Modifiers
    {
        get => _modifiers;
        set => _modifiers = value;
    }

    internal bool CombinationEquals(HotKey obj) => obj.Key.Equals(this.Key) && obj.Modifiers.Equals(this.Modifiers);

    internal static HotKey FromString(string format)
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

    internal static string GetLocalizedKeyName(Key key)
    {
        switch (key)
        {
            case Key.Tab:
                return Strings.Key_Tab!;
            case Key.Enter:
                return Strings.Key_Enter!;
            case Key.Space:
                return Strings.Key_Space!;
            case Key.Back:
                return Strings.Key_Backspace!;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        uint character = MapVirtualKey((uint)virtualKey, MAPVK_VK_TO_CHAR);

        if (character != 0)
            return ((char)character).ToString();

        return key.ToString();
    }

    public override string ToString() => ToString(true);

    internal string ToString(bool localize)
    {
        var keyString = localize ? GetLocalizedKeyName(this.Key) : this.Key.ToString();
        var modString = string.Empty;

        if ((Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
            modString += localize ? $"{Strings.Key_Win}+" : "Win+";

        if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            modString += localize ? $"{Strings.Key_Ctrl}+" : "Ctrl+";

        if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            modString += localize ? $"{Strings.Key_Alt}+" : "Alt+";

        if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            modString += localize ? $"{Strings.Key_Shift}+" : "Shift+";

        return $"{modString}{keyString}";
    }
}
