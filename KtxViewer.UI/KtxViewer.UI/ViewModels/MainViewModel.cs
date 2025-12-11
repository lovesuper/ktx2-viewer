using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KtxViewer.Application;
using KtxViewer.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KtxViewer.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly LoadImageUseCase _loadImageUseCase;

    [ObservableProperty]
    private ImageSource? _currentImage;

    [ObservableProperty]
    private string? _imageInfo;

    [ObservableProperty]
    private string? _fileInfo;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    public MainViewModel(LoadImageUseCase loadImageUseCase)
    {
        _loadImageUseCase = loadImageUseCase ?? throw new ArgumentNullException(nameof(loadImageUseCase));
    }

    public void AdjustZoom(double delta)
    {
        var newZoom = ZoomLevel + delta;
        ZoomLevel = Math.Clamp(newZoom, 0.1, 10.0);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KTX2 Files (*.ktx2)|*.ktx2|All Files (*.*)|*.*",
            Title = "Open KTX2 Texture"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            IsLoading = true;
            var image = await _loadImageUseCase.ExecuteAsync(dialog.FileName);

            CurrentImage = ConvertToBitmap(image);
            ImageInfo = $"{image.Width}x{image.Height} | {image.Format} | {image.MipLevels} mip(s) | {image.LayerCount} layer(s)";

            var fileInfo = new FileInfo(dialog.FileName);
            var fileSizeKb = fileInfo.Length / 1024.0;
            var fileSizeMb = fileSizeKb / 1024.0;
            var sizeStr = fileSizeMb >= 1 ? $"{fileSizeMb:F2} MB" : $"{fileSizeKb:F2} KB";
            FileInfo = $"{fileInfo.Name} | {sizeStr}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading texture: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static BitmapSource ConvertToBitmap(KtxImage image)
    {
        var stride = image.Width * 4;
        var bitmap = BitmapSource.Create(
            image.Width,
            image.Height,
            96, 96,
            PixelFormats.Bgra32,
            null,
            ConvertRgbaToBgra(image.PixelData.Span),
            stride);

        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] ConvertRgbaToBgra(ReadOnlySpan<byte> rgba)
    {
        var bgra = new byte[rgba.Length];
        for (int i = 0; i < rgba.Length; i += 4)
        {
            bgra[i] = rgba[i + 2];     // B
            bgra[i + 1] = rgba[i + 1]; // G
            bgra[i + 2] = rgba[i];     // R
            bgra[i + 3] = rgba[i + 3]; // A
        }
        return bgra;
    }
}
