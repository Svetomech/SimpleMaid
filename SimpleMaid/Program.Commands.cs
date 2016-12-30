using Svetomech.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using static Svetomech.Utilities.SimpleConsole;
using static Svetomech.Utilities.SimpleString;

namespace SimpleMaid
{
  internal static partial class Program
  {
    internal static string RunCommand(string command, ConsoleType console = ConsoleType.None)
    {
      if (ConsoleType.None == console)
      {
        return RunningWindows ? ExecuteCommand(command, ConsoleType.CMD) : ExecuteCommand(command, ConsoleType.Bash);
      }
      else
      {
        return ExecuteCommand(command, console);
      }
    }

    private static void ExitCommand()
    {
      Environment.Exit(0);
    }

    private static string HideCommand()
    {
      if (!MainWindow.IsShown)
      {
        return Variables.GeneralOkMsg;
      }

      MainWindow.Hide();

      return Variables.GeneralOkMsg;
    }

    private static string ShowCommand()
    {
      if (MainWindow.IsShown)
      {
        return Variables.GeneralOkMsg;
      }

      MainWindow.Show();

      return Variables.GeneralOkMsg;
    }

    private static string DownloadCommand(IList<string> commandParts)
    {
      if (commandParts.Count < 2)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      string downloadDirectoryPath = null;
      string downloadFileName = null;

      bool quickDownload;
      if ((quickDownload = (commandParts.Count == 2)) || Variables.KeywordDefault == commandParts[2])
      {
        downloadDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        downloadFileName = UrlToFileNameDropbox(commandParts[1]);
      }
      else if (commandParts.Count >= 3)
      {
        const string ev = Variables.EvaluateVariable;
        char evd = char.Parse(Variables.EvaluateVariableEnd);

        if (commandParts[2].Contains(ev))
        {
          var indexesOfCmdVariables = commandParts[2].AllIndexesOf(ev);

          int diff = 0;
          foreach (int index in indexesOfCmdVariables)
          {
            string variable = commandParts[2].Pacmanise(index + ev.Length - diff, evd);

            if (variable.Contains(" "))
            {
              ReportGeneralError(resources.InjectionErrorMessage);
              return null;
            }

            string unevaluatedVariable = ev + variable + evd;
            string evaluatedVariable = RunCommand($"echo {variable}").TrimEnd('\n');

            commandParts[2] = commandParts[2].Replace(unevaluatedVariable, evaluatedVariable);

            diff += unevaluatedVariable.Length - evaluatedVariable.Length;
          }
        }

        downloadDirectoryPath = Path.GetDirectoryName(commandParts[2]);
        downloadFileName = Path.GetFileName(commandParts[2]);
      }

      if (downloadDirectoryPath == null || downloadFileName == null)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      Directory.CreateDirectory(downloadDirectoryPath);

      // TODO: Use my FTP
      string downloadFilePath = Path.Combine(downloadDirectoryPath, downloadFileName);
      using (var wc = new WebClient())
      {
        try
        {
          wc.DownloadFile(new Uri(commandParts[1]), downloadFilePath);
        }
        catch (Exception exc)
        {
          return exc.Message;
        }
      }

      if (quickDownload)
      {
        Process.Start(downloadDirectoryPath);
      }

      return downloadFilePath;
    }

    private static string MessageCommand(IReadOnlyList<string> commandParts)
    {
      if (commandParts.Count < 2)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      SupportChatMessage = commandParts[1];
      ChatCommand = (commandParts.Count >= 3) ? commandParts[2] : ChatCommand;

      if (null == ChatboxWindow || ChatboxWindow.IsDisposed)
      {
        var chatboxThread = new Thread(OpenChatWindow) {IsBackground = true};
        chatboxThread.Start();

        while (null == ChatboxWindow || !ChatboxWindow.Visible)
        {
          Thread.Sleep(Variables.GeneralDelay);
        }

        ResurrectDeadThread(ref _chatThread, ServeMessages, _busyChatWise);
      }
      else
      {
        CloseChatWindow();
      }

      return $"{Variables.MessageCommand},{ChatboxWindow.Visible}";
    }

    private static string PowershellCommand(IReadOnlyList<string> commandParts)
    {
      if (commandParts.Count < 2)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      var commandBuilder = new StringBuilder(commandParts[1], Variables.IndividualValueLimit);

      for (int i = 2; i < commandParts.Count; ++i)
      {
        commandBuilder.Append($"; {commandParts[i]}");
      }

      commandBuilder.Replace(@"""", @"\""");

      return ExecuteCommand(commandBuilder.ToString(), ConsoleType.Powershell);
    }
  }
}
