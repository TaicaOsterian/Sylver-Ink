using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using static SylverInk.CommonUtils;
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

	private static WindowInteropHelper? hWndHelper;
	private const int NewNoteHotKeyID = 5911;
	private const int PreviousNoteHotKeyID = 37193;
	private static HwndSource? WindowSource;

	private static nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		switch (msg)
		{
			case 0x0312: // WM_HOTKEY
				switch (wParam.ToInt32())
				{
					case NewNoteHotKeyID:
						OnNewNoteHotkey();
						break;
					case PreviousNoteHotKeyID:
						OnPreviousNoteHotkey();
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

	private static void OnNewNoteHotkey() => CreateNewNote();

	private static void OnPreviousNoteHotkey()
	{
		if (PreviousOpenNote is not null)
		{
			OpenQuery(PreviousOpenNote);
			return;
		}

		int index = CurrentDatabase.CreateRecord(string.Empty);
		var record = CurrentDatabase.GetRecord(index);

		if (record is null)
			return;

		OpenQuery(record);
	}

	private static void RegisterHotKeys()
	{
		if (hWndHelper is null)
			return;

		RegisterHotKey(hWndHelper.Handle, NewNoteHotKeyID, 2, (uint)KeyInterop.VirtualKeyFromKey(Key.N));
		RegisterHotKey(hWndHelper.Handle, PreviousNoteHotKeyID, 2, (uint)KeyInterop.VirtualKeyFromKey(Key.L));
	}

	public static void Release()
	{
		WindowSource?.RemoveHook(HwndHook);
		WindowSource = null;
		UnregisterHotKeys();
	}

	private static void UnregisterHotKeys()
	{
		if (hWndHelper is null)
			return;

		UnregisterHotKey(hWndHelper.Handle, NewNoteHotKeyID);
		UnregisterHotKey(hWndHelper.Handle, PreviousNoteHotKeyID);
	}
}
