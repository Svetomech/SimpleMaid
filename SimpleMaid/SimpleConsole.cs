using static System.Console;
using static System.String;

namespace SimpleMaid
{
  public static class SimpleConsole
  {
    public class Arguments
    {
      /// <summary>
      /// Faster implementation of LINQ's Array.Contains().
      /// </summary>
      public static bool CheckPresence(string arg, string[] argArray)
      {
        if (argArray.Length < 1)
          return false;

        foreach (string argument in argArray)
        {
          if (argument == arg)
            return true;
        }
        return false;
      }

      /// <summary>
      /// Example: YourApp.exe --arg1 --arg2 value --arg3
      /// </summary>
      /// <returns>null if not found; String.Empty if found, but no value; otherwise, value.</returns>
      public static string GetValue(string arg, string[] argArray)
      {
        if (argArray.Length < 2)
          return null;

        for (int i = 0; i < argArray.Length; ++i)
        {
          if (argArray[i] == arg)
          {
            return (i + 1 < argArray.Length) ? argArray[i + 1] : Empty;
          }
        }
        return null;
      }
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
