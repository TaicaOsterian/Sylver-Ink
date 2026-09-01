using SylverInk.Net;
using SylverInk.Notes;
using SylverInk.Text;
using SylverInk.XAML.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using static SylverInk.CommonUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for SearchResult.xaml
/// </summary>
public partial class SearchResult : Window, IDisposable
{
    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out SimplePoint pPoint);

    [DllImport("user32.dll")]
    static extern int GetWindowLong(nint hwnd, int index);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(nint hwnd, int index, int newStyle);

    public struct SimplePoint(int x, int y)
    {
        public int X { get; set; } = x;
        public int Y { get; set; } = y;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private bool MouseInside;

    public bool Dragging { get; private set; }
    public Point DragMouseCoords { get; private set; } = new(0, 0);
    public DispatcherTimer? EnterMonitor { get; set; }
    public long EnterTime { get; set; }
    public nint HWnd { get; set; }
    public DispatcherTimer? LeaveMonitor { get; set; }
    public long LeaveTime { get; set; }
    public DispatcherTimer? MouseMonitor { get; set; }
    public int OriginalRevisionCount { get; set; }
    public double SnapTolerance { get; } = 20.0;
    public double StartOpacity { get; set; }
    public SearchResultViewModel ViewModel => (SearchResultViewModel)DataContext;

    public SearchResult()
    {
        DataContext = new SearchResultViewModel();
        ViewModel.RequestClose += (_, _) => HandleClose();
        InitializeComponent();
        InitMonitors();
    }

    public void Construct()
    {
        ViewModel.Construct();
    }

    public void Drag(object? sender, MouseEventArgs e)
    {
        if (!Dragging)
            return;

        var mouse = PointToScreen(e.GetPosition(null));
        var newCoords = new Point()
        {
            X = DragMouseCoords.X + mouse.X,
            Y = DragMouseCoords.Y + mouse.Y
        };

        if (CommonUtils.Settings.SnapSearchResults)
            Snap(ref newCoords);

        Left = newCoords.X;
        Top = newCoords.Y;
    }

    private void InitEnterMonitor()
    {
        EnterMonitor = new()
        {
            Interval = new TimeSpan(0, 0, 0, 0, 20)
        };

        EnterMonitor.Tick += (_, _) =>
        {
            var Seconds = (DateTime.UtcNow.Ticks - EnterTime) * 1E-7;

            if (Seconds > CommonUtils.Settings.NoteClickthrough || CommonUtils.Settings.NoteTransparency == 0.0)
            {
                Concurrent(UnsetWindowExTransparent);
                Opacity = 1.0;
                EnterMonitor.Stop();
                return;
            }

            var tick = Seconds * CommonUtils.Settings.NoteClickthroughInverse;
            Opacity = Lerp(StartOpacity, 1.0, tick * tick);
        };
    }

    private void InitLeaveMonitor()
    {
        LeaveMonitor = new()
        {
            Interval = new TimeSpan(0, 0, 0, 0, 20)
        };

        LeaveMonitor.Tick += (_, _) =>
        {
            var Seconds = (DateTime.UtcNow.Ticks - LeaveTime) * 1E-7;

            if (Seconds > CommonUtils.Settings.NoteClickthrough || CommonUtils.Settings.NoteTransparency == 0.0)
            {
                Opacity = 1.0 - (CommonUtils.Settings.NoteTransparency * 0.01);
                LeaveMonitor.Stop();
                return;
            }

            var tick = Seconds * CommonUtils.Settings.NoteClickthroughInverse;
            Opacity = Lerp(StartOpacity, 1.0 - (CommonUtils.Settings.NoteTransparency * 0.01), tick * tick);
        };
    }

    private void InitMouseMonitor()
    {
        MouseMonitor = new()
        {
            Interval = new TimeSpan(0, 0, 0, 0, 100)
        };

        MouseMonitor.Tick += WindowMouseMonitor;
    }

    public void InitMonitors()
    {
        InitEnterMonitor();
        InitLeaveMonitor();
        InitMouseMonitor();
    }

    public void RequestClose(NoteRecord? source = null)
    {
        if (source is null || !ViewModel.Record.Equals(source))
            return;

        HandleClose(true);
    }

    public bool RequestOpen(NoteRecord source)
    {
        if (!ViewModel.Record.Equals(source))
            return false;

        Activate();
        Focus();

        return true;
    }

    public void RequestUnlock(NoteRecord record) => ViewModel.RequestUnlock(record);

    public void SaveRecord()
    {
        if (ViewModel.Record is null)
            return;

        ViewModel.Record?.DB?.CreateRevision(ViewModel.Record, TextConverter.Save(ViewModel.Document, TextFormat.Xaml));
        ViewModel.LastChange = ViewModel.Record?.GetLastChange();
        DeferUpdateRecentNotes();
    }

    public bool SetWindowExTransparent()
    {
        var extendedStyle = GetWindowLong(HWnd, GWL_EXSTYLE);
        return SetWindowLong(HWnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT) != 0;
    }

    private Point Snap(ref Point Coords)
    {
        var (XSnapped, YSnapped) = (false, false);

        foreach (SearchResult other in OpenQueries)
        {
            if (other.ViewModel.Record == ViewModel.Record)
                continue;

            Point LT1 = new(Coords.X, Coords.Y); // Left-top corner of this window
            Point RB1 = new(Coords.X + Width, Coords.Y + Height); // Right-bottom corner of this window
            Point LT2 = new(other.Left, other.Top); // Left-top corner of the other window
            Point RB2 = new(other.Left + other.Width, other.Top + other.Height); // Right-bottom corner of the other window

            // X-delta and Y-delta values from the left-top corners of each window to the opposite corners of the other.
            var dLR = Math.Abs(LT1.X - RB2.X);
            var dRL = Math.Abs(RB1.X - LT2.X);
            var dTB = Math.Abs(LT1.Y - RB2.Y);
            var dBT = Math.Abs(RB1.Y - LT2.Y);

            // X-delta and Y-delta values from the left-top and right-bottom corners of each window to the corresponding corners of the other.
            var dLL = Math.Abs(LT1.X - LT2.X);
            var dRR = Math.Abs(RB1.X - RB2.X);
            var dTT = Math.Abs(LT1.Y - LT2.Y);
            var dBB = Math.Abs(RB1.Y - RB2.Y);

            // Check for left and right edges of either window being between the edges of the other.
            bool XTolerance = (LT1.X >= LT2.X && LT1.X <= RB2.X)
                || (RB1.X >= LT2.X && RB1.X <= RB2.X)
                || (LT2.X >= LT1.X && LT2.X <= RB1.X)
                || (RB2.X >= LT1.X && RB2.X <= RB1.X);

            // Check for top and bottom edges of either window being between the edges of the other.
            bool YTolerance = (LT1.Y >= LT2.Y && LT1.Y <= RB2.Y)
                || (RB1.Y >= LT2.Y && RB1.Y <= RB2.Y)
                || (LT2.Y >= LT1.Y && LT2.Y <= RB1.Y)
                || (RB2.Y >= LT1.Y && RB2.Y <= RB1.Y);

            // Opposite-corner snapping:
            // If the corners' X-delta values are within tolerance, and the windows are overlapping on the Y axis, then snap the windows along their vertical edges.
            // Do the same for the Y-delta values and the left-right edges.

            if (dLR < SnapTolerance && YTolerance && !XSnapped)
            {
                Coords.X = RB2.X;
                XSnapped = true;
            }

            if (dRL < SnapTolerance && YTolerance && !XSnapped)
            {
                Coords.X = LT2.X - Width;
                XSnapped = true;
            }

            if (dTB < SnapTolerance && XTolerance && !YSnapped)
            {
                Coords.Y = RB2.Y;
                YSnapped = true;
            }

            if (dBT < SnapTolerance && XTolerance && !YSnapped)
            {
                Coords.Y = LT2.Y - Height;
                YSnapped = true;
            }

            if (XSnapped && YSnapped)
                return Coords;

            // Matching-corner snapping:
            // If the windows are already snapped along one edge, and have now been dragged so that both axes are within tolerance, then snap them along the other edge.

            if (dLL < SnapTolerance && !XSnapped && YSnapped)
            {
                Coords.X = LT2.X;
                return Coords;
            }

            if (dRR < SnapTolerance && !XSnapped && YSnapped)
            {
                Coords.X = RB2.X - Width;
                return Coords;
            }

            if (dTT < SnapTolerance && XSnapped && !YSnapped)
            {
                Coords.Y = LT2.Y;
                return Coords;
            }

            if (dBB < SnapTolerance && XSnapped && !YSnapped)
            {
                Coords.Y = RB2.Y - Height;
                return Coords;
            }
        }

        return Coords;
    }

    public void StopMonitors()
    {
        EnterMonitor?.Stop();
        LeaveMonitor?.Stop();
        MouseMonitor?.Stop();
    }
    public bool UnsetWindowExTransparent()
    {
        int extendedStyle = GetWindowLong(HWnd, GWL_EXSTYLE);
        return SetWindowLong(HWnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_LAYERED & ~WS_EX_TRANSPARENT) != 0;
    }

    public void Dispose()
    {
        StopMonitors();
        GC.SuppressFinalize(this);
    }

    private void HandleClose(bool force = false)
    {
        if (ViewModel.Edited && !force)
        {
            switch (MessageBox.Show("You have unsaved changes. Save before closing this note?", "Sylver Ink: Notification", MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
            {
                case MessageBoxResult.Cancel:
                    return;
                case MessageBoxResult.No:
                    ViewModel.Edited = false;
                    for (int i = (ViewModel.Record?.GetNumRevisions() ?? 1) - 1; i >= OriginalRevisionCount; i--)
                        ViewModel.Record?.DeleteRevision(i);
                    RecentNotesDirty = true;
                    DeferUpdateRecentNotes();
                    break;
            }
        }

        Close();
    }

    private void Result_Closed(object? sender, EventArgs e)
    {
        StopMonitors();
        PreviousOpenNote = ViewModel.Record;

        if (ViewModel.Edited)
            SaveRecord();

        ViewModel.Record?.DB?.Transmit(NetworkUtils.MessageType.RecordUnlock, ViewModel.Record?.Index.ToByteArray());

        foreach (SearchResult result in OpenQueries)
        {
            if (result.ViewModel.Record != ViewModel.Record)
                continue;

            OpenQueries.Remove(result);
            return;
        }
    }

    private void ResultBlock_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ViewModel.Autosave();
    }

    private void WindowActivated(object? sender, EventArgs e)
    {
        Opacity = 1.0;
        ViewModel.IsFocused = true;

        UnsetWindowExTransparent();
    }

    private void WindowDeactivated(object? sender, EventArgs e)
    {
        Opacity = 1.0 - (CommonUtils.Settings.NoteTransparency * 0.01);
        ViewModel.IsFocused = false;

        SetWindowExTransparent();
    }

    private void WindowLoaded(object? sender, RoutedEventArgs e)
    {
        Construct();

        HWnd = new WindowInteropHelper(this).Handle;
        MouseMonitor?.Start();
    }

    private void WindowMove(object? sender, MouseEventArgs e) => Drag(sender, e);

    private void WindowMouseDown(object? sender, MouseButtonEventArgs e)
    {
        var n = PointToScreen(e.GetPosition(null));
        CaptureMouse();
        DragMouseCoords = new(Left - n.X, Top - n.Y);
        Dragging = true;
    }

    private void WindowMouseEnter(object sender, MouseEventArgs e)
    {
        if (IsActive)
            return;

        if (EnterMonitor?.IsEnabled is true)
            return;

        LeaveMonitor?.Stop();

        StartOpacity = Opacity;

        EnterTime = DateTime.UtcNow.Ticks;
        EnterMonitor?.Start();
    }

    private void WindowMouseLeave(object sender, MouseEventArgs e)
    {
        if (IsActive)
            return;

        if (LeaveMonitor?.IsEnabled is true)
            return;

        if (CommonUtils.Settings.NoteTransparency == 0.0)
            return;

        EnterMonitor?.Stop();

        StartOpacity = Opacity;

        if (StartOpacity == 1.0 - (CommonUtils.Settings.NoteTransparency * 0.01))
            return;

        Concurrent(SetWindowExTransparent);
        LeaveTime = DateTime.UtcNow.Ticks;
        LeaveMonitor?.Start();
    }

    public void WindowMouseMonitor(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out SimplePoint screenPosition))
            return;

        var eventArgs = new MouseEventArgs(Mouse.PrimaryDevice, 0);
        Point position;

        try
        {
            position = PointFromScreen(new(screenPosition.X, screenPosition.Y));
        }
        catch
        {
            return;
        }

        if (position.X > 0.0 &&
            position.X <= Width &&
            position.Y > 0.0 &&
            position.Y <= Height)
        {
            if (MouseInside)
                return;

            MouseInside = true;
            eventArgs.RoutedEvent = Mouse.MouseEnterEvent;
        }
        else
        {
            if (!MouseInside)
                return;

            MouseInside = false;
            eventArgs.RoutedEvent = Mouse.MouseLeaveEvent;
        }

        RaiseEvent(eventArgs);
    }

    private void WindowMouseUp(object? sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        DragMouseCoords = new(0, 0);
        Dragging = false;
    }
}
