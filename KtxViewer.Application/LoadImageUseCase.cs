using KtxViewer.Core;

namespace KtxViewer.Application;

public sealed class LoadImageUseCase(IKtxLoader loader)
{
    private readonly IKtxLoader _loader = loader ?? throw new ArgumentNullException(nameof(loader));

    public async Task<KtxImage> ExecuteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        return await _loader.LoadAsync(filePath, cancellationToken);
    }
}
