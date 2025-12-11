using System.Runtime.InteropServices;
using KtxViewer.Core;

namespace KtxViewer.Infrastructure;

public sealed class LibKtxTranscoder : IDisposable
{
    private bool _isDisposed;
    private static Action<string>? _logger;

    public static void SetLogger(Action<string>? logger)
    {
        _logger = logger;
    }

    private static void Log(string message)
    {
        if (_logger != null)
            _logger(message);
        else
            System.Diagnostics.Debug.WriteLine(message);
    }

    public unsafe byte[]? TryTranscode(ReadOnlySpan<byte> ktx2Data, uint width, uint height)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(LibKtxTranscoder));

        Log($"LibKtx: Starting transcode for {width}x{height}, data size: {ktx2Data.Length} bytes");

        IntPtr texturePtr = IntPtr.Zero;

        try
        {
            fixed (byte* dataPtr = ktx2Data)
            {
                Log("LibKtx: Calling ktxTexture2_CreateFromMemory...");
                var result = LibKtxInterop.ktxTexture2_CreateFromMemory(
                    dataPtr,
                    (ulong)ktx2Data.Length,
                    LibKtxInterop.KtxTextureCreateFlagBits.KTX_TEXTURE_CREATE_LOAD_IMAGE_DATA_BIT,
                    out texturePtr);

                Log($"LibKtx: CreateFromMemory result: {result}");

                if (result != LibKtxInterop.KtxErrorCode.KTX_SUCCESS)
                {
                    Log($"LibKtx: CreateFromMemory FAILED with {result}");
                    return null;
                }
            }

            if (texturePtr == IntPtr.Zero)
            {
                Log("LibKtx: Texture pointer is null");
                return null;
            }

            Log($"LibKtx: Texture created successfully, ptr=0x{texturePtr.ToInt64():X}");

            Log("LibKtx: Calling TranscodeBasis to RGBA32...");
            var transcodeResult = LibKtxInterop.ktxTexture2_TranscodeBasis(
                texturePtr,
                LibKtxInterop.KtxTranscodeFmt.KTX_TTF_RGBA32,
                LibKtxInterop.KtxTranscodeFlags.KTX_TF_HIGH_QUALITY);

            Log($"LibKtx: TranscodeBasis result: {transcodeResult}");

            if (transcodeResult != LibKtxInterop.KtxErrorCode.KTX_SUCCESS)
            {
                Log($"LibKtx: TranscodeBasis FAILED with {transcodeResult}");
                return null;
            }

            Log("LibKtx: Transcoding successful!");

            ulong imageSize = (ulong)(width * height * 4);
            Log($"LibKtx: RGBA32 size = {width}x{height}x4 = {imageSize} bytes");

            IntPtr imageDataPtr = LibKtxInterop.ktxTexture_GetData(texturePtr);

            if (imageDataPtr == IntPtr.Zero)
            {
                Log("LibKtx: GetData returned null pointer");
                return null;
            }

            Log($"LibKtx: Data pointer=0x{imageDataPtr.ToInt64():X}");

            byte[] pixelData = new byte[imageSize];
            Marshal.Copy(imageDataPtr, pixelData, 0, (int)imageSize);

            Log($"LibKtx: Successfully copied {pixelData.Length} bytes");

            return pixelData;
        }
        catch (DllNotFoundException ex)
        {
            Log($"LibKtx: DLL not found - {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Log($"LibKtx: Exception - {ex.GetType().Name}: {ex.Message}");
            Log($"   Stack: {ex.StackTrace}");
            return null;
        }
        finally
        {
            if (texturePtr != IntPtr.Zero)
            {
                LibKtxInterop.ktxTexture2_Destroy(texturePtr);
                Log("LibKtx: Texture destroyed");
            }
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
        }
    }
}
