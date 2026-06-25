using SylverInk.Notes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML.Objects;

/// <summary>
/// Interaction logic for DatabaseControl.xaml
/// </summary>
public partial class DatabaseControl : UserControl
{
    public DatabaseControl()
    {
        InitializeComponent();
		CreateContextMenu();
	}

	private void ButtonClick(object? sender, RoutedEventArgs e)
	{
		var senderObject = (Button?)sender;

		switch (senderObject?.Content)
		{
			case "Import":
				ImportWindow = new();
				break;
			case "Search":
				SearchWindow = new();
				break;
			case "Settings":
				SettingsWindow = new();
				break;
			case "Exit":
				Application.Current.MainWindow.Close();
				break;
		}
	}

	private void ContextDelete(object? sender, RoutedEventArgs e)
	{
		if (RecentSelection is null)
			return;

		if (MessageBox.Show("Are you sure you want to delete this note?", "Sylver Ink: Notification", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
			return;

		CurrentDatabase.DeleteRecord(RecentSelection);

		return;
	}

	private void ContextOpen(object? sender, RoutedEventArgs e)
	{
		if (RecentSelection is null)
			return;

		OpenQuery(RecentSelection);

		return;
	}

	private void CreateContextMenu()
	{
		ContextMenu menu = new();

		MenuItem itemOpen = new()
		{
			Header = "Open",
		};

		MenuItem itemDelete = new()
		{
			Header = "Delete",
		};

		itemOpen.Click += ContextOpen;
		itemDelete.Click += ContextDelete;

		menu.Items.Add(itemOpen);
		menu.Items.Add(itemDelete);

		PlusTab.ContextMenu = menu;
	}

	private void ListItemChosen(object sender, MouseButtonEventArgs e)
	{
		if (sender is not ListBox box)
			return;

		if (box.SelectedItem is not NoteRecord record)
			return;

		RecentSelection = record;

		// We set the recent selection on any click, but only open it on a left button click. This makes it easier for the context menu to grab the affected note when needed.
		if (e.ChangedButton == MouseButton.Right)
			return;

		OpenQuery(RecentSelection);
	}

	private void NewNoteKeydown(object? sender, KeyEventArgs e)
	{
		if (e.Key != Key.Enter)
			return;

		if (sender is not TextBox box)
			return;

		CurrentDatabase.CreateRecord(box.Text);
		box.Text = string.Empty;
		DeferUpdateRecentNotes();
	}
}
