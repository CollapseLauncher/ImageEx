using Windows.Storage.Streams;

#nullable enable
namespace ImageEx;

internal static class ImageSourceUtility
{
    public static async ValueTask<ImageSource?> TryGetEmbeddedImageSourceFromUri(
        Uri               uri,
        CancellationToken token = default)
    {
        if (!StringUtility.TryGetStreamFromUrlDataString(uri,
                                                         out string? mimeType,
                                                         out MemoryStream? stream))
        {
            return null;
        }

        using (stream)
        {
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
