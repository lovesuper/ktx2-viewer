using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KtxViewer.Application;
using KtxViewer.Core;
using KtxViewer.UI.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KtxViewer.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly LoadImageUseCase _loadImageUseCase;
    private string? _currentFilePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAsPngCommand))]
    private ImageSource? _currentImage;

    [ObservableProperty]
    private string? _imageInfo;

    [ObservableProperty]
    private string? _fileInfo;

    [ObservableProperty]
    private string? _detailedInfo;

    [ObservableProperty]
    private string? _alphaInfo;

    [ObservableProperty]
    private bool _isInfoVisible;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private double _loadProgress;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private bool _isBorderVisible = true;

    /// <summary>Current list of opened files that the user can page through.</summary>
    public ObservableCollection<string> Playlist { get; } = new();

    [ObservableProperty]
    private string? _navigationInfo;

    /// <summary>True when more than one image is open (drives the navigation control and the list).</summary>
    [ObservableProperty]
    private bool _isMultiImage;

    /// <summary>User toggle for the file-list panel; combined with <see cref="IsMultiImage"/>.</summary>
    [ObservableProperty]
    private bool _isFileListShown = true;

    /// <summary>Effective visibility of the file-list panel (multiple images AND user kept it shown).</summary>
    [ObservableProperty]
    private bool _isFileListVisible;

    /// <summary>Width of the file-list panel; resizable within [<see cref="FileListMinWidth"/>, <see cref="FileListMaxWidth"/>].</summary>
    [ObservableProperty]
    private double _fileListWidth = FileListDefaultWidth;

    public const double FileListDefaultWidth = 220;
    public const double FileListMinWidth = FileListDefaultWidth - 100;
    public const double FileListMaxWidth = FileListDefaultWidth + 100;

    /// <summary>Selected item in the list overlay; two-way bound to the ListBox.</summary>
    [ObservableProperty]
    private int _selectedListIndex = -1;

    private int _currentIndex = -1;

    partial void OnSelectedListIndexChanged(int value)
    {
        // Only react to user selection; programmatic sync (value == _currentIndex) and
        // invalid values are ignored to avoid a feedback loop.
        if (value < 0 || value >= Playlist.Count || value == _currentIndex)
        {
            return;
        }

        _currentIndex = value;
        _ = LoadCurrentAsync();
    }

    partial void OnIsFileListShownChanged(bool value) => UpdateFileListVisibility();

    private void UpdateFileListVisibility()
    {
        IsFileListVisible = IsMultiImage && IsFileListShown;
    }

    // --- Spritesheet map overlay -------------------------------------------

    /// <summary>True when a sprite map has been loaded for the current image.</summary>
    [ObservableProperty]
    private bool _isMapLoaded;

    /// <summary>User toggle for the spritesheet-map view (defaults to on when a map loads).</summary>
    [ObservableProperty]
    private bool _isSpritesheetMapVisible;

    /// <summary>Effective visibility of the map overlay (map loaded AND view enabled).</summary>
    [ObservableProperty]
    private bool _isMapOverlayVisible;

    /// <summary>Short status line describing the loaded map.</summary>
    [ObservableProperty]
    private string? _mapInfo;

    /// <summary>Geometry of all frame rectangles in image-pixel coordinates.</summary>
    [ObservableProperty]
    private Geometry? _mapGeometry;

    private SpriteSheetMap? _currentMap;
    private int _currentImageWidth;
    private int _currentImageHeight;

    partial void OnIsMapLoadedChanged(bool value) => UpdateMapOverlayVisibility();

    partial void OnIsSpritesheetMapVisibleChanged(bool value) => UpdateMapOverlayVisibility();

    private void UpdateMapOverlayVisibility()
    {
        IsMapOverlayVisible = IsMapLoaded && IsSpritesheetMapVisible;
    }

    [RelayCommand]
    private void ToggleSpritesheetMap()
    {
        IsSpritesheetMapVisible = !IsSpritesheetMapVisible;
    }

    /// <summary>
    /// Loads a sprite-map JSON file and attaches it to the currently displayed image.
    /// Shows a detailed error if the file does not match the expected structure.
    /// </summary>
    public void LoadMap(string jsonPath)
    {
        if (CurrentImage is null)
        {
            MessageBox.Show(
                "Open a texture first, then drop a .json map onto it.",
                "No image loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(jsonPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not read the map file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!SpriteSheetMap.TryParse(json, out var map, out var error))
        {
            MessageBox.Show(
                $"'{Path.GetFileName(jsonPath)}' is not a valid spritesheet map.\n\n{error}",
                "Invalid map file", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!MapFitsImage(map!, out var fitError))
        {
            MessageBox.Show(
                $"'{Path.GetFileName(jsonPath)}' does not match the current image.\n\n{fitError}",
                "Map does not match image", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _currentMap = map;
        BuildMapGeometry();
        IsMapLoaded = true;
        IsSpritesheetMapVisible = true; // enabled by default once a map is loaded

        var sizeNote = (map!.SheetWidth != _currentImageWidth || map.SheetHeight != _currentImageHeight)
            ? $" (sheet {map.SheetWidth}x{map.SheetHeight}, scaled to image)"
            : string.Empty;
        MapInfo = $"Map: {map.Frames.Count} frames{sizeNote}";
    }

    /// <summary>
    /// Checks that the map actually belongs to the current image. A correct map scales
    /// uniformly onto the texture (same factor on both axes), so mismatched proportions
    /// mean it was built for a different spritesheet and its lines would not line up.
    /// </summary>
    private bool MapFitsImage(SpriteSheetMap map, out string error)
    {
        error = string.Empty;

        if (_currentImageWidth <= 0 || _currentImageHeight <= 0)
        {
            return true; // unknown image size - cannot judge, don't block
        }

        double scaleX = (double)_currentImageWidth / map.SheetWidth;
        double scaleY = (double)_currentImageHeight / map.SheetHeight;
        double relativeDifference = Math.Abs(scaleX - scaleY) / Math.Max(scaleX, scaleY);

        const double tolerance = 0.02; // 2% - allows for rounding and 1x/2x exports
        if (relativeDifference <= tolerance)
        {
            return true;
        }

        double sheetAspect = (double)map.SheetWidth / map.SheetHeight;
        double imageAspect = (double)_currentImageWidth / _currentImageHeight;
        var imageRef = string.IsNullOrEmpty(map.ImageName)
            ? string.Empty
            : $"\nThe map was built for '{map.ImageName}'.";

        error =
            $"The map's sheet size is {map.SheetWidth}x{map.SheetHeight} (aspect {sheetAspect:F3}), " +
            $"but the image is {_currentImageWidth}x{_currentImageHeight} (aspect {imageAspect:F3}).\n\n" +
            $"The proportions differ by {relativeDifference * 100:F1}%, so the frame boundaries would not line up. " +
            $"This map is most likely for a different texture.{imageRef}";
        return false;
    }

    private void ClearMap()
    {
        _currentMap = null;
        MapGeometry = null;
        MapInfo = null;
        IsMapLoaded = false;
        IsSpritesheetMapVisible = false;
    }

    private void BuildMapGeometry()
    {
        if (_currentMap is null || _currentImageWidth <= 0 || _currentImageHeight <= 0)
        {
            MapGeometry = null;
            return;
        }

        // Frame coordinates are in sheet (meta.size) space; scale them to the actual image pixels.
        double scaleX = (double)_currentImageWidth / _currentMap.SheetWidth;
        double scaleY = (double)_currentImageHeight / _currentMap.SheetHeight;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var frame in _currentMap.Frames)
            {
                double x = frame.X * scaleX;
                double y = frame.Y * scaleY;
                double w = frame.W * scaleX;
                double h = frame.H * scaleY;

                ctx.BeginFigure(new Point(x, y), isFilled: false, isClosed: true);
                ctx.LineTo(new Point(x + w, y), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(x + w, y + h), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(x, y + h), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        MapGeometry = geometry;
    }

    private static readonly string[] SupportedExtensions = { ".ktx", ".ktx2" };

    public MainViewModel(LoadImageUseCase loadImageUseCase)
    {
        _loadImageUseCase = loadImageUseCase ?? throw new ArgumentNullException(nameof(loadImageUseCase));
    }

    public static bool IsSupportedFile(string path)
    {
        var ext = Path.GetExtension(path);
        return SupportedExtensions.Any(e => ext.Equals(e, StringComparison.OrdinalIgnoreCase));
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
    private void ToggleInfo()
    {
        IsInfoVisible = !IsInfoVisible;
    }

    [RelayCommand]
    private void ToggleFileList()
    {
        IsFileListShown = !IsFileListShown;
    }

    /// <summary>
    /// Loads a set of files into the playlist and shows the first one.
    /// Unsupported extensions and duplicates are filtered out.
    /// </summary>
    public async Task LoadFilesAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var valid = paths
            .Where(p => !string.IsNullOrWhiteSpace(p) && IsSupportedFile(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (valid.Count == 0)
        {
            return;
        }

        Playlist.Clear();
        foreach (var p in valid)
        {
            Playlist.Add(p);
        }

        _currentIndex = 0;
        await LoadCurrentAsync(cancellationToken);
    }

    private async Task LoadCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_currentIndex < 0 || _currentIndex >= Playlist.Count)
        {
            return;
        }

        UpdateNavigationState();
        await LoadFileAsync(Playlist[_currentIndex], cancellationToken);
    }

    private void UpdateNavigationState()
    {
        NavigationInfo = Playlist.Count > 0 ? $"{_currentIndex + 1} / {Playlist.Count}" : null;
        IsMultiImage = Playlist.Count > 1;
        UpdateFileListVisibility();
        SelectedListIndex = _currentIndex;
        NextImageCommand.NotifyCanExecuteChanged();
        PreviousImageCommand.NotifyCanExecuteChanged();
    }

    private bool CanGoNext() => _currentIndex >= 0 && _currentIndex < Playlist.Count - 1;

    private bool CanGoPrevious() => _currentIndex > 0;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextImage(CancellationToken cancellationToken = default)
    {
        if (!CanGoNext())
        {
            return;
        }

        _currentIndex++;
        await LoadCurrentAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousImage(CancellationToken cancellationToken = default)
    {
        if (!CanGoPrevious())
        {
            return;
        }

        _currentIndex--;
        await LoadCurrentAsync(cancellationToken);
    }

    public async Task LoadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            IsLoading = true;
            LoadProgress = 0;

            var progress = new Progress<double>(value =>
            {
                LoadProgress = Math.Min(90, value * 0.9);
            });

            var image = await _loadImageUseCase.ExecuteAsync(filePath, cancellationToken, progress);

            LoadProgress = 95;
            await Task.Yield();

            // A new image invalidates any previously loaded sprite map.
            ClearMap();

            CurrentImage = ConvertToBitmap(image);
            _currentFilePath = filePath;
            _currentImageWidth = image.Width;
            _currentImageHeight = image.Height;
            LoadProgress = 100;

            ImageInfo = $"{image.Width}x{image.Height} | {image.Format} | {image.MipLevels} mip(s) | {image.LayerCount} layer(s)";

            var alpha = AnalyzeAlpha(image.PixelData.Span);
            AlphaInfo = $"Coverage: {alpha.Coverage:F1}% | Empty: {alpha.Transparent:F1}%";

            var fileInfo = new FileInfo(filePath);
            var fileSizeKb = fileInfo.Length / 1024.0;
            var fileSizeMb = fileSizeKb / 1024.0;
            var sizeStr = fileSizeMb >= 1 ? $"{fileSizeMb:F2} MB" : $"{fileSizeKb:F2} KB";
            FileInfo = $"{fileInfo.Name} | {sizeStr}";

            if (image.Metadata != null)
            {
                var md = image.Metadata;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Resolution: {md.PixelWidth}x{md.PixelHeight}x{md.PixelDepth}");

                if (md.VkFormat > 0)
                {
                    sb.AppendLine($"Format: {md.VkFormat} (Vulkan ID)");
                }

                sb.AppendLine($"Type Size: {md.TypeSize}");
                sb.AppendLine($"Levels: {md.LevelCount}");
                sb.AppendLine($"Layers: {md.LayerCount}");
                sb.AppendLine($"Faces: {md.FaceCount}");

                if (md.SupercompressionScheme > 0)
                {
                    sb.AppendLine($"Supercompression: {GetSupercompressionName(md.SupercompressionScheme)}");
                }

                sb.AppendLine($"Color Model: {md.ColorModel}");
                sb.AppendLine($"Color Primaries: {md.ColorPrimaries}");
                sb.AppendLine($"Transfer Function: {md.TransferFunction}");

                AppendAlphaSection(sb, alpha);

                if (md.KeyValuePairs.Count > 0)
                {
                    sb.AppendLine("\nMetadata:");
                    foreach (var kvp in md.KeyValuePairs)
                    {
                        sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }

                DetailedInfo = sb.ToString();
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("No metadata available");
                AppendAlphaSection(sb, alpha);
                DetailedInfo = sb.ToString();
            }
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

    [RelayCommand]
    private async Task OpenFileAsync(CancellationToken cancellationToken = default)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "KTX Files (*.ktx;*.ktx2)|*.ktx;*.ktx2|KTX2 Files (*.ktx2)|*.ktx2|KTX Files (*.ktx)|*.ktx|All Files (*.*)|*.*",
            Title = "Open KTX Texture",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadFilesAsync(dialog.FileNames, cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSavePng))]
    private void SaveAsPng()
    {
        if (CurrentImage is not BitmapSource bitmapSource)
        {
            return;
        }

        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                Title = "Save as PNG",
                DefaultExt = ".png"
            };

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_currentFilePath);
                dialog.FileName = $"{fileNameWithoutExtension}.png";
            }

            if (dialog.ShowDialog() == true)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                using var fileStream = new FileStream(dialog.FileName, FileMode.Create);
                encoder.Save(fileStream);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanSavePng() => CurrentImage != null;

    private string GetSupercompressionName(uint scheme)
    {
        return scheme switch
        {
            0 => "None",
            1 => "BasisLZ",
            2 => "Zstandard",
            3 => "ZLIB",
            _ => $"Unknown ({scheme})"
        };
    }

    /// <summary>
    /// Alpha-channel analysis of an image (as a percentage of all pixels).
    /// Used to gauge sprite-sheet packing quality: the higher the Coverage
    /// and the lower the Transparent share, the tighter the packing.
    /// </summary>
    private readonly record struct AlphaStats(
        double Coverage,      // pixels with alpha > 0 (useful area occupied by sprites)
        double Transparent,   // fully transparent pixels (alpha == 0, empty space)
        double Opaque,        // fully opaque pixels (alpha == 255)
        double SemiTransparent); // partially transparent (0 < alpha < 255, edges/soft alpha)

    /// <summary>
    /// Computes the alpha-value distribution over RGBA8 data in a single pass.
    /// </summary>
    private static AlphaStats AnalyzeAlpha(ReadOnlySpan<byte> rgba)
    {
        long total = rgba.Length / 4;
        if (total == 0)
        {
            return new AlphaStats(0, 0, 0, 0);
        }

        long transparent = 0;
        long opaque = 0;
        long semi = 0;

        for (int i = 3; i < rgba.Length; i += 4)
        {
            byte a = rgba[i];
            if (a == 0)
            {
                transparent++;
            }
            else if (a == 255)
            {
                opaque++;
            }
            else
            {
                semi++;
            }
        }

        double pct = 100.0 / total;
        long coverage = opaque + semi;
        return new AlphaStats(
            coverage * pct,
            transparent * pct,
            opaque * pct,
            semi * pct);
    }

    private static void AppendAlphaSection(System.Text.StringBuilder sb, AlphaStats alpha)
    {
        sb.AppendLine("\nAlpha analysis (sprite packing):");
        sb.AppendLine($"  Coverage (alpha > 0): {alpha.Coverage:F2}%");
        sb.AppendLine($"  Empty space (alpha = 0): {alpha.Transparent:F2}%");
        sb.AppendLine($"  Opaque (alpha = 255): {alpha.Opaque:F2}%");
        sb.AppendLine($"  Semi-transparent (0 < alpha < 255): {alpha.SemiTransparent:F2}%");
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
