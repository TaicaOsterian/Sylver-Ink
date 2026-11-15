namespace SylverInk.XAML;

public partial class Import : Window
{
    internal ImportViewModel ViewModel => (ImportViewModel)DataContext;

    internal Import()
    {
        DataContext = new ImportViewModel();
        ViewModel.RequestClose += (_, _) => Close();
        InitializeComponent();
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();
}
