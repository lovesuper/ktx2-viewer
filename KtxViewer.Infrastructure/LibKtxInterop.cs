using System.Runtime.InteropServices;

namespace KtxViewer.Infrastructure;

internal static unsafe class LibKtxInterop
{
    private const string LibraryName = "ktx";

    #region Enums

    public enum KtxErrorCode
    {
        KTX_SUCCESS = 0,
        KTX_FILE_DATA_ERROR,
        KTX_FILE_ISPIPE,
        KTX_FILE_OPEN_FAILED,
        KTX_FILE_OVERFLOW,
        KTX_FILE_READ_ERROR,
        KTX_FILE_SEEK_ERROR,
        KTX_FILE_UNEXPECTED_EOF,
        KTX_FILE_WRITE_ERROR,
        KTX_GL_ERROR,
        KTX_INVALID_OPERATION,
        KTX_INVALID_VALUE,
        KTX_NOT_FOUND,
        KTX_OUT_OF_MEMORY,
        KTX_TRANSCODE_FAILED,
        KTX_UNKNOWN_FILE_FORMAT,
        KTX_UNSUPPORTED_TEXTURE_TYPE,
        KTX_UNSUPPORTED_FEATURE,
        KTX_LIBRARY_NOT_LINKED,
        KTX_DECOMPRESS_LENGTH_ERROR,
        KTX_DECOMPRESS_CHECKSUM_ERROR
    }

    public enum KtxTranscodeFmt
    {
        KTX_TTF_ETC1_RGB = 0,
        KTX_TTF_ETC2_RGBA = 1,
        KTX_TTF_BC1_RGB = 2,
        KTX_TTF_BC3_RGBA = 3,
        KTX_TTF_BC4_R = 4,
        KTX_TTF_BC5_RG = 5,
        KTX_TTF_BC7_RGBA = 6,
        KTX_TTF_PVRTC1_4_RGB = 8,
        KTX_TTF_PVRTC1_4_RGBA = 9,
        KTX_TTF_ASTC_4x4_RGBA = 10,
        KTX_TTF_PVRTC2_4_RGB = 18,
        KTX_TTF_PVRTC2_4_RGBA = 19,
        KTX_TTF_ETC2_EAC_R11 = 20,
        KTX_TTF_ETC2_EAC_RG11 = 21,
        KTX_TTF_RGBA32 = 13,
        KTX_TTF_RGB565 = 14,
        KTX_TTF_BGR565 = 15,
        KTX_TTF_RGBA4444 = 16,
        KTX_TTF_ETC = 22,
        KTX_TTF_BC1_OR_3 = 23
    }

    [Flags]
    public enum KtxTranscodeFlags : uint
    {
        None = 0,
        KTX_TF_PVRTC_DECODE_TO_NEXT_POW2 = 2,
        KTX_TF_TRANSCODE_ALPHA_DATA_TO_OPAQUE_FORMATS = 4,
        KTX_TF_HIGH_QUALITY = 32
    }

    public enum KtxTextureCreateFlagBits
    {
        KTX_TEXTURE_CREATE_LOAD_IMAGE_DATA_BIT = 0x00000001,
        KTX_TEXTURE_CREATE_RAW_KVDATA_BIT = 0x00000002,
        KTX_TEXTURE_CREATE_SKIP_KVDATA_BIT = 0x00000004,
        KTX_TEXTURE_CREATE_CHECK_GLTF_BASISU_BIT = 0x00000008
    }

    #endregion

    #region Structs

    [StructLayout(LayoutKind.Sequential)]
    public struct ktxTexture2
    {
        public IntPtr classId;
        public IntPtr vtbl;
        public IntPtr vvtbl;
        public IntPtr _protected;
        public uint isArray;
        public uint isCubemap;
        public uint isCompressed;
        public uint generateMipmaps;
        public uint baseWidth;
        public uint baseHeight;
        public uint baseDepth;
        public uint numDimensions;
        public uint numLevels;
        public uint numLayers;
        public uint numFaces;
        public IntPtr pData;
        public ulong dataSize;
        public uint vkFormat;
        public IntPtr pDfd;
        public uint supercompressionScheme;
        public uint isVideo;
        public uint durationDenominator;
        public uint durationNumerator;
        public uint timescale;
        public uint thisFrame;
        public IntPtr _private;
    }

    #endregion

    #region Functions

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern KtxErrorCode ktxTexture2_CreateFromMemory(
        byte* bytes,
        ulong size,
        KtxTextureCreateFlagBits createFlags,
        out IntPtr newTex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern KtxErrorCode ktxTexture2_TranscodeBasis(
        IntPtr texture,
        KtxTranscodeFmt outputFormat,
        KtxTranscodeFlags transcodeFlags);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ktxTexture_GetData(IntPtr texture);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong ktxTexture_GetImageSize(
        IntPtr texture,
        uint level);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ktxTexture2_Destroy(IntPtr texture);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ktxErrorString(KtxErrorCode error);

    #endregion
}
