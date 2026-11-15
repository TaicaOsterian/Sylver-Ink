using System.IO.Pipes;
using System.Threading;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.Interop;

internal static class MutexUtils
{
    private static Mutex? mutex;
    private static readonly CancellationTokenSource mutexTokenSource = new();
    private static readonly string MutexName = $"Sylver Ink/{typeof(MainWindow).GUID}";

    internal static int ShellVerbs { get; private set; }

    /// <summary>
    /// Mutex management in Sylver Ink allows passing shell verbs through a named pipe to an existing open instance.
    /// </summary>
    /// <returns><see langword="true"/> if and only if shell verbs were passed from this instance to an existing one (i.e. from mutex client to server).</returns>
    private static bool HandleMutex(bool mutexCreated)
    {
        if (mutexCreated)
        {
            Thread mutexPipeThread = new(() => HandleMutexPipe(mutexTokenSource.Token))
            {
                IsBackground = true
            };
            mutexPipeThread.Start();
            return false;
        }

        mutex = null;
        var args = Environment.GetCommandLineArgs();

        try
        {
            var client = new NamedPipeClientStream(MutexName);
            client.Connect(1000);

            using (StreamWriter writer = new(client))
                writer.Write(string.Join("\t", args));

            ShellVerbs = args.Length;

            return true;
        }
        catch (Exception e)
        {
            App.LogException(e);
            return false;
        }
    }

    private static async void HandleMutexPipe(CancellationToken token)
    {
        while (mutex != null)
        {
            await using var server = new NamedPipeServerStream(MutexName);

            try
            {
                await server.WaitForConnectionAsync(token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            catch (Exception e)
            {
                App.LogException(e);
                return;
            }

            if (token.IsCancellationRequested)
                return;

            using StreamReader reader = new(server);
            string[] args = [.. reader.ReadToEnd().Split("\t", StringSplitOptions.RemoveEmptyEntries)];
            bool activated;
            var now = DateTime.UtcNow;

            Concurrent(() =>
            {
                do
                {
                    activated = Application.Current.MainWindow.Activate();
                    Application.Current.MainWindow.Focus();
                } while (!activated && !Application.Current.MainWindow.IsFocused && (DateTime.UtcNow - now).Seconds < 1);
            });

            await Concurrent(HandleShellVerbs, args);
        }
    }

    private static async Task HandleShellVerbs(string[]? args = null)
    {
        args ??= Environment.GetCommandLineArgs();

        if (args.Length < 2)
            return;

        switch (args[1])
        {
            case "open": // &Open
                await HandleVerbOpen(args.Length > 2 ? args[2] : string.Empty);
                break;
            default: // &Open
                await HandleVerbOpen(args[1]);
                break;
        }
    }

    private static async Task HandleVerbOpen(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return;

        var wideBreak = string.Empty;

        foreach (string dbFile in InitComplete ? Databases.Select(db => db.DBFile) : Settings.LastDatabases)
        {
            if (Path.GetFullPath(dbFile).Equals(Path.GetFullPath(filename), SystemStringComparison))
                wideBreak = Path.GetFullPath(dbFile);
        }

        if (string.IsNullOrWhiteSpace(wideBreak))
        {
            ShellDB = Path.GetFullPath(filename);
            await Database.Create(filename);
            return;
        }

        ShellDB = wideBreak;
        SwitchDatabase($"~F:{wideBreak}");
    }

    internal static async Task<bool> Init()
    {
        await HandleShellVerbs();
        mutex = new Mutex(true, MutexName, out bool mutexCreated);
        return HandleMutex(mutexCreated);
    }

    internal static void Release()
    {
        mutexTokenSource.Cancel();
        mutex?.Dispose();
        mutex = null;
    }
}
