using System;
using System.Diagnostics;
using static System.Console;

namespace SimpleMaid
{
  public enum ConsoleTypes
  {
    CMD,
    Powershell,
    Bash,
    None
  }

  public static class SimpleConsole
  {
    public static string ExecuteCommand(string command, ConsoleTypes console)
    {
      ProcessStartInfo procStartInfo = null;
      switch (console)
      {
        case ConsoleTypes.CMD:
          procStartInfo = new ProcessStartInfo("cmd", "/c " + command);
          break;

        case ConsoleTypes.Powershell:
          procStartInfo = new ProcessStartInfo("powershell", "-command " + command);
          break;

        case ConsoleTypes.Bash:
          procStartInfo = new ProcessStartInfo("/bin/bash", "-c " + command);
          break;

        case ConsoleTypes.None:
          return null;

        default:
          throw new ArgumentException(nameof(console));
      }

      procStartInfo.RedirectStandardOutput = true;
      procStartInfo.UseShellExecute = false;
      procStartInfo.CreateNoWindow = true;

      Process proc = new Process();
      proc.StartInfo = procStartInfo;
      proc.Start();

      string result = proc.StandardOutput.ReadToEnd();

      // HACK: Only need it because my server doesn't handle NewLine too well
      result = result.Replace(Environment.NewLine, "\n");

      return result;
    }

    public static class Line
    {
      /// <summary>
      /// Isn't adaptive to the window width changes.
      /// </summary>
      /// <returns>a string to fit entire window width of the console.</returns>
      public static string GetFilled(char filler)
      {
        return new string(filler, WindowWidth);
      }

      /// <summary>
      /// SetCursorPosition() to the beginning of the line you want to clear beforehand.
      /// </summary>
      public static void ClearCurrent()
      {
        int currentLineCursor = CursorTop;
        SetCursorPosition(0, CursorTop);
        Write(new string(' ', WindowWidth));
        SetCursorPosition(0, currentLineCursor);
      }
    }
  }
}
