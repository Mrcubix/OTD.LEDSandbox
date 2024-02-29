using SkiaSharp;

namespace OTD.LEDSandbox.Converters;

public class UniversalConverter : Converter
{
    private const int WIDTH = 64;
    private const int HEIGHT = 32 * 4;
    
    /// <inheritdoc />
    public override byte[]? Convert(Stream stream, bool doFlip)
    {
        // seek to the beginning of the stream
        stream.Seek(0, SeekOrigin.Begin);

        var res = ConvertCore(stream, doFlip);

        return res;
    }

    // Use skiasharp to convert any image, which may not only be bitmap, to grayscale
    private byte[]? ConvertCore(Stream stream, bool doFlip)
    {
        using SKImage image = SKImage.FromEncodedData(stream);

        if (image is null)
        {
            Console.WriteLine("Failed to decode the image.");
            return null;
        }

        if (image.Width <= 0 || image.Height <= 0 || image.Width > WIDTH || image.Height > HEIGHT)
        {
            Console.WriteLine($"Invalid image dimensions, must be greater than 0, but under {WIDTH}x{HEIGHT}.");
            return null;
        }

        // Convert the image to grayscale
        SKBitmap result = ConvertToGrayscale(image);

        // flip the image if needed
        if (doFlip)
            FlipImage(result);

        // Convert the 8-bit grayscale image to a 4-bit grayscale image
        byte[] data = ConvertTo4BitGrayscale(result);

        // Dispose the result
        result.Dispose();

        // Finalize the conversion
        return FinalizeConversion(data);
    }

    private SKBitmap ConvertToGrayscale(SKImage image)
    {
        // This create an 8-bit grayscale image
        SKBitmap bitmap = new(image.Width, image.Height, SKColorType.Gray8, SKAlphaType.Premul);

        using SKCanvas canvas = new(bitmap);

        SKHighContrastConfig highContrastConfig = new()
        {
            Grayscale = true,
            InvertStyle = SKHighContrastConfigInvertStyle.NoInvert,
            Contrast = 0.0f
        };

        using SKPaint paint = new()
        {
            ColorFilter = SKColorFilter.CreateHighContrast(highContrastConfig),
            IsAntialias = true
        };

        canvas.DrawImage(image, 0, 0, paint);

        canvas.Flush();

        return bitmap;
    }

    private byte[] ConvertTo4BitGrayscale(SKBitmap image)
    {
        byte[] data = image.Bytes;

        // bitmaps are australian, they are stored upside down
        // flip for the conversion
        for (int p = 0; p < data.Length; p +=  image.Width)
            Array.Reverse(data, p, image.Width);

        Array.Reverse(data);

        int currentByte = 0;
        byte[] pixeldatahbpp = new byte[4096];

        for (int i = 0; i < data.Length - 1; i += 2)
        {
            byte h = (byte)((data[i + 1] >> 4) & 0x0F);
            byte l = (byte)(data[i] & 0xF0);

            pixeldatahbpp[currentByte++] = (byte)(h | l);
        }

        return pixeldatahbpp;
    }

    /// <summary>
    ///   Flip the image upside down.
    /// </summary>
    /// <param name="image">The image to flip.</param>
    private void FlipImage(SKBitmap image)
    {
        using var canvas = new SKCanvas(image);
        
        var matrix = SKMatrix.CreateScale(1, -1, image.Width / 2, image.Height / 2);

        canvas.Concat(ref matrix);

        canvas.DrawBitmap(image, 0, 0);

        canvas.Flush();
    }
}