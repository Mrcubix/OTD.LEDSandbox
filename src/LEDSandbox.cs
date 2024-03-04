﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;
using OpenTabletDriver;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Devices;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Tablet;
using OTD.LEDSandbox.Converters;
using OTD.LEDSandbox.Lib;
using SysConvert = System.Convert;

namespace OTD.LEDSandbox
{
    [PluginName("LED Sandbox")]
    public class LEDSandbox : ITool
    {
        #region Constants
        private const int WIDTH = 64;
        private const int HEIGHT = 128;

        // Only support Wacom Devices, with product id ranging from 184 to 188 (PTK-440, PTK-540WL, PTK-640, PTK-840, PTK-1240)
        public const int SUPPORTED_VENDORID = 1386;
        public static readonly int[] SupportedProductID = Enumerable.Range(184, 188).ToArray();

        private readonly UniversalConverter _universalConverter;
        private readonly LEDSandboxHost _ledSandboxHost;
        private readonly CancellationTokenSource _tokenSource;

        #endregion

        #region Fields

        private RpcHost<LEDSandboxHost> _rpcHost;

        private HidStream _reportStream;

        #endregion

        #region Constructor

#pragma warning disable CS8618

        public LEDSandbox()
        {
            _universalConverter = new();
            _tokenSource = new();

            _rpcHost = new RpcHost<LEDSandboxHost>("OTD.LEDSandbox");
            _ledSandboxHost = _rpcHost.Instance;
        }

#pragma warning restore CS8618

        #endregion

        #region Initialization

        public bool Initialize()
        {
            _ = Task.Run(PreInitializeCore);

            return true;
        }

        private void PreInitializeCore()
        {
            // values goes from 4 to 7, but we offset by 4 from the original value
            _ledSandboxHost.Brightness = Math.Clamp(_ledSandboxHost.Brightness, 0, 3);

            // Initialize the RPC host
            try
            {
                _ = Task.Run(InitializeRPC, _tokenSource.Token);
            }
            catch (Exception e)
            {
                Log.Write("CustomLED", "An unhandled exception occurred while initializing the RPC host.", LogLevel.Error);
                Log.Write("CustomLED", e.Message, LogLevel.Error);
            }

            // Start fetching the tablet, images, build them & send them
            InitializeCore();

            SendBrightnessCommand();
        }

        private void InitializeCore()
        {
            if (Info.Driver is Driver driver)
            {
                var tablet = driver.Tablet;
                var _reader = driver.TabletReader;

                if (_reader is not DeviceReader<IDeviceReport> reader || tablet is null)
                    return;

                _reportStream = _reader.ReportStream;

                var device = reader.Device;

                // check the vendor id fisrt
                if (device.VendorID == SUPPORTED_VENDORID && SupportedProductID.Contains(device.ProductID))
                {
                    Log.Write("CustomLED", "The device is supported.", LogLevel.Info);

                    // Read the top display image
                    if (!TryAccessFile(TopDisplayImage, out FileInfo topDisplayImage))
                    {
                        Log.Write("CustomLED", "The top display image does not exist or plugin does not have read access.", LogLevel.Warning);
                    }

                    // Read the bottom display image
                    if (!TryAccessFile(BottomDisplayImage, out FileInfo bottomDisplayImage))
                    {
                        Log.Write("CustomLED", "The bottom display image does not exist or plugin does not have read access.", LogLevel.Warning);
                    }

                    if (topDisplayImage != null)
                        BuildAndSendImage(topDisplayImage, 0, FlipTopDisplayImage, WIDTH, HEIGHT, _reader.ReportStream);

                    if (bottomDisplayImage != null)
                        BuildAndSendImage(bottomDisplayImage, 4, FlipBottomDisplayImage, WIDTH, HEIGHT, _reader.ReportStream);
                }
                else
                {
                    Log.Write("CustomLED", "The device is not supported.", LogLevel.Warning);
                    Log.Write("CustomLED", "Supported devices are : Wacoms PTK-440, PTK-540WL, PTK-640, PTK-840, PTK-1240.", LogLevel.Warning);
                }
            }
        }

        private async Task InitializeRPC()
        {
            _rpcHost.ConnectionStateChanged += OnConnectionStateChanged;
            LEDSandboxHost.ActiveRingLEDChanged += OnActiveRingLEDChanged;

            await _rpcHost.Main();
        }

        #endregion

        #region Properties

        #region Plugin Properties

