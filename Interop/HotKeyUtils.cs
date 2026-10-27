using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.Interop;

/// <summary>
/// Static functions serving global hotkey registration.
/// </summary>
public static class HotKeyUtils
{
    [DllImport("User32.dll")]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("User32.dll")]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    public static readonly DependencyProperty HotKeyCommandProperty =
        DependencyProperty.RegisterAttached(
            "HotKeyCommand",
            typeof(ICommand),
            typeof(HotKeyUtils),
            new PropertyMetadata(null, OnCommandChanged));
    private static WindowInteropHelper? hWndHelper;
    public const int NewNoteHotKeyID = 114455722;
    public const int PreviousNoteHotKeyID = 180604626;
    private static readonly Dictionary<int, bool> RegistrationStatus = new([
        new(NewNoteHotKeyID, false),
        new(PreviousNoteHotKeyID, false),
    ]);
    private static HwndSource? WindowSource;

    private static void Button_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button)
            return;

        button.IsChecked = false;
    }

    private static void Button_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var button = (ToggleButton)sender;
        Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
            return;

        if (key is Key.Escape)
        {
            button.IsChecked = false;
            return;
        }

        var command = GetHotKeyCommand(button);

        ModifierKeys modifiers = Keyboard.Modifiers;

        var result = new HotKey()
        {
            Key = key,
            KeyID = (int)button.Tag,
            Modifiers = modifiers,
        };

        if (command?.CanExecute(result) == true)
            command.Execute(result);

        button.IsChecked = false;
    }

    public static ICommand GetHotKeyCommand(DependencyObject obj) => (ICommand)obj.GetValue(HotKeyCommandProperty);

    private static nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case 0x0312: // WM_HOTKEY
                switch (wParam.ToInt32())
                {
                    case NewNoteHotKeyID:
                        OnNewNoteHotKey();
                        break;
                    case PreviousNoteHotKeyID:
                        OnPreviousNoteHotKey();
                        break;
                }
                break;
            default:
                return default;
        }

        handled = true;
        return default;
    }

    public static void Init()
    {
        hWndHelper = new WindowInteropHelper(Application.Current.MainWindow);
        WindowSource = HwndSource.FromHwnd(hWndHelper.Handle);
        WindowSource.AddHook(HwndHook);
        RegisterHotKeys();
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleButton button)
            return;

        button.Checked += (s, ae) =>
        {
            UnregisterHotKeys();
            button.Focus();
            Keyboard.Focus(button);
            button.LostFocus += Button_LostFocus;
            button.PreviewKeyDown += Button_PreviewKeyDown;
        };

        button.Unchecked += (s, ae) =>
        {
            button.PreviewKeyDown -= Button_PreviewKeyDown;
            button.LostFocus -= Button_LostFocus;
            RegisterHotKeys();
        };
    }

    private static void OnNewNoteHotKey() => CreateNewNote();

    private static void OnPreviousNoteHotKey() => CurrentDatabase.PopPreviousNote();

    private static void RegisterHotKeys()
    {
        RegisterHotKey(NewNoteHotKeyID, CommonUtils.Settings.NewNoteHotKey);
        RegisterHotKey(PreviousNoteHotKeyID, CommonUtils.Settings.PreviousNoteHotKey);
    }

    public static bool RegisterHotKey(int hotKeyID, HotKey gesture)
    {
        if (hWndHelper is null)
            return false;

        if (RegistrationStatus.TryGetValue(hotKeyID, out var register) && register)
            return true;

        if (!TestHotKey(gesture))
            return false;

        register = RegisterHotKey(hWndHelper.Handle, hotKeyID, (uint)gesture.Modifiers, (uint)KeyInterop.VirtualKeyFromKey(gesture.Key));
        RegistrationStatus[hotKeyID] = register;
        return register;
    }

    public static void Release()
    {
        WindowSource?.RemoveHook(HwndHook);
        WindowSource = null;
        UnregisterHotKeys();
    }

    public static bool ReRegisterHotKey(int hotKeyID, HotKey gesture)
    {
        if (!UnregisterHotKey(hotKeyID))
            return false;

        if (!RegisterHotKey(hotKeyID, gesture))
            return false;

        return true;
    }

    public static void SetHotKeyCommand(DependencyObject obj, ICommand value) => obj.SetValue(HotKeyCommandProperty, value);

    private static bool TestHotKey(HotKey gesture)
    {
        if (hWndHelper is null)
            return false;

        var result = RegisterHotKey(hWndHelper.Handle, 99999, (uint)gesture.Modifiers, (uint)KeyInterop.VirtualKeyFromKey(gesture.Key));
        if (result)
        {
            UnregisterHotKey(hWndHelper.Handle, 99999);
            return true;
        }

        return false;
    }

    private static void UnregisterHotKeys()
    {
        UnregisterHotKey(NewNoteHotKeyID);
        UnregisterHotKey(PreviousNoteHotKeyID);
    }

    public static bool UnregisterHotKey(int hotKeyID)
    {
        if (hWndHelper is null)
            return false;

        if (RegistrationStatus.TryGetValue(hotKeyID, out var unregister) && !unregister)
            return true;

        unregister = UnregisterHotKey(hWndHelper.Handle, hotKeyID);
        RegistrationStatus[hotKeyID] = !unregister;
        return unregister;
    }
}
