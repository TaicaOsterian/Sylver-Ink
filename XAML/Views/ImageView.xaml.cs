namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for ImageView.xaml
/// </summary>
public partial class ImageView : Popout
{
    private Point dragStartPoint;
    private Matrix dragMatrix;
    private UIElement? element;
    private Matrix home = Matrix.Identity;
    private Image? image;
    private Control? parentControl;
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
        image = FindImage(canvas);

        DependencyObject d = element;
        while (d != null)
        {
            d = VisualTreeHelper.GetParent(d);
            if (d is not Control control)
                continue;

            parentControl = control;
            parentControl.Background ??= Brushes.Transparent;
            parentControl.MouseWheel += Control_MouseWheel;
            parentControl.MouseLeftButtonDown += Control_MouseLeftButtonDown;
            break;
        }

        UpdateHome();
    }

    private void Canvas_ZoomClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (parentControl is null)
            return;

        double scaleDelta;
        switch (button.Content)
        {
            case "+":
                scaleDelta = 1.15;
                break;
            case "-":
                scaleDelta = 0.85;
                break;
            default:
                UpdateHome();
                return;
        }

        MatrixTransform mt = GetMatrixTransform();
        Matrix matrix = mt.Matrix;

        double centerX = parentControl.ActualWidth * 0.5;
        double centerY = parentControl.ActualHeight * 0.5;

        matrix.ScaleAt(scaleDelta, scaleDelta, centerX, centerY);
        mt.Matrix = matrix;
        ViewModel.ZoomLevel *= scaleDelta;

        e.Handled = true;
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
        c.MouseLeftButtonUp -= Control_MouseLeftButtonUp;

        e.Handled = true;
    }

    private void Control_MouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not Control c)
            return;

        MatrixTransform mt = GetMatrixTransform();
        var matrix = dragMatrix;

        var p2 = e.GetPosition(c);
        var x = p2.X - dragStartPoint.X;
        var y = p2.Y - dragStartPoint.Y;
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
        ViewModel.ZoomLevel *= scaleDelta;

        e.Handled = true;
    }

    private static Image? FindImage(DependencyObject root)
    {
        if (root is Image img)
            return img;

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FindImage(VisualTreeHelper.GetChild(root, i));
            if (found is not null)
                return found;
        }
        return null;
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

    private void UpdateHome()
    {
        if (parentControl is null || image is null)
            return;

        double dx = (parentControl.ActualWidth - image.ActualWidth) * 0.5;
        double dy = (parentControl.ActualHeight - image.ActualHeight) * 0.5;

        home = Matrix.Identity;
        home.Translate(dx, dy);

        GetMatrixTransform().Matrix = home;
        ViewModel.ZoomLevel = 1.0;

        return;
    }
}
