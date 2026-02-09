using SylverInk.Net;
using SylverInk.Notes;
using SylverInk.Text;
using SylverInk.XAML;
using SylverInk.XAML.Objects;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAMLUtils;

public static class SearchResultUtils
{
	[DllImport("user32.dll")]
	static extern int GetWindowLong(nint hwnd, int index);

	[DllImport("user32.dll")]
	static extern int SetWindowLong(nint hwnd, int index, int newStyle);

	private const int GWL_EXSTYLE = -20;
	private const int WS_EX_LAYERED = 0x00080000;
	private const int WS_EX_TRANSPARENT = 0x00000020;

	public struct SimplePoint(int x, int y)
	{
		public int X { get; set; } = x;
		public int Y { get; set; } = y;
	}

	public static void AddTabToRibbon(this SearchResult window)
	{
		if (window.ResultRecord is null)
			return;

		if (window.ResultRecord.DB is null)
			return;

		SwitchDatabase(window.ResultRecord.DB);

		TabItem item = new()
		{
			Content = new NoteTab() {
				InitialPointer = window.ResultBlock.CaretPosition,
				Record = window.ResultRecord
			},
			Header = GetRibbonHeader(window.ResultRecord),
		};

		var ChildPanel = GetChildPanel("DatabasesPanel");
		ChildPanel.SelectedIndex = ChildPanel.Items.Add(item);
		OpenTabs.Add(item);

		window.StopMonitors();
		window.Close();
	}

	public static void Construct(this SearchResult window)
	{
		if (window.FinishedLoading)
			return;

		if (window.ResultRecord?.Locked is true)
		{
			window.LastChangedLabel.Content = "Locked by another user";
			window.ResultBlock.IsEnabled = false;
		}
		else
		{
			window.LastChangedLabel.Content = window.ResultRecord?.GetLastChange();
			window.ResultRecord?.DB?.Transmit(NetworkUtils.MessageType.RecordUnlock, IntToBytes(window.ResultRecord?.Index ?? 0));
		}

		window.Edited = false;
		window.ResultBlock.Document = window.ResultRecord?.GetDocument() ?? new();
		window.ResultBlock.Focus();

		window.OriginalBlockCount = window.ResultBlock.Document.Blocks.Count;
		window.OriginalRevisionCount = window.ResultRecord?.GetNumRevisions() ?? 0;
		window.OriginalText = TextConverter.Save(window.ResultBlock.Document, TextFormat.Xaml);

		var tabPanel = GetChildPanel("DatabasesPanel");
		for (int i = tabPanel.Items.Count - 1; i > 0; i--)
		{
			if (tabPanel.Items[i] is not TabItem item)
				continue;

			if (item.Tag is not NoteRecord record)
				continue;

			if (record.Equals(window.ResultRecord) is true)
				tabPanel.Items.RemoveAt(i);
		}

		window.FinishedLoading = true;
	}

	public static void Drag(this SearchResult window, object? sender, MouseEventArgs e)
	{
		if (!window.Dragging)
			return;

		var mouse = window.PointToScreen(e.GetPosition(null));
		var newCoords = new Point()
		{
			X = window.DragMouseCoords.X + mouse.X,
			Y = window.DragMouseCoords.Y + mouse.Y
		};

		if (CommonUtils.Settings.SnapSearchResults)
			window.Snap(ref newCoords);

		window.Left = newCoords.X;
		window.Top = newCoords.Y;
	}

	private static void InitEnterMonitor(SearchResult window)
	{
		window.EnterMonitor = new()
		{
			Interval = new TimeSpan(0, 0, 0, 0, 20)
		};

		window.EnterMonitor.Tick += (_, _) =>
		{
			var Seconds = (DateTime.UtcNow.Ticks - window.EnterTime) * 1E-7;

			if (Seconds > CommonUtils.Settings.NoteClickthrough || CommonUtils.Settings.NoteTransparency == 0.0)
			{
				Concurrent(window.UnsetWindowExTransparent);
				window.Opacity = 1.0;
				window.EnterMonitor.Stop();
				return;
			}

			var tick = Seconds * CommonUtils.Settings.NoteClickthroughInverse;
			window.Opacity = Lerp(window.StartOpacity, 1.0, tick * tick);
		};
	}

