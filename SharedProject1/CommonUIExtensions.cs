using System.Reflection;

namespace Artemious.Helpers
{
    public static class CommonUIExtensions
    {
        #region IsWindows10

        static bool? _isWindows10;
        public static bool IsWindows10 => (_isWindows10 ?? (_isWindows10 = getIsWindows10Sync())).Value;

        static bool getIsWindows10Sync()
        {
            bool hasWindows81Property = typeof(Windows.ApplicationModel.Package).GetRuntimeProperty("DisplayName") != null;
            bool hasWindowsPhone81Property = typeof(Windows.Graphics.Display.DisplayInformation).GetRuntimeProperty("RawPixelsPerViewPixel") != null;

            bool isWindows10 = hasWindows81Property && hasWindowsPhone81Property;
            return isWindows10;
        }

        #endregion

    }
}
