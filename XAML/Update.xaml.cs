using SylverInk.Net;
using System;
using System.Windows;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Update.xaml
/// </summary>
public partial class Update : Window
{
	private DateTime lastUpdate;

	public Update()
	{
		InitializeComponent();
		DataContext = CommonUtils.Settings;
		lastUpdate = DateTime.UtcNow;
	}

	public void ReportProgress(double percentage)
	{
		if ((DateTime.UtcNow - lastUpdate).Milliseconds <= 50)
			return;

		UpdateProgress.Value = percentage;
		lastUpdate = DateTime.UtcNow;
	}

	private void UpdateCancel(object? sender, RoutedEventArgs e) => UpdateHandler.CancelUpdate();
}
