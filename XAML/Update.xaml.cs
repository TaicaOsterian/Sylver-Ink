using SylverInk.XAML.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Update.xaml
/// </summary>
public partial class Update : Window
{
	public UpdateViewModel ViewModel => (UpdateViewModel)DataContext;

	public Update()
	{
		DataContext = new UpdateViewModel();
		InitializeComponent();
	}

	private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();
}
