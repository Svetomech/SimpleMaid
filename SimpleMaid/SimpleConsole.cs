using System;
using System.Diagnostics;
using System.Security;
using static System.Console;

namespace Svetomech.SimpleUtilities
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

    /* HACK (to get the actual value, not an object): new NetworkCredential(String.Empty, PasswordPrompt("Enter a password: ")).Password;
         N.B.! It's really dirty, kills the purpose of using a SecureString and doesn't even work in Mono */
    public static SecureString PasswordPrompt(string hintMessage)
    {
      Clear();
      CursorVisible = true;

      string middlePractical = "| " + hintMessage;
      string middle = middlePractical + " |";
      middle = middlePractical + Line.GetFilled(' ').Remove(0, middle.Length) + " |";

      Write("#" + Line.GetFilled('-').Remove(0, 2) + "#");
      Write(middle);
      Write("#" + Line.GetFilled('-').Remove(0, 2) + "#");
      SetCursorPosition(middlePractical.Length, CursorTop - 2);

      ConsoleKeyInfo keyInfo;
      var passHolder = new SecureString();
      int starsCount = 0;
      int middleDiff = middle.Length - middlePractical.Length;
      while ((keyInfo = ReadKey(true)).Key != ConsoleKey.Enter)
      {
        if (keyInfo.Key != ConsoleKey.Backspace)
        {
          /*if (!((int)ki.Key >= 65 && (int)ki.Key <= 90))
            continue;*/ // <-- stricter, but disallows digits
          if (char.IsControl(keyInfo.KeyChar))
            continue;

          if (starsCount + 1 < middleDiff)
            starsCount++;
          else
            continue;

          passHolder.AppendChar(keyInfo.KeyChar);

          Write('*');
        }
        else
        {
          if (starsCount - 1 >= 0)
            starsCount--;
          else
            continue;

          passHolder.RemoveAt(passHolder.Length - 1);

          Line.ClearCurrent();
          Write(middlePractical);
          for (int i = 0; i < starsCount; ++i)
          {
            Write('*');
          }
          var pos = new Point(CursorLeft, CursorTop);
          Write(Line.GetFilled(' ').Remove(0, middlePractical.Length + starsCount + " |".Length) + " |");
          SetCursorPosition(pos.X, pos.Y);
        }

        if (0 == starsCount)
        {
          Beep();
        }
        if (middleDiff - 1 == starsCount)
        {
          SetCursorPosition(CursorLeft - 1, CursorTop);
          Beep();
        }
      }

      Clear();

      return passHolder;
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
