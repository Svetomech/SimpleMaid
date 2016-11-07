using Svetomech.Utilities;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using static Svetomech.Utilities.SimplePlatform;

namespace SimpleMaid
{
  internal static partial class Program
  {
    public static string State
    {
      set
      {
        Console.Title = $"{Application.ProductName}: {value}";
      }

      get
      {
        return Console.Title.Substring(Console.Title.IndexOf(':') + 2);
      }
    }

    internal static frmChatWindow ChatboxWindow;
    internal static bool ChatboxExit;
    internal static string SupportChatMessage;
    internal static string UserChatMessage;
    internal static string ChatCommand;

    private static DirectoryInfo desiredAppDirectory;
    private static MainConfiguration mainConfig;
    private static Window mainWindow;
    private static Mutex singleInstance;

    private static Thread connectionThread;
    private static Thread commandThread;
    private static Thread chatThread;
    private static bool busyConnectionWise;
    private static bool busyCommandWise;
    private static bool busyChatWise;

    private static readonly bool runningWindows = (RunningPlatform() == Platform.Windows);
    private static volatile bool internetAlive = true;

    private static void Main(string[] args)
    {
      // Some initialisation work
      Console.Clear();
      System.Windows.Forms.Application.EnableVisualStyles();
      ChatboxWindow = null;
      ChatboxExit = false;

      // Forbid executing as admin/root
      if (SimpleApp.IsElevated())
      {
        reportGeneralError(resources.AdminErrorMessage);
        exit();
      }

      // Highly optimised console arguments' searching
      var rogueArg = new ConsoleArgument(false);
      var autorunArg = new ConsoleArgument(false);
      var langArg = new ConsoleArgument(false, CultureInfo.InstalledUICulture.Name);
      var passArg = new ConsoleArgument(false, Variables.DefaultPassword);

      if (args.Length >= 1)
      {
        int adjustedLength = args.Length;

        for (int i = 0; i < args.Length; ++i)
        {
          if (!rogueArg.Found && (rogueArg.Found = (args[i] == Variables.RogueArgument)))
          {
            adjustedLength--;
          }

          if (!autorunArg.Found && (autorunArg.Found = (args[i] == Variables.AutorunArgument)))
          {
            adjustedLength--;
          }

          if (adjustedLength < 2)
          {
            break;
          }

          if (!langArg.Found && (args[i] == Variables.LanguageArgument))
          {
            langArg.Value = (langArg.Found = (i + 1 < adjustedLength)) ? args[i + 1] : langArg.Value;

            adjustedLength -= langArg.Found ? 2 : 1;
          }
          else if (!passArg.Found && (args[i] == Variables.PasswordArgument))
          {
            passArg.Value = (passArg.Found = (i + 1 < adjustedLength)) ? args[i + 1] : passArg.Value;

            adjustedLength -= passArg.Found ? 2 : 1;
          }
        }
      }

      // Generate app directory path in a cross-platform way
      desiredAppDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), Application.CompanyName, Application.ProductName));

      // Initialize main config file based on app directory
      mainConfig = new MainConfiguration(Path.Combine(desiredAppDirectory.FullName,
        (Variables.ConfigName != Variables.KeywordDefault) ? Variables.ConfigName : $"{Application.ProductName}.ini"));

      // Don't show main window if app was autorun
      mainWindow = NativeMethods.GetConsoleWindow();
      bool inDesiredDir = desiredAppDirectory.IsEqualTo(Application.StartupPath);
      if (inDesiredDir || rogueArg.Found)
      {
        mainWindow.Hide();
      }

      // Localize app strings according to resources.xx
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(langArg.Value);

      // TODO: Move so it happens AFTER startup directory management
      // Close app if there is another instance running
      singleInstance = new Mutex(false, "Local\\" + Application.AssemblyGuid);
      if (!singleInstance.WaitOne(0, false))
      {
        reportPastSelf();
        exit();
      }

      // TODO: Only do it once per version
      // TODO: Handle exceptions
      // Copy files required for app to run locally
      if (!inDesiredDir)
      {
        string[] filePaths = { Application.ExecutablePath, ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath,
          mainConfig.ParserLocation };

        var langFolder = new DirectoryInfo(ConfigurationManager.AppSettings[Variables.LangFolderKey]);

        try
        {
          foreach (var filePath in filePaths)
          {
            File.Copy(filePath, Path.Combine(desiredAppDirectory.FullName, Path.GetFileName(filePath)), true);
          }

          var desiredAppSubdirectory = new DirectoryInfo(Path.Combine(desiredAppDirectory.FullName, langFolder.Name));

          langFolder.CopyTo(desiredAppSubdirectory, false);
        }
        catch // unauthorized, io, notsupported
        {
          // 1. stop other instance using guid technique
          //   if there is no other instance running, (?recursive or ?continue)
          // 2. try to copy again
          //   if unlucky, (?recursive or ?continue)
          // 3. start that instance (?with a delay through .bat file)
          // 4. exit
        }
      }


      bool firstRun;
      //bool promptShown = false;
      if (firstRun = !mainConfig.ExistsLocally)
      {
        //
        //
        mainConfig.machinePassword = passArg;
        mainConfig.autoRun = autorunArgFound;
        //
      }
      else
      {
        if (!passArgFound)
        {
          validateConfigPassword(ref mainConfigData, ref promptShown);
        }
        else
        {
          if (isPasswordOK(machinePassword))
          {
            mainConfigData["Service"]["sMachinePassword"] = machinePassword;
          }
          else
          {
            validateConfigPassword(ref mainConfigData, ref promptShown);
          }
        }

        if (autorunArgFound)
        {
          mainConfig.autoRun = !mainConfig.autoRun;
        }
      }

      if (firstRun || !machineConfigured || autorunArgFound || passArgFound || promptShown)
      {
        if (!machineConfigured)
        {
          configureMachine();
          machineConfigured = true;
          mainConfigData["Service"]["bMachineConfigured"] = machineConfigured.ToString();
        }

        mainConfigParser.WriteFile(mainConfigFile.FullName, mainConfigData, Encoding.UTF8);
      }


      if (runningWindows)
      {
        if (autoRun)
        {
          SwitchAutorun(Application.ProductName, Path.Combine(desiredAppDirectory.FullName,
            Path.GetFileName(Application.ExecutablePath)));
        }
        else
        {
          SwitchAutorun(Application.ProductName);
        }
      }


      var timeThread = new Thread(sendMachineTime);
      timeThread.IsBackground = true;
      timeThread.Start();
      Thread.Sleep(1000);

      connectionThread = new Thread(handleConnection);
      connectionThread.IsBackground = true;
      busyConnectionWise = true;
      connectionThread.Start();

      commandThread = new Thread(awaitCommands);
      commandThread.IsBackground = true;
      busyCommandWise = false;

      chatThread = new Thread(serveMessages);
      chatThread.IsBackground = true;
      busyChatWise = false;


      timeThread.Join();
    }


    private static void sendMachineTime()
    {
      reportThreadStart(resources.TimeStart);

      while (true)
      {
        var now = DateTime.Now;

        if (resources.WebErrorMessage != Set($"time.{machineName}" /* PUN NOT INTENDED */,
          $"{now.ToShortDateString()} {now.ToLongTimeString()}"))
        {
          if (!internetAlive)
          {
            internetAlive = true;
            resurrectDeadThreads();
          }
        }

        Thread.Sleep(Variables.GeneralDelay - now.Millisecond);
      }
    }

    private static void handleConnection()
    {
      reportThreadStart(resources.ConnectionStart);

      while (busyConnectionWise && internetAlive)
      {
        bool logonManually = Get(machineName) == machinePassword;

        if (logonManually || !String.IsNullOrWhiteSpace(mainConfigData["Service"]["sLogonCommand"]))
        {
          if (logonManually)
          {
            mainConfigData["Service"]["sLogonCommand"] = String.Empty;
            mainConfigParser.WriteFile(mainConfigFile.FullName, mainConfigData, Encoding.UTF8);
          }

          busyCommandWise = !busyCommandWise;
          SetUntilSet(machineName, String.Empty);

          resurrectDeadThread(ref commandThread, awaitCommands, busyCommandWise);
        }

        Thread.Sleep(Variables.GeneralDelay);
      }

      reportThreadStop(resources.ConnectionStop);
    }

    private static void awaitCommands()
    {
      reportThreadStart(resources.CommandStart);

      string ans = Variables.AnswerPrefix;
      char sep = Variables.CommandsSeparator;

      string remoteCommand = null;

      while (busyCommandWise && internetAlive)
      {
        if (remoteCommand != null)
        {
          Thread.Sleep(Variables.GeneralDelay);
        }

        remoteCommand = GetUntilGet("commands." + machineName);

        if (String.Empty == remoteCommand || remoteCommand.StartsWith(ans))
        {
          continue;
        }

        string[] commandParts = remoteCommand.Split(new char[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        bool isCommandSpecial = (commandParts[0] != remoteCommand);

        if (isCommandSpecial)
        {
          char specialCommandIdentifier = commandParts[0].ToCharArray()[0];

          if (!String.IsNullOrWhiteSpace(mainConfigData["Service"]["sLogonCommand"]))
          {
            specialCommandIdentifier = Variables.RepeatCommand;
          }

          switch (specialCommandIdentifier)
          {
            case Variables.QuitCommand:
              SetUntilSet("commands." + machineName, ans + Variables.GeneralOKMsg);
              exitCommand();
              break;

            case Variables.HideCommand:
              SetUntilSet("commands." + machineName, ans + hideCommand());
              break;

            case Variables.ShowCommand:
              SetUntilSet("commands." + machineName, ans + showCommand());
              break;

            case Variables.DownloadCommand:
              SetUntilSet("commands." + machineName, ans + downloadCommand(commandParts));
              break;

            case Variables.MessageCommand:
              SetUntilSet("commands." + machineName, ans + messageCommand(commandParts));
              break;

            case Variables.PowershellCommand:
              SetUntilSet("commands." + machineName, ans + powershellCommand(commandParts));
              break;

            case Variables.RepeatCommand:
              if (commandParts.Length < 2)
                goto default;

              if (String.IsNullOrWhiteSpace(mainConfigData["Service"]["sLogonCommand"]))
              {
                mainConfigData["Service"]["sLogonCommand"] = remoteCommand;
                mainConfigParser.WriteFile(mainConfigFile.FullName, mainConfigData, Encoding.UTF8);
              }
              else if (remoteCommand != mainConfigData["Service"]["sLogonCommand"])
              {
                goto default;
                //////////////////////////////////////////////////////////////////////////////
                ////////////////////////////////////
                /////////////////////////////////////////////////////////////////////////
                ///////////////////////////////////////
                /////////////////////////////////////////////////////////////////////////////////
                ///////////////////////////////////////////////////
                //////////////////////////////////////////////////////////////////////////////////
              }

              string[] commandPartsFixed = new string[commandParts.Length - 1];
              for (int i = 1; i < commandParts.Length; ++i)
              {
                commandPartsFixed[i - 1] = commandParts[i];
              }

              bool isRepeatCommandSpecial = ($"{commandParts[0]}{sep}{commandPartsFixed[0]}" != remoteCommand);

              if (!isRepeatCommandSpecial)
              {
                executeCommand(commandPartsFixed[0]);
              }
              else
              {
                char specialRepeatCommandIdentifier = commandPartsFixed[0].ToCharArray()[0];

                switch (specialRepeatCommandIdentifier)
                {
                  case Variables.QuitCommand:
                    exitCommand();
                    break;

                  case Variables.PowershellCommand:
                    powershellCommand(commandPartsFixed);
                    break;

                  default:
                    reportGeneralError(resources.WrongCommandErrMsg + remoteCommand);
                    break;
                }
              }
              break;

            default:
              reportGeneralError(resources.WrongCommandErrMsg + remoteCommand);
              break;
          }
        }
        else
        {
          SetUntilSet("commands." + machineName, ans + executeCommand(commandParts[0]));
        }
      }

      reportThreadStop(resources.CommandStop);
    }

    // TODO: Resolve colission - user and support sending messages simultaneously
    private static void serveMessages()
    {
      reportThreadStart(resources.ChatStart);

      string ans = Variables.AnswerPrefix;
      char sep = Variables.CommandsSeparator;

      string remoteMessage = null;
      string previousRemoteMessage = null;

      while (busyChatWise && internetAlive)
      {
        if (remoteMessage != null)
        {
          Thread.Sleep(Variables.GeneralDelay);
        }

        remoteMessage = GetUntilGet("messages." + machineName);

        if (!String.IsNullOrWhiteSpace(UserChatMessage))
        {
          SetUntilSet("messages." + machineName, ans + UserChatMessage);
          UserChatMessage = null;
        }

        if (remoteMessage == previousRemoteMessage || String.Empty == remoteMessage || remoteMessage.StartsWith(ans))
        {
          continue;
        }

        previousRemoteMessage = remoteMessage;

        // TODO: частный случай m, ещё две то есть
        // TODO: ChatCommand = message_aprts[1];
        #region Parsing message
        string[] messageParts = remoteMessage.Split(new char[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        SupportChatMessage = messageParts[0];
        #endregion
      }

      reportThreadStop(resources.ChatStop);
    }
  }
}
