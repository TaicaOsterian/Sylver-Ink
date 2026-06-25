using SylverInk.Notes;
using SylverInk.XAMLUtils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static SylverInk.CommonUtils;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Search.xaml
/// </summary>
public partial class Search : Window
{
	public string Query { get; private set; } = string.Empty;

	public Search()
	{
		DataContext = CommonUtils.Settings;
		InitializeComponent();
		CreateContextMenu();
	}

	private void CloseClick(object? sender, RoutedEventArgs e) => Close();

	private async void ContextDelete(object? sender, RoutedEventArgs e)
	{
		if (RecentSelection is null)
			return;

		if (MessageBox.Show("Are you sure you want to delete this note?", "Sylver Ink: Notification", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
			return;

		CurrentDatabase.DeleteRecord(RecentSelection);

		await this.PerformSearch();

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

		Results.ContextMenu = menu;
	}

	private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

	private void ListItemChosen(object? sender, MouseButtonEventArgs e)
	{
		if (sender is not ListBox box)
			return;

		if (box.SelectedItem is not NoteRecord record)
			return;

		RecentSelection = record;

		if (e.ChangedButton == MouseButton.Right)
			return;

		OpenQuery(record)?.ScrollToText(Query);
	}

	private void OnClose(object? sender, EventArgs e)
	{
		CommonUtils.Settings.SearchResults.Clear();
	}

	private async void QueryClick(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button button)
			return;

		button.Content = "Querying...";
		button.IsEnabled = false;

		Query = SearchText.Text ?? string.Empty;

		await this.PerformSearch();
	}

	private async void SearchLoaded(object sender, RoutedEventArgs e)
	{
		Query = string.Empty;

		await this.PerformSearch();
	}
}
