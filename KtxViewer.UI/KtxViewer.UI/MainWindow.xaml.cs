using KtxViewer.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KtxViewer.UI;

public partial class MainWindow : Window
{
    private Point? _lastDragPoint;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            MainBorder.CornerRadius = new CornerRadius(0);
            MainBorder.Margin = new Thickness(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.PrimaryScreenWidth - SystemParameters.WorkArea.Right,
                SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Bottom
            );
            MainBorder.BorderThickness = new Thickness(0);
        }
        else
        {
            MainBorder.CornerRadius = new CornerRadius(8);
            MainBorder.Margin = new Thickness(0);
            MainBorder.BorderThickness = new Thickness(1);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not ScrollViewer scrollViewer)
            return;

        var oldZoom = viewModel.ZoomLevel;
        var mousePos = e.GetPosition(scrollViewer);

        var absoluteX = scrollViewer.HorizontalOffset + mousePos.X;
        var absoluteY = scrollViewer.VerticalOffset + mousePos.Y;

        var delta = e.Delta > 0 ? 0.1 : -0.1;
        viewModel.AdjustZoom(delta);

        var newZoom = viewModel.ZoomLevel;

        if (Math.Abs(newZoom - oldZoom) > 0.001)
        {
            var newOffsetX = absoluteX * (newZoom / oldZoom) - mousePos.X;
            var newOffsetY = absoluteY * (newZoom / oldZoom) - mousePos.Y;

            scrollViewer.ScrollToHorizontalOffset(newOffsetX);
            scrollViewer.ScrollToVerticalOffset(newOffsetY);
        }

        e.Handled = true;
    }

    private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        _lastDragPoint = e.GetPosition(scrollViewer);
        scrollViewer.Cursor = Cursors.Hand;
        scrollViewer.CaptureMouse();
    }

    private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || !_lastDragPoint.HasValue)
            return;

        var currentPoint = e.GetPosition(scrollViewer);
        var delta = currentPoint - _lastDragPoint.Value;

        scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - delta.X);
        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta.Y);

        _lastDragPoint = currentPoint;
    }

    private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        _lastDragPoint = null;
        scrollViewer.Cursor = Cursors.Arrow;
        scrollViewer.ReleaseMouseCapture();
    }
}
