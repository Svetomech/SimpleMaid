using static System.Console;
using static System.String;

namespace SimpleMaid
{
  public static class SimpleConsole
  {
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
