using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static SylverInk.Net.NetworkUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.Net;

internal sealed class NetClient : IDisposable
{
    private IPAddress? Address;
    private readonly BackgroundWorker ClientTask = new() { WorkerSupportsCancellation = true };
    private readonly Database? DB;
    private TcpClient DBClient = new();
    private bool _disposed;
    private byte? Flags;

    internal bool Active { get; private set; }
    internal bool Connected { get; private set; }
    internal System.Windows.Shapes.Ellipse? Indicator { get; private set; }

    internal NetClient(Database DB)
    {
        this.DB = DB;

        ClientTask.DoWork += async (sender, _) =>
        {
            var task = (BackgroundWorker?)sender;
            while (!task?.CancellationPending is true)
            {
                try
                {
                    if (DBClient.Available > 0)
                        await ReadFromStream(DBClient, this.DB);

                    if (!DBClient.Connected || !DBClient.GetStream().Socket.Connected)
                        Concurrent(Disconnect);
                }
                catch (Exception e)
                {
                    App.LogException(e);
                    Concurrent(Disconnect);
                }
            }
        };
    }

    internal async Task Connect(string code)
    {
        if (code.Length != 6)
            return;

        Indicator ??= new()
        {
            StrokeThickness = 1.0,
            Tag = this.DB
        };

        Active = true;
        Address = CodeToAddress(code, out Flags);

        if (DBClient.Connected)
            Disconnect();

        UpdateIndicator(Indicator, IndicatorStatus.Connecting);

        DBClient = new()
        {
            ReceiveBufferSize = int.MaxValue,
            SendBufferSize = int.MaxValue
        };

        try
        {
            await DBClient.ConnectAsync(Address, TcpPort);
            await DBClient.GetStream().WriteAsync(new List<byte>([Flags ?? 0]).ToArray());
        }
        catch (Exception e)
        {
            App.LogException(e);
            ShowTooltip(Strings.FailedConnection!);
            if (DB is not null)
                Concurrent(RemoveDatabase, DB);
            return;
        }

        ClientTask.RunWorkerAsync();
    }

    internal async void Disconnect()
    {
        ClientTask.CancelAsync();
        DBClient.Close();
        await Task.Run(() => SpinWait.SpinUntil(new(() => !DBClient.Connected)));
        Active = false;
        Connected = false;
        UpdateIndicator(Indicator, IndicatorStatus.Inactive);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            ClientTask.Dispose();
            DBClient.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal async void Send(MessageType type, byte[] data)
    {
        if (!DBClient.Connected)
            return;

        byte[] id = [(byte)type];
        byte[] streamData = [.. id, .. data];

        try
        {
            await DBClient.GetStream().WriteAsync(streamData);
        }
        catch (Exception e)
        {
            App.LogException(e);
            ShowTooltip(Strings.NetClosed!);
            Disconnect();
        }
        finally
        {
            RefreshRecentNotes();
        }
    }
}
