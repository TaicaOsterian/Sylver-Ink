using SylverInk.Interop;
using SylverInk.Net;
using SylverInk.Notes;
using SylverInk.XAML;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static SylverInk.CommonUtils;
using static SylverInk.FileIO.FileUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.DataUtils;

namespace SylverInk;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
	private bool ShellVerbsPassed;

	public MainWindow()
	{
		InitializeComponent();
		DataContext = CommonUtils.Settings;
	}

	private void Button_Click(object? sender, RoutedEventArgs e)
	{
		var senderObject = (Button?)sender;

		switch (senderObject?.Content)
		{
			case "Import":
				ImportWindow = new();
				break;
			case "Replace":
				ReplaceWindow = new();
				break;
			case "Search":
				SearchWindow = new();
				break;
			case "Settings":
				SettingsWindow = new();
				break;
			case "Exit":
				Close();
				break;
		}
	}

	private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

	private async void HandleCheckInit()
	{
		using var tokenSource = new CancellationTokenSource();
		var token = tokenSource.Token;

		var initTask = Task.Run(() =>
		{
			do
			{
				InitComplete = Databases.Count > 0
					&& SettingsLoaded
					&& UpdatesChecked;

				if (Concurrent(() => Application.Current.MainWindow.FindName("DatabasesPanel")) is null)
					InitComplete = false;
			} while (!InitComplete && !token.IsCancellationRequested);
		}, token);

		await initTask;

		if (string.IsNullOrEmpty(ShellDB))
			SwitchDatabase($"~N:{CommonUtils.Settings.LastActiveDatabase}");
		else
			SwitchDatabase($"~F:{ShellDB}");

		foreach (var openNote in LastActiveNotes)
		{
			var oSplit = openNote.Split(':');
			if (oSplit.Length < 2)
				continue;

			if (!int.TryParse(oSplit[1], out var iNote))
				continue;

			Database? target = null;
			foreach (Database db in Databases)
				if (oSplit[0].Equals(db.Name))
					target = db;

			if (target is null)
				continue;

			if (!target.HasRecord(iNote))
				continue;

			if (OpenQuery(target.GetRecord(iNote)) is not SearchResult result)
				continue;

			if (LastActiveNotesHeight.TryGetValue($"{target.Name}:{iNote}", out var openHeight))
				result.Height = openHeight;

			if (LastActiveNotesLeft.TryGetValue($"{target.Name}:{iNote}", out var openLeft))
				result.Left = openLeft;

			if (LastActiveNotesTop.TryGetValue($"{target.Name}:{iNote}", out var openTop))
				result.Top = openTop;

			if (LastActiveNotesWidth.TryGetValue($"{target.Name}:{iNote}", out var openWidth))
				result.Width = openWidth;
		}

		CanResize = true;
		LastActiveNotes.Clear();
		LastActiveNotesHeight.Clear();
		LastActiveNotesLeft.Clear();
		LastActiveNotesTop.Clear();
		LastActiveNotesWidth.Clear();
		ResizeMode = ResizeMode.CanResize;
		CommonUtils.Settings.MainTypeFace = new(CommonUtils.Settings.MainFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
		DeferUpdateRecentNotes();
	}

	private static bool IsShuttingDown()
	{
		try
		{
			Application.Current.ShutdownMode = Application.Current.ShutdownMode;
			return false;
		}
		catch
		{
			return true;
		}
	}

	private async void MainWindow_Closing(object? sender, CancelEventArgs e)
	{
		if (IsShuttingDown()) // Prevent redundant event handling.
			return;

		if (AbortRun)
		{
			Application.Current.Shutdown();
			return;
		}

		CommonUtils.Settings.Save();

		if (!DatabaseChanged)
		{
			switch (MessageBox.Show("Are you sure you wish to exit Sylver Ink?", "Sykver Ink: Notification", MessageBoxButton.YesNo, MessageBoxImage.Question))
			{
				case MessageBoxResult.No:
					e.Cancel = true;
					return;
				case MessageBoxResult.Yes:
					Application.Current.Shutdown();
					return;
			}
		}

		switch (MessageBox.Show("Do you want to save your work before exiting?", "Sylver Ink: Notification", MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
		{
			case MessageBoxResult.Cancel:
				e.Cancel = true;
				return;
			case MessageBoxResult.Yes:
				e.Cancel = true;
				MainGrid.IsEnabled = false;

				foreach (Database db in Databases)
					Erase(GetLockFile(db.DBFile));

				await SaveDatabases();

				DatabaseChanged = false;
				CommonUtils.Settings.Save();
				Application.Current.Shutdown();
				return;
			case MessageBoxResult.No:
				foreach (Database db in Databases)
					Erase(GetLockFile(db.DBFile));

				Application.Current.Shutdown();
				return;
		}
	}

	private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e) => DeferUpdateRecentNotes();

	private void NewNote_Keydown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
			return;

		if (sender is not TextBox box)
			return;

		CurrentDatabase.CreateRecord(box.Text);
		box.Text = string.Empty;
		DeferUpdateRecentNotes();
	}

	protected override void OnClosed(EventArgs e)
	{
		HotKeyUtils.Release();
		MutexUtils.Release();
		base.OnClosed(e);
	}

	protected override async void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);

		// Hotkey registration
		HotKeyUtils.Init();

		// Database initialization
		HandleCheckInit();
		ShellVerbsPassed = MutexUtils.Init();

		if (InstanceRunning())
		{
			if (!ShellVerbsPassed)
				MessageBox.Show("Another instance of Sylver Ink is already running.", "Sylver Ink: Error", MessageBoxButton.OK, MessageBoxImage.Error);

			// If shell verbs were passed to an existing instance, close this instance silently before a head is established.
			AbortRun = true;
			Close();
			return;
		}

		// Settings initialization
		await CommonUtils.Settings.Load();
		SettingsLoaded = true;

		foreach (var folder in Subfolders)
			if (!Directory.Exists(folder.Value))
				Directory.CreateDirectory(folder.Value);

		// Style initialization
		SetMenuColors(this);

		// (If initialization was interrupted, prevent marking it as completed)
		if (!IsShuttingDown())
			UpdatesChecked = true;

		// Create an empty database if and only if we haven't loaded any from files
		if (CommonUtils.Settings.FirstRun)
			await Database.Create(Path.Join(Subfolders["Databases"], DefaultDatabase, $"{DefaultDatabase}.sidb"));

		// Refresh the display (checking for updates is a blocking call, so we want to populate the recent notes list beforehand)
		DeferUpdateRecentNotes();

		// Check for updates
		Erase(UpdateHandler.UpdateLockUri);
		Erase(UpdateHandler.TempUri);
		await UpdateHandler.CheckForUpdates();
	}
}
