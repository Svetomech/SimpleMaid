﻿using Svetomech.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using static Svetomech.Utilities.NativeMethods;
using static Svetomech.Utilities.SimpleConsole;

namespace SimpleMaid
{
  internal static partial class Program
  {
    private static void exitCommand()
    {
      Environment.Exit(0);
    }

    private static string hideCommand()
    {
      if (Program.Hidden)
      {
        return Variables.GeneralOKMsg;
      }

      ShowWindow(mainWindowHandle, SW_HIDE);
      Program.Hidden = true;

      return Variables.GeneralOKMsg;
    }

    private static string showCommand()
    {
      if (!Program.Hidden)
      {
        return Variables.GeneralOKMsg;
      }


      ShowWindow(mainWindowHandle, SW_SHOW);
      Program.Hidden = false;

      return Variables.GeneralOKMsg;
    }

    private static string downloadCommand(string[] commandParts)
    {
      if (commandParts.Length < 2)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      string downloadDirectoryPath = null;
      string downloadFileName = null;

      bool quickDownload;
      if ((quickDownload = (commandParts.Length == 2)) || Variables.KeywordDefault == commandParts[2])
      {
        downloadDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        downloadFileName = urlToFileName(commandParts[1]);
      }
      else if (commandParts.Length >= 3)
      {
        string ev = Variables.EvaluateVariable;
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
              reportGeneralError(resources.InjectionErrorMessage);
              return null;
            }

            string unevaluatedVariable = ev + variable + evd;
            string evaluatedVariable = executeCommand($"echo {variable}");

            commandParts[2] = commandParts[2].Replace(unevaluatedVariable, evaluatedVariable);

            diff += unevaluatedVariable.Length - evaluatedVariable.Length;
          }
        }

        downloadDirectoryPath = Path.GetDirectoryName(commandParts[2]);
        downloadFileName = Path.GetFileName(commandParts[2]);
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

    private static string messageCommand(string[] commandParts)
    {
      if (null == ChatboxWindow || ChatboxWindow.IsDisposed)
      {
        var chatboxThread = new Thread(openChatWindow);
        chatboxThread.IsBackground = true;
        chatboxThread.Start();

        while (null == ChatboxWindow || !ChatboxWindow.Visible)
        {
          Thread.Sleep(Variables.GeneralDelay);
        }

        if (busyChatWise && chatThread != null && !chatThread.IsAlive)
        {
          chatThread = new Thread(serveMessages);
          chatThread.IsBackground = true;
          chatThread.Start();
        }
      }
      else
      {
        closeChatWindow();
      }

      return $"{Variables.MessageCommand},{ChatboxWindow.Visible}";
    }

    private static string powershellCommand(string[] commandParts)
    {
      if (!runningWindows)
      {
        return Variables.PowershellLinuxErrMsg;
      }

      if (commandParts.Length < 2)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      for (int i = 2; i < commandParts.Length; ++i)
      {
        commandParts[1] += "; " + commandParts[i];
      }
      commandParts[1] = commandParts[1].Replace(@"""", @"\""");

      return executeCommand(commandParts[1], ConsoleTypes.Powershell);
    }

    private static string executeCommand(string command, ConsoleTypes console = ConsoleTypes.None)
    {
      if (ConsoleTypes.None == console)
      {
        return runningWindows ? ExecuteCommand(command, ConsoleTypes.CMD) : ExecuteCommand(command, ConsoleTypes.Bash);
      }
      else
      {
        return ExecuteCommand(command, console);
      }
    }
  }
}