        [Property("Top Display Image"),
         DefaultPropertyValue(""),
         ToolTip("The image to display on the top display.")]
        public string TopDisplayImage { get; set; } = string.Empty;

        [Property("Flip Top Display Image"),
         DefaultPropertyValue(false),
         ToolTip("Whether to flip the top display image.")]
        public bool FlipTopDisplayImage { get; set; }

        [Property("Bottom Display Image"),
         DefaultPropertyValue(""),
         ToolTip("The image to display on the bottom display.")]
        public string BottomDisplayImage { get; set; } = string.Empty;

        [Property("Flip Bottom Display Image"),
         DefaultPropertyValue(false),
         ToolTip("Whether to flip the bottom display image.")]
        public bool FlipBottomDisplayImage { get; set; }

        [Property("Brightness"),
         DefaultPropertyValue(2),
         ToolTip("The brightness of the LED, from the following values: \n" +
                 "0: Off, \n" +
                 "1: Dim, \n" +
                 "2: Intermediate, \n" +
                 "3: Brightest.")]
        public int Brightness { get; set; }

        #endregion

        #endregion

        #region Methods

        #region Image Processing

        private void BuildAndSendImage(FileInfo file, int displayChunk, bool doFlip, int width, int height, HidStream hidStream)
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
            catch (Exception e)
            {
                Log.Write("CustomLED", "An unhandled exception occurred while converting the image.", LogLevel.Error);
                Log.Write("CustomLED", e.Message, LogLevel.Error);
                return;
            }

            if (data is null)
                return;

            // Convert the raw data to init data
            var initData = ConvertImageToInitData(data, displayChunk, width, height);

            try
            {
                // Send the init data to the tablet
                SendInitData(initData, hidStream);
            }
            catch (Exception e)
            {
                Log.Write("CustomLED", "An unhandled exception occurred while sending the init data.", LogLevel.Error);
                Log.Write("CustomLED", e.Message, LogLevel.Error);
            }

            // Close the stream
            stream.Dispose();
        }

        #endregion

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

        #region IO

        public void SendInitData(byte[,] features, HidStream hidStream)
        {
            for (int i = 0; i < features.GetLength(0); i++)
            {
                byte[] row = Enumerable.Range(0, features.GetUpperBound(1) + 1)
                  .Select(j => features[i, j])
                  .ToArray();

                hidStream.SetFeature(row);
            }
        }

        public bool TryAccessFile(string path, out FileInfo info)
        {
            info = null!;

            if (string.IsNullOrEmpty(path))
                return false;

            if (!File.Exists(path))
                return false;

            try
            {
                info = new FileInfo(path);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public void SendBrightnessCommand()
        {
            if (_reportStream is null)
                return;

            // Proceed to build the report
            var report = new byte[9];

            // Report ID
            report[0] = 0x20;

            // Ring LED
            report[1] = (byte)(4 + LEDSandboxHost.ActiveRingLED);

            // Brightness
            var brightnessByte = GetBrightnessBytes();

            report[2] = brightnessByte[0];
            report[3] = brightnessByte[1];
            report[4] = brightnessByte[2];

            try
            {
                // Send the report
                _reportStream?.SetFeature(report.ToArray());

                Log.Write("CustomLED", $"The active ring LED has changed to {LEDSandboxHost.ActiveRingLED}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log.Write("CustomLED", "An unhandled exception occurred while sending the report.", LogLevel.Error);
                Log.Write("CustomLED", ex.Message, LogLevel.Error);
            }
        }

        #endregion

        private byte[] GetBrightnessBytes()
        {
            return Brightness switch
            {
                0 => new byte[] { 0x0a, 0x0a, 0x00 },
                1 => new byte[] { 0x0a, 0x28, 0x15 },
                2 => new byte[] { 0x0a, 0x28, 0x1a },
                _ => new byte[] { 0x20, 0x7f, 0x1f }
            };
        }

        #endregion

        #region Event Handlers

        private void OnConnectionStateChanged(object? sender, bool e)
        {
            Log.Write("CustomLED", $"An Application {(e ? "is now connected to" : "has disconnected from")} the LED host.", LogLevel.Info);
        }

        private void OnActiveRingLEDChanged(object? sender, int e) => SendBrightnessCommand();

        #endregion

        #region Disposal

        public void Dispose()
        {
            _rpcHost.ConnectionStateChanged -= OnConnectionStateChanged;
            LEDSandboxHost.ActiveRingLEDChanged -= OnActiveRingLEDChanged;

            _tokenSource.Cancel();
        }

        #endregion
    }
}