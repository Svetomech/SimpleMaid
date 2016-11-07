using IniParser.Model;
using Svetomech.Utilities;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using static Svetomech.Utilities.PasswordStrength;
using static Svetomech.Utilities.SimpleConsole;
using static Svetomech.Utilities.NativeMethods;

namespace SimpleMaid
{
  internal static partial class Program
  {
    // TODO: Set&Get vs SetUntilSet&GetUntilGet - clarify use cases
    private static string Set(string tag, string value)
    {
      tag = $"{Application.ProductName}_{tag}";

      var encoding = new UTF8Encoding();
      byte[] requestBody = encoding.GetBytes($"tag={tag}&value={value}&fmt=html");

      var request = (HttpWebRequest)WebRequest.Create($"{Variables.ServerAddress}/storeavalue");
      request.Method = "POST";
      request.Credentials = Variables.Credentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;
      request.UserAgent = Variables.UserAgent;

      try
      {
        using (var requestStream = request.GetRequestStream())
        {
          requestStream.Write(requestBody, 0, requestBody.Length);
        }
      }
      catch (WebException)
      {
        reportWebError();
        internetAlive = false;
        return resources.WebErrorMessage;
      }
      try { using (var response = request.GetResponse()) ; }
      catch (WebException)
      {
        reportWebError();
        internetAlive = false;
        return resources.WebErrorMessage;
      }

      resetConsoleColor();
      Console.ForegroundColor = ConsoleColor.Gray;
      Console.WriteLine($"SET  {tag}  {value}\n");

      return String.Empty;
    }

