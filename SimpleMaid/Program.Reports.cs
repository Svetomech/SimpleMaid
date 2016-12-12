using System;
using System.Threading;
using static Svetomech.Utilities.SimpleConsole;

namespace SimpleMaid
{
  internal static partial class Program
  {
    internal static void ReportGeneralError(string message)
    {
      Console.BackgroundColor = ConsoleColor.Blue;
      Console.ForegroundColor = ConsoleColor.White;
      Console.WriteLine(message + Environment.NewLine);

      if (!MainWindow.IsShown)
      {
        MainWindow.Show();
        Console.Beep();
        Thread.Sleep(Variables.WindowCloseDelay);
        MainWindow.Hide();
      }
    }

    internal static void ReportWeakPassword()
    {
      string middlePractical = "| " + resources.PasswordWeakHint;
      string middle = middlePractical + " |";
      middle = middlePractical + Line.GetFilled(' ').Remove(0, middle.Length) + " |";

      Console.Write(@"#" + Line.GetFilled('-').Remove(0, 2) + @"#");
      Console.Write(middle);
      Console.Write(@"#" + Line.GetFilled('-').Remove(0, 2) + @"#");
      Console.CursorVisible = false;

      Thread.Sleep(Variables.PasswordWeakDelay);

      Console.CursorVisible = true;
    }

    private static void ReportPastSelf()
    {
      Console.ResetColor();
      Console.ForegroundColor = ConsoleColor.DarkMagenta;
      Console.WriteLine(resources.PastSins + Environment.NewLine);
    }

    private static void ReportWebError()
    {
      Console.ResetColor();
      Console.ForegroundColor = ConsoleColor.DarkYellow;
      Console.WriteLine(resources.WebErrorMessage + Environment.NewLine);
    }

    private static void ReportThreadStart(string message)
    {
      Console.ResetColor();
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine(message + Environment.NewLine);

      if (resources.CommandStart == message)
      {
        Console.Beep();
      }
    }

    private static void ReportThreadStop(string message)
    {
      Console.ResetColor();
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(message + Environment.NewLine);

      if (resources.CommandStop == message)
      {
        Console.Beep();
      }
    }
  }
}
