using System;
using System.Threading;
using static Svetomech.Utilities.NativeMethods;
using static Svetomech.Utilities.SimpleConsole;

namespace SimpleMaid
{
  internal static partial class Program
  {
    private static void reportPastSelf()
    {
      resetConsoleColor();
      Console.ForegroundColor = ConsoleColor.DarkMagenta;
      Console.WriteLine(resources.PastSins + "\n");
    }

    private static void reportWeakPassword()
    {
      string middlePractical = "| " + resources.PasswordWeakHint;
      string middle = middlePractical + " |";
      middle = middlePractical + Line.GetFilled(' ').Remove(0, middle.Length) + " |";

      Console.Write("#" + Line.GetFilled('-').Remove(0, 2) + "#");
      Console.Write(middle);
      Console.Write("#" + Line.GetFilled('-').Remove(0, 2) + "#");
      Console.CursorVisible = false;

      Thread.Sleep(Variables.PasswordWeakDelay);

      Console.CursorVisible = true;
    }

    private static void reportWebError()
    {
      resetConsoleColor();
      Console.ForegroundColor = ConsoleColor.DarkYellow;
      Console.WriteLine(resources.WebErrorMessage + "\n");
    }

    private static void reportGeneralError(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Blue;
      Console.ForegroundColor = ConsoleColor.White;
      Console.WriteLine(msg + "\n");

      if (Program.Hidden)
      {
        ShowWindow(mainWindowHandle, SW_SHOW);
        Console.Beep();
        Thread.Sleep(Variables.GeneralCloseDelay);
        ShowWindow(mainWindowHandle, SW_HIDE);
      }
    }

    private static void reportThreadStart(string msg)
    {
      resetConsoleColor();
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine(msg + "\n");

      if (resources.CommandStart == msg)
      {
        Console.Beep();
      }
    }

    private static void reportThreadStop(string msg)
    {
      resetConsoleColor();
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(msg + "\n");

      if (resources.CommandStop == msg)
      {
        Console.Beep();
      }
    }
  }
}
