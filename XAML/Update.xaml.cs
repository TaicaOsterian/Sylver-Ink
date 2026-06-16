using SylverInk.Net;
using System;
using System.Windows;
using System.Windows.Input;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Update.xaml
/// </summary>
public partial class Update : Window
{
	private DateTime lastUpdate;

	public Update()
	{
		DataContext = CommonUtils.Settings;
		InitializeComponent();
		lastUpdate = DateTime.UtcNow;
	}

	private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

	public void ReportProgress(double percentage)
	{
		if ((DateTime.UtcNow - lastUpdate).Milliseconds <= 50)
			return;

		UpdateProgress.Value = percentage;
		lastUpdate = DateTime.UtcNow;
	}

	private void UpdateCancel(object? sender, RoutedEventArgs e) => UpdateHandler.CancelUpdate();
}
