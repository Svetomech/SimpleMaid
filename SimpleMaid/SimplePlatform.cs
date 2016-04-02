using static System.Environment;
using static System.PlatformID;

namespace SimpleLibrary
{
  public static class SimplePlatform
  {
    public enum Platform
    {
      Windows,
      Unix
    }

    public static Platform RunningPlatform()
    {
      switch (OSVersion.Platform)
      {
        case Unix:
        case MacOSX:
          return Platform.Unix;

        default:
          return Platform.Windows;
      }
    }
  }
}
