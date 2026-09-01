using SylverInk.XAML.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace SylverInk.XAML;

public partial class Import : Window
{
    public ImportViewModel ViewModel => (ImportViewModel)DataContext;

    public Import()
    {
        DataContext = new ImportViewModel();
        ViewModel.RequestClose += (_, _) => Close();
        InitializeComponent();
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();
}
