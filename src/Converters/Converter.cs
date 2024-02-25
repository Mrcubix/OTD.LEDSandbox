namespace OTD.CustomLED.Converters;

public abstract class Converter
{
    /// <summary>
    ///   Converts the image bytes to a common format.
    /// </summary>
    /// <param name="data">The image's stream to convert.</param>
    /// <returns>The converted image bytes.</returns>
    public abstract byte[]? Convert(Stream data);
}