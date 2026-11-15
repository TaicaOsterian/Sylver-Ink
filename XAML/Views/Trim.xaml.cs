namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Trim.xaml
/// </summary>
public sealed partial class Trim : Window
{
    internal TrimViewModel ViewModel => (TrimViewModel)DataContext;

    public Trim()
    {
        DataContext = new TrimViewModel();
        ViewModel.RequestClose += (_, _) => Close();
        InitializeComponent();
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

    private void Trim_Loaded(object? sender, RoutedEventArgs e)
    {

    }
}
