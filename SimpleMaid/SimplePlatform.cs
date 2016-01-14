using System;

namespace SimpleMaid
{
  public static class SimplePlatform
  {
    public enum Platform
    {
      Windows,
      Unix
    }

    public static Platform runningPlatform()
    {
      switch (Environment.OSVersion.Platform)
      {
        case PlatformID.Unix:
        case PlatformID.MacOSX:
          return Platform.Unix;

        default:
          return Platform.Windows;
      }
    }
  }
}
