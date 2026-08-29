using SylverInk.XAML.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static SylverInk.CommonUtils;
using static SylverInk.XAMLUtils.MainWindowUtils;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class Settings : Window
{
	public SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

	public Settings()
	{
		DataContext = new SettingsViewModel();
		ViewModel.RequestClose += (_, _) => Close();
		InitializeComponent();
	}

	private void ColorPopup(object? sender, RoutedEventArgs e)
	{
		if (sender is not Button button)
			return;

		SettingsColorPicker.CustomColorPicker.ColorTag = (string?)button.Tag;
		SettingsColorPicker.ColorSelection.IsOpen = true;
	}

	private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

	private void Settings_Loaded(object? sender, RoutedEventArgs e)
	{
		SettingsColorPicker.InitBrushes();

		if (RibbonBox.SelectedItem is null)
		{
			foreach (ComboBoxItem item in RibbonBox.Items)
			{
				if (item.Tag.Equals(RibbonTabContent.ToString()))
					RibbonBox.SelectedItem = item;
			}
		}

		if (SortBox.SelectedItem is null)
		{
			foreach (ComboBoxItem item in SortBox.Items)
			{
				if (item.Tag.Equals(RecentEntriesSortMode.ToString()))
					SortBox.SelectedItem = item;
			}
		}
	}

	private void SortRibbonChanged(object? sender, SelectionChangedEventArgs e)
	{
		var box = (ComboBox?)sender;
		var item = (ComboBoxItem?)box?.SelectedItem;

		var tag = new EnumConverter(typeof(SortType)).ConvertFromString((string?)item?.Tag ?? "ByChange") as SortType?;

		RecentEntriesSortMode = tag ?? SortType.ByChange;
		RecentNotesDirty = true;
		DeferUpdateRecentNotes();
	}

	private void StickyRibbonChanged(object? sender, SelectionChangedEventArgs e)
	{
		var box = (ComboBox?)sender;
		var item = (ComboBoxItem?)box?.SelectedItem;

		var tag = new EnumConverter(typeof(DisplayType)).ConvertFromString((string?)item?.Tag ?? "Content") as DisplayType?;
		RibbonTabContent = tag ?? DisplayType.Content;

		UpdateRibbonTabs();
	}
}
