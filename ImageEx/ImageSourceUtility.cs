using System.Text;
using Windows.Storage.Streams;

#nullable enable
namespace ImageEx;

internal static class ImageSourceUtility
{
    private static void TryGetSvgMime(MemoryStream stream, ref string? mimeType)
    {
        if (!string.IsNullOrEmpty(mimeType))
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[128];
        buffer = buffer[..stream.Read(buffer)];
        int indexOfSvg = buffer.IndexOf("<"u8);

        stream.Position = 0;
        if (indexOfSvg < 0 &&
            indexOfSvg + 1 >= buffer.Length)
        {
            return;
        }

        buffer = buffer[(indexOfSvg + 1)..].Trim((byte)0x20);
        if (buffer.Length < 3)
        {
            return;
        }

        Span<char> svgSig = stackalloc char[3];
        Encoding.UTF8.TryGetChars(buffer[..3], svgSig, out _);

        ReadOnlySpan<char> svgSigRo = svgSig;
        if (svgSigRo.Equals("svg", StringComparison.OrdinalIgnoreCase))
        {
            mimeType = "image/svg";
        }
    }

    public static async ValueTask<ImageSource?> TryGetEmbeddedImageSourceFromUri(
        string            uriString,
        CancellationToken token = default)
    {
        if (!StringUtility.TryGetStreamFromUrlDataString(uriString,
                                                         out string? mimeType,
                                                         out MemoryStream? stream))
        {
            return null;
        }

        using (stream)
        {
            TryGetSvgMime(stream, ref mimeType);
            using IRandomAccessStream randomAccessStream = stream.AsRandomAccessStream();

            ImageSource imageSource;
            if (!string.IsNullOrEmpty(mimeType) &&
                mimeType.Contains("svg", StringComparison.OrdinalIgnoreCase))
            {
                imageSource = new SvgImageSource();
                await ((SvgImageSource)imageSource).SetSourceAsync(randomAccessStream);
            }
            else
            {
                imageSource = new BitmapImage();
                await ((BitmapImage)imageSource).SetSourceAsync(randomAccessStream);
            }

            return imageSource;
        }
    }
}
