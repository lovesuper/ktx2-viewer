using KtxViewer.UI.ViewModels;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Constrain the maximized borderless window to the monitor work area so it
        // does not spill past the screen edges or cover the taskbar.
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);

        // Allow drag-and-drop from a normal (non-elevated) Explorer even when this
        // process runs elevated. Without this, UIPI silently blocks the drop messages,
        // so D&D appears to "do nothing" when the app is launched as Administrator.
        ChangeWindowMessageFilterEx(handle, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(handle, WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(handle, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);
    }

    private void FileListResizer_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var newWidth = viewModel.FileListWidth + e.HorizontalChange;
        viewModel.FileListWidth = Math.Clamp(newWidth, MainViewModel.FileListMinWidth, MainViewModel.FileListMaxWidth);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        switch (e.Key)
        {
            case Key.Left:
                if (viewModel.PreviousImageCommand.CanExecute(null))
                    viewModel.PreviousImageCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right:
                if (viewModel.NextImageCommand.CanExecute(null))
                    viewModel.NextImageCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private static string[] GetDroppedFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return System.Array.Empty<string>();

        return e.Data.GetData(DataFormats.FileDrop) as string[] ?? System.Array.Empty<string>();
    }

    private static bool TryGetDroppedKtxFiles(DragEventArgs e, out string[] files)
    {
        files = GetDroppedFiles(e).Where(MainViewModel.IsSupportedFile).ToArray();
        return files.Length > 0;
    }

    private static bool TryGetDroppedMapFile(DragEventArgs e, out string file)
    {
        file = GetDroppedFiles(e)
            .FirstOrDefault(f => System.IO.Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
        return file.Length > 0;
    }

    private void MainBorder_DragEnter(object sender, DragEventArgs e)
    {
        if (TryGetDroppedKtxFiles(e, out _) || TryGetDroppedMapFile(e, out _))
        {
            DropOverlay.Visibility = Visibility.Visible;
        }
    }

    private void MainBorder_DragOver(object sender, DragEventArgs e)
    {
        var accepts = TryGetDroppedKtxFiles(e, out _) || TryGetDroppedMapFile(e, out _);
        e.Effects = accepts ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MainBorder_DragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void MainBorder_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;

        if (DataContext is not MainViewModel viewModel)
            return;

        // KTX textures take priority; a .json drop is treated as a sprite map for the current image.
        if (TryGetDroppedKtxFiles(e, out var files))
        {
            await viewModel.LoadFilesAsync(files);
        }
        else if (TryGetDroppedMapFile(e, out var mapFile))
        {
            viewModel.LoadMap(mapFile);
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            // The window size is already constrained to the work area via WM_GETMINMAXINFO,
            // so it is enough to drop the corner radius, border and outer margin.
            MainBorder.CornerRadius = new CornerRadius(0);
            MainBorder.Margin = new Thickness(0);
            MainBorder.BorderThickness = new Thickness(0);
            RestoreIcon.Visibility = Visibility.Visible;
            MaximizeIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            MainBorder.CornerRadius = new CornerRadius(8);
            MainBorder.Margin = new Thickness(0);
            MainBorder.BorderThickness = new Thickness(1);
            RestoreIcon.Visibility = Visibility.Collapsed;
            MaximizeIcon.Visibility = Visibility.Visible;
        }
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var work = monitorInfo.rcWork;
                var bounds = monitorInfo.rcMonitor;

                // Position and size are relative to the monitor, in physical pixels.
                mmi.ptMaxPosition.X = work.Left - bounds.Left;
                mmi.ptMaxPosition.Y = work.Top - bounds.Top;
                mmi.ptMaxSize.X = work.Right - work.Left;
                mmi.ptMaxSize.Y = work.Bottom - work.Top;
            }
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    // UIPI message filter: lets drag-and-drop messages through when the app is elevated.
    private const uint MSGFLT_ALLOW = 1;
    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_COPYGLOBALDATA = 0x0049;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, IntPtr pChangeFilterStruct);

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
