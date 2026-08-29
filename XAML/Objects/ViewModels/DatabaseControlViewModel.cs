using SylverInk.XAMLUtils;
using System.Windows;
using System.Windows.Input;
using static SylverInk.CommonUtils;

namespace SylverInk.XAML.Objects.ViewModels;

public class DatabaseControlViewModel : ViewModelBase
{
	public ICommand ExitCommand { get; }
	public ICommand ImportCommand { get; }
	public ICommand SearchCommand { get; }
	public ICommand SettingsCommand { get; }

	public DatabaseControlViewModel()
	{
		ExitCommand = new RelayCommand(Exit);
		ImportCommand = new RelayCommand(Import);
		SearchCommand = new RelayCommand(Search);
		SettingsCommand = new RelayCommand(Settings);
	}

	private void Exit(object? param) => Application.Current.MainWindow.Close();

	private void Import(object? param) => ImportWindow = new();

	private void Search(object? param) => SearchWindow = new();

	private void Settings(object? param) => SettingsWindow = new();
}
