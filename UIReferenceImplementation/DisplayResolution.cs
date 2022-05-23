// Copyright @ MyScript. All rights reserved.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class DisplayResolution
    {
        public static Vector GetDpi(Window window, Boolean rawDPI)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            uint dpiX = 0;
            uint dpiY = 0;
            GetDpi(hwnd, rawDPI, out dpiX, out dpiY);

            var source = PresentationSource.FromVisual(window);
            Matrix transform = source.CompositionTarget.TransformFromDevice;
            Vector dcuPx = transform.Transform(new Vector(1, 1));

            return new Vector(dpiX * dcuPx.X, dpiY * dcuPx.Y);
        }


        /// <summary>
        /// Returns the scaling of the given screen.
        /// </summary>
        /// <param name="dpiX">Gives the horizontal scaling back (in dpi).</param>
        /// <param name="dpiY">Gives the vertical scaling back (in dpi).</param>
        private static void GetDpi(IntPtr hwnd, Boolean rawDPI, out uint dpiX, out uint dpiY)
        {
            var hmonitor = MonitorFromWindow(hwnd, _MONITOR_DEFAULTTONEAREST);
            var typeDPI = rawDPI ? _MDT_RAW_DPI : _MDT_EFFECTIVE_DPI;
            var hresult_ = GetDpiForMonitor(hmonitor, typeDPI, out dpiX, out dpiY);

            // When compiling for x86, the 32 bits IntPtr returned by GetDpiForMonitor can be negative
            // (for example _E_NOTSUPPORTED == 0x80070032).
            // Casting to Int64 (required when compiling for x64) returns also a negative value in x86
            // (for example 0xFFFFFFFF80070032 for _E_NOTSUPPORTED), and so we need to mask the value
            // to get the significant lower 32 bits part.
            var hresult = hresult_.ToInt64() & 0xFFFFFFFF;

            switch (hresult)
            {
                case _S_OK: break;
                case _E_NOTSUPPORTED: dpiX = dpiY = 0; break;
                case _E_HANDLE:
                case _E_INVALIDARG:
                case _E_BADARG:
                    throw new ArgumentException("Invalid argument. See https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510.aspx for more information.");
                default:
                    throw new COMException("Unknown error. See https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510.aspx for more information.");
            }

            if (dpiX == 0 || dpiY == 0)
                dpiX = dpiY = 96;
        }

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromWindow([In]IntPtr hwnd, [In]uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In]IntPtr hmonitor, [In]int dpiType, [Out]out uint dpiX, [Out]out uint dpiY);

        const long _S_OK = 0;
        const long _E_NOTSUPPORTED = 0x80070032L;
        const long _E_INVALIDARG = 0x80070057L;
        const long _E_HANDLE = 0x80070006L;
        const long _E_BADARG = 0x800700a0L;

        const int _MONITOR_DEFAULTTONEAREST = 2;
        const int _MDT_EFFECTIVE_DPI = 0;
        const int _MDT_RAW_DPI = 2;
    }
}
