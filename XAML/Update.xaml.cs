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
	private double progress;

	public Update()
	{
		InitializeComponent();
		lastUpdate = DateTime.Now;
	}

	public void ReportProgress(double percentage)
	{
		progress = percentage;
		
		if ((DateTime.Now - lastUpdate).Milliseconds > 50)
		{
			UpdateProgress.Value = percentage;
			lastUpdate = DateTime.Now;
		}
	}

	private void UpdateCancel(object? sender, RoutedEventArgs e)
	{
		UpdateHandler.CancelUpdate();
	}
}
