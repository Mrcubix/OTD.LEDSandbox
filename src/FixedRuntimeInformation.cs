using System;
using System.Runtime.InteropServices;
namespace OTD.LEDSandbox
{
    public static class FixedRuntimeInformation
    {
        public static string BaseOSDescription => GetBaseOSDescription();
        public static string Architecture => RuntimeInformation.OSArchitecture.ToString().ToLower();
        public static string RuntimeIdentifier => $"{BaseOSDescription}-{Architecture}";
        private static string GetBaseOSDescription()
        {
            if (OperatingSystem.IsWindows())
                return "windows";
            if (OperatingSystem.IsLinux())
                return "linux";
            if (OperatingSystem.IsMacOS())
                return "osx";
            return "unknown";
        }
    }
}