	private static void InitLeaveMonitor(SearchResult window)
	{
		window.LeaveMonitor = new()
		{
			Interval = new TimeSpan(0, 0, 0, 0, 20)
		};

		window.LeaveMonitor.Tick += (_, _) =>
		{
			var Seconds = (DateTime.UtcNow.Ticks - window.LeaveTime) * 1E-7;

			if (Seconds > CommonUtils.Settings.NoteClickthrough || CommonUtils.Settings.NoteTransparency == 0.0)
			{
				window.Opacity = 1.0 - (CommonUtils.Settings.NoteTransparency * 0.01);
				window.LeaveMonitor.Stop();
				return;
			}

			var tick = Seconds * CommonUtils.Settings.NoteClickthroughInverse;
			window.Opacity = Lerp(window.StartOpacity, 1.0 - (CommonUtils.Settings.NoteTransparency * 0.01), tick * tick);
		};
	}

	private static void InitMouseMonitor(SearchResult window)
	{
		window.MouseMonitor = new()
		{
			Interval = new TimeSpan(0, 0, 0, 0, 100)
		};

		window.MouseMonitor.Tick += window.WindowMouseMonitor;
	}

	public static void InitMonitors(this SearchResult window)
	{
		InitEnterMonitor(window);
		InitLeaveMonitor(window);
		InitMouseMonitor(window);
	}

	public static void SaveRecord(this SearchResult window)
	{
		if (window.ResultRecord is null)
			return;

		window.ResultRecord?.DB?.CreateRevision(window.ResultRecord, TextConverter.Save(window.ResultBlock.Document, TextFormat.Xaml));
		window.LastChangedLabel.Content = window.ResultRecord?.GetLastChange();
		DeferUpdateRecentNotes();
	}

	public static bool SetWindowExTransparent(this SearchResult window)
	{
		var extendedStyle = GetWindowLong(window.HWnd, GWL_EXSTYLE);
		return SetWindowLong(window.HWnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT) != 0;
	}

	private static Point Snap(this SearchResult window, ref Point Coords)
	{
		var (XSnapped, YSnapped) = (false, false);

		foreach (SearchResult other in OpenQueries)
		{
			if (other.ResultRecord == window.ResultRecord)
				continue;

			Point LT1 = new(Coords.X, Coords.Y); // Left-top corner of this window
			Point RB1 = new(Coords.X + window.Width, Coords.Y + window.Height); // Right-bottom corner of this window
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
			// If the corners' X-delta values are within tolerance, and the windows are overlapping on the Y axis, then snap the windows along their top-bottom edges.
			// Do the same for the Y-delta values and the left-right edges.

			if (dLR < window.SnapTolerance && YTolerance && !XSnapped)
			{
				Coords.X = RB2.X;
				XSnapped = true;
			}

			if (dRL < window.SnapTolerance && YTolerance && !XSnapped)
			{
				Coords.X = LT2.X - window.Width;
				XSnapped = true;
			}

			if (dTB < window.SnapTolerance && XTolerance && !YSnapped)
			{
				Coords.Y = RB2.Y;
				YSnapped = true;
			}

			if (dBT < window.SnapTolerance && XTolerance && !YSnapped)
			{
				Coords.Y = LT2.Y - window.Height;
				YSnapped = true;
			}

			if (XSnapped && YSnapped)
				return Coords;

			// Matching-corner snapping:
			// If the windows are already snapped along one edge, and have now been dragged so that both axes are within tolerance, then snap them along the other edge.

			if (dLL < window.SnapTolerance && !XSnapped && YSnapped)
			{
				Coords.X = LT2.X;
				return Coords;
			}

			if (dRR < window.SnapTolerance && !XSnapped && YSnapped)
			{
				Coords.X = RB2.X - window.Width;
				return Coords;
			}

			if (dTT < window.SnapTolerance && XSnapped && !YSnapped)
			{
				Coords.Y = LT2.Y;
				return Coords;
			}

			if (dBB < window.SnapTolerance && XSnapped && !YSnapped)
			{
				Coords.Y = RB2.Y - window.Height;
				return Coords;
			}
		}

		return Coords;
	}

	public static void StopMonitors(this SearchResult window)
	{
		window.EnterMonitor?.Stop();
		window.LeaveMonitor?.Stop();
		window.MouseMonitor?.Stop();
	}
	public static bool UnsetWindowExTransparent(this SearchResult window)
	{
		int extendedStyle = GetWindowLong(window.HWnd, GWL_EXSTYLE);
		return SetWindowLong(window.HWnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_LAYERED & ~WS_EX_TRANSPARENT) != 0;
	}
}
