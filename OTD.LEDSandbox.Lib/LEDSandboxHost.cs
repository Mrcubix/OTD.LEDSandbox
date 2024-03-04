using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenTabletDriver.External.Common.Contracts;
using OpenTabletDriver.External.Common.Serializables;

namespace OTD.LEDSandbox.Lib
{
    public class LEDSandboxHost : IPluginDaemon
    {
        private int _brightness = 2;

        private static int _activeRingLED = 0;

        #region Constructors

        public LEDSandboxHost() {}

        #endregion

        #region Events

        public event EventHandler<int>? BrightnessChanged;

        public static event EventHandler<int>? ActiveRingLEDChanged;

        #endregion

        #region Properties

        public int Brightness
        {
            get => _brightness;
            set
            {
                if (_brightness != value)
                {
                    _brightness = value;
                    BrightnessChanged?.Invoke(this, value);
                }
            }
        }

        public static int ActiveRingLED
        {
            get => _activeRingLED;
            set
            {
                if (_activeRingLED != value)
                {
                    _activeRingLED = value;
                    ActiveRingLEDChanged?.Invoke(null, value);
                }
            }
        }

        #endregion

        #region RPC Methods

        public Task<List<SerializablePlugin>> GetPlugins()
        {
            throw new NotSupportedException();
        }

        public Task<int> GetCurrentBrightness() => Task.FromResult(Brightness);

        public Task SetRingLED(int ringLED)
        {
            lock (this)
            {
                ActiveRingLED = Math.Clamp(ringLED, 0, 3);
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}
