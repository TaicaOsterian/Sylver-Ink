using SylverInk.Notes;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Windows;
using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.Interop;

public static class MutexUtils
{
	private static Mutex? mutex;
	private static readonly CancellationTokenSource mutexTokenSource = new();
	private static readonly string MutexName = $"SylverInk/{typeof(MainWindow).GUID}";

	/// <summary>
	/// Mutex management in Sylver Ink allows passing shell verbs through a named pipe to an existing open instance.
	/// </summary>
	/// <returns><see langword="true"/> if and only if shell verbs were passed from this instance to an existing one (i.e. from mutex client to server).</returns>
	private static bool HandleMutex(bool mutexCreated)
	{
		if (mutexCreated)
		{
			Thread mutexPipeThread = new(() => HandleMutexPipe(mutexTokenSource.Token));
			mutexPipeThread.Start();
			return false;
		}

		mutex = null;
		var args = Environment.GetCommandLineArgs();

		var client = new NamedPipeClientStream(MutexName);
		client.Connect();

		using (StreamWriter writer = new(client))
			writer.Write(string.Join("\t", args));

		if (args.Length > 1)
			return true;

		return false;
	}

	private async static void HandleMutexPipe(CancellationToken token)
	{
		using var server = new NamedPipeServerStream(MutexName);

		while (mutex != null)
		{
			try
			{
				await server.WaitForConnectionAsync(token);
			}
			catch
			{
				return;
			}

			if (token.IsCancellationRequested)
				return;

			using StreamReader reader = new(server);
			string[] args = [.. reader.ReadToEnd().Split("\t", StringSplitOptions.RemoveEmptyEntries)];
			bool activated;
			var now = DateTime.UtcNow;

			do
			{
				activated = Concurrent(Application.Current.MainWindow.Activate);
				Concurrent(Application.Current.MainWindow.Focus);
			} while (!activated && !Application.Current.MainWindow.IsFocused && (DateTime.UtcNow - now).Seconds < 1);

			Concurrent(() => HandleShellVerbs(args));
		}
	}

	private static void HandleShellVerbs(string[]? args = null)
	{
		if ((args ??= Environment.GetCommandLineArgs()).Length < 2)
			return;

		switch (args[1])
		{
			case "open": // &Open
				HandleVerbOpen(args.Length > 2 ? args[2] : string.Empty);
				break;
			default: // &Open
				HandleVerbOpen(args[1]);
				break;
		}
	}

	private async static void HandleVerbOpen(string filename)
	{
		if (string.IsNullOrWhiteSpace(filename))
			return;

		var wideBreak = string.Empty;

		foreach (string dbFile in InitComplete ? Databases.Select(db => db.DBFile) : Settings.LastDatabases)
			if (Path.GetFullPath(dbFile).Equals(Path.GetFullPath(filename)))
				wideBreak = Path.GetFullPath(dbFile);

		if (string.IsNullOrWhiteSpace(wideBreak))
		{
			ShellDB = Path.GetFullPath(filename);
			await Database.Create(filename);
			return;
		}

		ShellDB = wideBreak;
		SwitchDatabase($"~F:{wideBreak}");
	}

	public static bool Init()
	{
		mutex = new Mutex(true, MutexName, out bool mutexCreated);
		return HandleMutex(mutexCreated);
	}

	public static void Release()
	{
		mutexTokenSource.Cancel();
		mutex = null;
	}
}
