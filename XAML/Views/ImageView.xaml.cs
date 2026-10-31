namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for ImageView.xaml
/// </summary>
public partial class ImageView : Popout
{
    private UIElement? element;
    private Point dragStartPoint;
    private Matrix dragMatrix;
    public override ImageViewViewModel ViewModel => (ImageViewViewModel)ViewModelProperty;

    public ImageView()
    {
        DataContext = new ImageViewViewModel();
        base.Construct((ImageViewViewModel)DataContext);
        ViewModel.RequestClose += (_, _) => HandleClose();
        InitializeComponent();
    }

    private void Canvas_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Canvas canvas)
            return;

        element = canvas;
        DependencyObject d = element;
        while (d != null)
        {
            d = VisualTreeHelper.GetParent(d);
            if (d is not Control control)
                continue;

            control.Background ??= Brushes.Transparent;
            control.MouseWheel += Control_MouseWheel;
            control.MouseLeftButtonDown += Control_MouseLeftButtonDown;
            break;
        }
    }

    public void Construct(ImageSource source)
    {
        ViewModel.Source = source;
        OpenImages.Add(this);
    }

    private void Control_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (sender is not Control c)
            return;

        if (!c.CaptureMouse())
            return;

        dragStartPoint = e.GetPosition(c);
        dragMatrix = GetMatrixTransform().Matrix;

        c.MouseMove += Control_MouseMove;
        c.MouseLeftButtonUp += Control_MouseLeftButtonUp;

        e.Handled = true;
    }

    private void Control_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
    {
        if (sender is not Control c)
            return;

        c.ReleaseMouseCapture();
        c.MouseMove -= Control_MouseMove;
        c.MouseUp -= Control_MouseLeftButtonUp;

        e.Handled = true;
    }

    private void Control_MouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not Control c)
            return;

        MatrixTransform mt = GetMatrixTransform();
        var matrix = dragMatrix;

        var p2 = e.GetPosition(c);
        var x = (p2.X - dragStartPoint.X) * matrix.M11;
        var y = (p2.Y - dragStartPoint.Y) * matrix.M11;
        matrix.Translate(x, y);
        mt.Matrix = matrix;

        e.Handled = true;
    }

    private void Control_MouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (sender is not Control c)
            return;

        if (c.IsMouseCaptured)
            return;

        MatrixTransform mt = GetMatrixTransform();

        var p = e.GetPosition(element);
        var matrix = mt.Matrix;

        double scaleCurrent = matrix.M11;
        double scaleDelta = (e.Delta > 0) ? 1.05 : 0.95;

        var p2 = matrix.Transform(new Point(0, 0));
        var centerX = scaleCurrent * p.X + p2.X;
        var centerY = scaleCurrent * p.Y + p2.Y;

        matrix.ScaleAt(scaleDelta, scaleDelta, centerX, centerY);
        mt.Matrix = matrix;

        e.Handled = true;
    }

    private MatrixTransform GetMatrixTransform()
    {
        if (element is null)
            return new MatrixTransform();

        if (element.RenderTransform is not MatrixTransform mt || mt.IsFrozen)
        {
            mt = new MatrixTransform();
            element.RenderTransform = mt;
        }

        return mt;
    }

    private void HandleClose()
    {
        OpenImages.Remove(this);
        Close();
    }
}