    private static string Get(string tag)
    {
      tag = $"{Application.ProductName}_{tag}";

      string value;

      var encoding = new UTF8Encoding();
      byte[] requestBody = encoding.GetBytes($"tag={tag}&fmt=html");

      var request = (HttpWebRequest)WebRequest.Create($"{Variables.ServerAddress}/getvalue");
      request.Method = "POST";
      request.Credentials = Variables.Credentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;
      request.UserAgent = Variables.UserAgent;

      try
      {
        using (var requestStream = request.GetRequestStream())
        {
          requestStream.Write(requestBody, 0, requestBody.Length);
        }
      }
      catch (WebException)
      {
        reportWebError();
        internetAlive = false;
        return resources.WebErrorMessage;
      }

      // TODO: Refactor
      try
      {
        using (var response = request.GetResponse())
        using (var responseStream = response.GetResponseStream())
        using (var sr = new StreamReader(responseStream))
        {
          value = sr.ReadToEnd();
          value = value.Substring(value.IndexOf(tag) + tag.Length + 4);
          value = value.Remove(value.IndexOf("\""));
        }
      }
      catch (WebException)
      {
        reportWebError();
        internetAlive = false;
        return resources.WebErrorMessage;
      }

      // TODO: Rewrite - problems with decoding
      value = decodeEncodedNonAsciiCharacters(value);
      value = value.Replace(@"\/", @"/");
      value = value.Replace(@"\\", @"\");
      value = WebUtility.HtmlDecode(value);

      resetConsoleColor();
      Console.ForegroundColor = ConsoleColor.DarkGreen;
      Console.WriteLine($"GET  {tag}  {value}\n");

      return value;
    }

    private static void SetUntilSet(string tag, string value)
    {
      while (internetAlive && resources.WebErrorMessage == Set(tag, value))
      {
        Thread.Sleep(Variables.GeneralDelay);
      }
    }

    private static string GetUntilGet(string tag)
    {
      string value = resources.WebErrorMessage;

      while (internetAlive && resources.WebErrorMessage == (value = Get(tag)))
      {
        Thread.Sleep(Variables.GeneralDelay);
      }

      return value;
    }

    private static void exit()
    {
      Thread.Sleep(Variables.WindowCloseDelay);
      Environment.Exit(0);
    }

    private static string createMachine()
    {
      return Guid.NewGuid().ToString();
    }

    private static void configureMachine()
    {
      int valueLength = machineName.Length + 1; // Variables.MachinesDelimiter
      int realValueLimit = (int)Math.Floor(Variables.IndividualValueLimit / valueLength) * valueLength;

      int listIndex = -1;
      string currentList;
      do
      {
        listIndex++;
        currentList = GetUntilGet($"machines{listIndex}");
        if (currentList.Contains(machineName))
        {
          return;
        }
      } while (currentList.Length >= realValueLimit);

      string machines = currentList;

      SetUntilSet($"machines{listIndex}", $"{machines}{machineName}{Variables.MachinesDelimiter}");
    }

    private static bool isNameOK(string name)
    {
      Guid temp; // TODO: Waiting for C# 7.0 to turn this into one-liner
      return Guid.TryParse(name, out temp);
    }

    private static bool isPasswordOK(string password)
    {
      var strength = CheckStrength(password);

      Program.State = $"{resources.MainWindowTitle} [{nameof(PasswordStrength)}: {strength}]";

      return (strength >= Variables.MinimalPasswordStrength);
    }

    private static string passwordPrompt()
    {
      return InsecurePasswordPrompt(resources.PasswordEnterTip);
    }

    // TODO: Unite these two into validatePassword
    private static void validateMemoryPassword(ref IniData configuration, ref bool promptShown)
    {
      if (isPasswordOK(machinePassword))
      {
        configuration["Service"].AddKey("sMachinePassword", machinePassword);
      }
      else
      {
        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_SHOW);
        }

        while (!isPasswordOK(machinePassword = passwordPrompt()))
        {
          reportWeakPassword();
        }
        promptShown = true;

        configuration["Service"].AddKey("sMachinePassword", machinePassword);

        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_HIDE);
        }
      }
    }
    private static void validateConfigPassword(ref IniData configuration, ref bool promptShown)
    {
      if (isPasswordOK(configuration["Service"]["sMachinePassword"]))
      {
        machinePassword = configuration["Service"]["sMachinePassword"];
      }
      else
      {
        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_SHOW);
        }

        while (!isPasswordOK(machinePassword = passwordPrompt()))
        {
          reportWeakPassword();
        }
        promptShown = true;

        configuration["Service"]["sMachinePassword"] = machinePassword;

        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_HIDE);
        }
      }
    }

    private static void resetConsoleColor()
    {
      if (runningWindows)
      {
        Console.BackgroundColor = ConsoleColor.Black;
      }
      else
      {
        Console.ResetColor();
      }
    }

    // TODO: Get filename from Response.Header
    private static string urlToFileName(string url)
    {
      return Uri.UnescapeDataString(url.Substring(url.LastIndexOf('/') + 1));
    }

    // TODO: Implement adequate decoding
    private static string decodeEncodedNonAsciiCharacters(string value)
    {
      return Regex.Replace(value, @"\\u(?<Value>[a-zA-Z0-9]{4})", m =>
        ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString());
    }

    private static void openChatWindow()
    {
      busyChatWise = true;

      ChatboxWindow = new frmChatWindow();
      ChatboxWindow.ShowDialog();

      // ! code below only executes after ChatboxWindow is closed

      ChatboxExit = false;

      busyChatWise = false;

      SetUntilSet($"commands.{machineName}", $"{Variables.AnswerPrefix}{Variables.MessageCommand},{ChatboxWindow.Visible}");
    }

    private static void closeChatWindow()
    {
      busyChatWise = false;

      ChatboxExit = true;
    }

    private static void resurrectDeadThreads()
    {
      Thread[] threads = { connectionThread,   commandThread,   chatThread };
      Action[] starts  = { handleConnection,   awaitCommands,   serveMessages };
      bool[]   flags   = { busyConnectionWise, busyCommandWise, busyChatWise };

      for (int i = 0; i < threads.Length; ++i)
      {
        resurrectDeadThread(ref threads[i], starts[i], flags[i]);
      }
    }

    private static void resurrectDeadThread(ref Thread thread, Action start, bool flag)
    {
      if (flag && thread != null && !thread.IsAlive)
      {
        thread = new Thread(new ThreadStart(start));
        thread.IsBackground = true;
        thread.Start();
      }
    }
  }
}
