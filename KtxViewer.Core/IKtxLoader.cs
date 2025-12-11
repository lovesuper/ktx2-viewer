namespace KtxViewer.Core;

public interface IKtxLoader
{
    Task<KtxImage> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
