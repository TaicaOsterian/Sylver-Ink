namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Help.xaml
/// </summary>
public partial class About : Window
{
    public AboutViewModel ViewModel => (AboutViewModel)DataContext;

    public About()
    {
        DataContext = new AboutViewModel();
        ViewModel.RequestClose += (_, _) => Close();
        InitializeComponent();
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

    private void FollowHyperlink(object? sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
