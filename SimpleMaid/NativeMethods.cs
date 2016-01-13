using System;
using System.Runtime.InteropServices;

namespace SimpleMaid
{
  internal static class NativeMethods
  {
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    internal const int SW_HIDE = 0;
    internal const int SW_SHOW = 5;
  }
}
