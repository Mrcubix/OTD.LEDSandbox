using OpenTabletDriver;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.DependencyInjection;
using OpenTabletDriver.Plugin.Devices;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using OTD.LEDSandbox.Converters;
using SysConvert = System.Convert;

namespace OTD.LEDSandbox;

[PluginName("LED Sandbox")]
public class LEDSandbox : IPositionedPipelineElement<IDeviceReport>
{
    private const int WIDTH = 64;
    private const int HEIGHT = 128;

    // Only support Wacom Devices
    public const int SUPPORTED_VENDORID = 1386;
    // range from 184 to 188
    public static readonly int[] SupportedProductID = Enumerable.Range(184, 188).ToArray();

    private UniversalConverter _universalConverter = null!;

    #region Initialization

    [OnDependencyLoad]
    public void Initialize()
    {
        _universalConverter = new();

        if (Driver is Driver driver)
        {
            var tablet = driver.InputDevices.Where(dev => dev.Properties == Tablet.Properties).FirstOrDefault();
            var device = tablet?.InputDevices.Where(dev => dev.Configuration == Tablet.Properties).FirstOrDefault();

            if (device is not InputDevice inputDevice)
                return;

            var identifier = inputDevice.Identifier;

            // check the vendor id fisrt
            if (identifier.VendorID == SUPPORTED_VENDORID && SupportedProductID.Contains(identifier.ProductID))
            {
                Log.Write("CustomLED", "The device is supported.", LogLevel.Info);

                FileInfo topDisplayImage = null!;
                FileInfo bottomDisplayImage = null!;

                // Read the files
                if (!string.IsNullOrEmpty(TopDisplayImage))
                    topDisplayImage = new FileInfo(TopDisplayImage);

                if (!string.IsNullOrEmpty(BottomDisplayImage))
                    bottomDisplayImage = new FileInfo(BottomDisplayImage);

                if (topDisplayImage is not null)
                    InitializeCore(topDisplayImage, 0, FlipTopDisplayImage, WIDTH, HEIGHT, inputDevice.ReportStream);

                if (bottomDisplayImage is not null)
                    InitializeCore(bottomDisplayImage, 4, FlipBottomDisplayImage, WIDTH, HEIGHT, inputDevice.ReportStream);
            }
            else
            {
                Log.Write("CustomLED", "The device is not supported.", LogLevel.Warning);
                Log.Write("CustomLED", "Supported devices are : Wacoms PTK-440, PTK-540WL, PTK-640, PTK-840, PTK-1240.", LogLevel.Warning);
            }
        }
    }

    private void InitializeCore(FileInfo file, int displayChunk, bool doFlip, int width, int height, IDeviceEndpointStream hidStream)
    {
        // Read the image
        var stream = file.OpenRead();

        byte[]? data;

        try
        {
            // Convert the image to raw data
            data = Convert(stream, doFlip);
        }
        catch (TypeInitializationException)
        {
            Log.Write("CustomLED", "Probably failed to load libskiasharp.", LogLevel.Fatal);
            Log.Write("CustomLED", "If you are running on an arm device, Install the arm version.", LogLevel.Fatal);
            return;
        }

        if (data is null)
            return;

        // Convert the raw data to init data
        var initData = ConvertImageToInitData(data, displayChunk, width, height);

        // Send the init data to the tablet
        SendInitData(initData, hidStream);

        // Close the stream
        stream.Dispose();
    }

    #endregion

    #region Events

#pragma warning disable CS8618

    public event Action<IDeviceReport> Emit;

#pragma warning restore CS8618

    #endregion

    #region Properties

#pragma warning disable CS8618

    [TabletReference]
    public TabletReference Tablet { get; set; }

    [Resolved]
    public IDriver Driver { get; set; }

    #region Plugin Properties

    [Property("Top Display Image"),
     DefaultPropertyValue(""),
     ToolTip("The image to display on the top display.")]
    public string TopDisplayImage { get; set; }

    [Property("Flip Top Display Image"),
     DefaultPropertyValue(false),
     ToolTip("Whether to flip the top display image.")]
    public bool FlipTopDisplayImage { get; set; }

    [Property("Bottom Display Image"),
     DefaultPropertyValue(""),
     ToolTip("The image to display on the bottom display.")]
    public string BottomDisplayImage { get; set; }

    [Property("Flip Bottom Display Image"),
     DefaultPropertyValue(false),
     ToolTip("Whether to flip the bottom display image.")]
    public bool FlipBottomDisplayImage { get; set; }

    public PipelinePosition Position => PipelinePosition.None;

#pragma warning restore CS8618

    #endregion

    #endregion

    #region Methods

    public void Consume(IDeviceReport value) => Emit?.Invoke(value);

    #region Conversion

    /// <summary>
    ///   Converts the image bytes to a common format.
    /// </summary>
    /// <param name="imageBytes">The image bytes to convert.</param>
    /// <returns>The converted image bytes.</returns>
    public byte[]? Convert(Stream stream, bool doFlip)
    {
        //return _bitmapConverter.Convert(stream);
        return _universalConverter.Convert(stream, doFlip);
    }

    public void FlipData(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            byte chr = data[i];
            byte h = (byte)((chr >> 4) & 0x0F);
            byte l = (byte)((chr & 0x0F) << 4);

            data[i] = 0;
            data[i] |= h;
            data[i] |= l;
        }

        Array.Reverse(data);
    }

    public byte[,] ConvertImageToInitData(byte[] data, int displayChunk, int width, int height)
    {
        const int MAX_CHUNK_SIZE = 512;

        byte[,] features = new byte[16, 256 + 3];

        int displayChunkBlock = 0;
        int featureIndex = 0;
        int currentByte = 0;

        for (int i = 0; i < width * height; i++)
        {
            if (!SysConvert.ToBoolean(i % MAX_CHUNK_SIZE))
            {
                if (i > 0)
                {
                    featureIndex++;
                }

                if (displayChunkBlock > 3)
                {
                    displayChunk++;
                    displayChunkBlock = 0;
                }

                features[featureIndex, 0] = 0x23;
                features[featureIndex, 1] = (byte)displayChunk;
                features[featureIndex, 2] = (byte)displayChunkBlock;

                currentByte = 3;
                displayChunkBlock++;
            }
            if (!SysConvert.ToBoolean(i % 2))
            {
                features[featureIndex, currentByte] = (byte)((data[i] << 4) & 0xF0);
                currentByte++;
            }
            else
            {
                features[featureIndex, currentByte - 1] |= data[i];
            }
        }
        return features;
    }

    #endregion

    public void SendInitData(byte[,] features, IDeviceEndpointStream hidStream)
    {
        for (int i = 0; i < features.GetLength(0); i++)
        {
            byte[] row = Enumerable.Range(0, features.GetUpperBound(1) + 1)
              .Select(j => features[i, j])
              .ToArray();

            hidStream.SetFeature(row);
        }
    }

    #endregion

    #region Disposal

    public void Dispose()
    {

    }

    #endregion
}
