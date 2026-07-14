using SylverInk.Net;
using SylverInk.Notes;
using SylverInk.XAML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk;

/// <summary>
/// Static helper functions and properties serving multi- or general-purpose needs across the entire project.
/// </summary>
public static partial class CommonUtils
{
	public enum DisplayType
	{
		Content,
		Change,
		Creation,
		Index
	}

	public enum SortType
	{
		ByIndex,
		ByChange,
		ByCreation
	}

	public enum UUIDType
	{
		Database,
		Record,
		Revision
	}

	private static Import? _import;
	private static Search? _search;
	private static Settings? _settings;

	public static bool AbortRun { get; set; }
	public static string DateFormat { get; } = "yyyy-MM-dd HH:mm:ss";
	public static Import? ImportWindow { get => _import; set { _import?.Close(); _import = value; _import?.Show(); } }
	public static bool InitComplete { get; set; }
	public static List<string> LastActiveNotes { get; } = [];
	public static Dictionary<string, double> LastActiveNotesHeight { get; } = [];
	public static Dictionary<string, double> LastActiveNotesLeft { get; } = [];
	public static Dictionary<string, double> LastActiveNotesTop { get; } = [];
	public static Dictionary<string, double> LastActiveNotesWidth { get; } = [];
	public static List<SearchResult> OpenQueries { get; } = [];
	public static NoteRecord? PreviousOpenNote { get; set; }
	public static NoteRecord? RecentSelection { get; set; }
	public static Search? SearchWindow { get => _search; set { _search?.Close(); _search = value; _search?.Show(); } }
	public static ContextSettings Settings { get; } = new();
	public static bool SettingsLoaded { get; set; }
	public static Settings? SettingsWindow { get => _settings; set { _settings?.Close(); _settings = value; _settings?.Show(); } }
	public static bool UpdatesChecked { get; set; }
	public static double WindowHeight { get; set; }
	public static double WindowWidth { get; set; }

	/// <summary>
	/// Dispatch an action to the main thread for synchronous execution.
	/// </summary>
	/// <param name="callback">The action to be performed on the main thread</param>
	public static void Concurrent(Action callback) => Application.Current.Dispatcher.Invoke(callback);

	/// <summary>
	/// Dispatch an action with one argument and no return value to the main thread for synchronous execution.
	/// </summary>
	/// <param name="callback">The function to be executed on the main thread</param>
	public static void Concurrent<T>(Action<T> callback, T arg) => Application.Current.Dispatcher.Invoke(callback, arg);

	/// <summary>
	/// Dispatch an action with two arguments and no return value to the main thread for synchronous execution.
	/// </summary>
	/// <param name="callback">The function to be executed on the main thread</param>
	public static void Concurrent<T1, T2>(Action<T1, T2> callback, T1 arg1, T2 arg2) => Application.Current.Dispatcher.Invoke(callback, arg1, arg2);

	/// <summary>
	/// Dispatch an action with three arguments and no return value to the main thread for synchronous execution.
	/// </summary>
	/// <param name="callback">The function to be executed on the main thread</param>
	public static void Concurrent<T1, T2, T3>(Action<T1, T2, T3> callback, T1 arg1, T2 arg2, T3 arg3) => Application.Current.Dispatcher.Invoke(callback, arg1, arg2, arg3);

	/// <summary>
	/// Dispatch a function with no arguments to the main thread for synchronous execution, and return the result of that execution.
	/// </summary>
	/// <param name="callback">The function to be executed on the main thread</param>
	public static TResult Concurrent<TResult>(Func<TResult> callback) => Application.Current.Dispatcher.Invoke(callback);

	/// <summary>
	/// Dispatch a function with one argument to the main thread for synchronous execution, and return the result of that execution.
	/// </summary>
	/// <param name="callback">The function to be executed on the main thread</param>
	public static TResult Concurrent<T, TResult>(Func<T, TResult> callback, T arg) => (TResult)Application.Current.Dispatcher.Invoke(callback, arg);

	/// <summary>
	/// Dispatch a function with two arguments to the main thread for synchronous execution, and return the result of that execution.
	/// </summary>
	/// <param name="callback">The function to be executed on the main thread</param>
	public static TResult Concurrent<T1, T2, TResult>(Func<T1, T2, TResult> callback, T1 arg1, T2 arg2) => (TResult)Application.Current.Dispatcher.Invoke(callback, arg1, arg2);

	/// <summary>
	/// Dispatch a function with three arguments to the main thread for synchronous execution, and return the result of that execution.
	/// </summary>
	/// <param name="callback">The function to be executed on the main thread</param>
	public static TResult Concurrent<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> callback, T1 arg1, T2 arg2, T3 arg3) => (TResult)Application.Current.Dispatcher.Invoke(callback, arg1, arg2, arg3);

	public static bool InstanceRunning() => Process.GetProcessesByName("Sylver Ink").Length > 1 && !File.Exists(UpdateHandler.UpdateLockUri);

	public static byte[] ToByteArray(this int data) => [
		(byte)((data >> 24) & 0xFF),
		(byte)((data >> 16) & 0xFF),
		(byte)((data >> 8) & 0xFF),
		(byte)(data & 0xFF)
	];

	public static byte[] ToByteArray(this uint data) => [
		(byte)((data >> 24) & 0xFF),
		(byte)((data >> 16) & 0xFF),
		(byte)((data >> 8) & 0xFF),
		(byte)(data & 0xFF)
	];

	public static string MakeUUID(UUIDType type = UUIDType.Record)
	{
		var uuid = Guid.NewGuid().ToString("N");
		uuid = $"{uuid[..14]}{(byte)type:X2}{uuid[16..]}";
		return uuid.ToUpper(CultureInfo.InvariantCulture);
	}

	public async static Task OnFirstRun()
	{
		if (!Settings.FirstRun)
			return;

		// Create an empty database if and only if we haven't loaded any from files
		await Database.Create(Path.Join(Subfolders["Databases"], DefaultDatabase, $"{DefaultDatabase}.sidb"));

		return;
	}

	[GeneratedRegex(@"\((\p{Nd}+)\)$")]
	public static partial Regex IndexDigits();
}
