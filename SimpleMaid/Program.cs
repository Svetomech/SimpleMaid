using Svetomech.Utilities;
using Svetomech.Utilities.Types;
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
        Console.Title = $"{ConsoleApplication.ProductName}: {value}";
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

    internal static DirectoryInfo DesiredAppDirectory;
    internal static MainConfiguration MainConfig;
    internal static Window MainWindow;
    internal static Mutex SingleInstance;

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
      if (App.IsElevated())
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

      // Setting application file names
      string desiredAppName = ConsoleApplication.ProductName;
      string realAppName = Path.GetFileName(ConsoleApplication.ExecutablePath);

      // Generate app directory path in a cross-platform way
      DesiredAppDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), ConsoleApplication.CompanyName, desiredAppName));

      // Initialize main config file based on app directory
      MainConfig = new MainConfiguration(Path.Combine(DesiredAppDirectory.FullName,
        (Variables.ConfigName != Variables.KeywordDefault) ? Variables.ConfigName : $"{desiredAppName}.ini"));

      // Don't show main window if app was autorun
      MainWindow = NativeMethods.GetConsoleWindow();
      bool inDesiredDir = DesiredAppDirectory.IsEqualTo(ConsoleApplication.StartupPath);
      if (inDesiredDir || rogueArg.Found)
      {
        MainWindow.Hide();
      }

      // Localize app strings according to resources.xx
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(langArg.Value);

      // Close app if there is another instance running
      SingleInstance = new Mutex(false, $@"Local\{ConsoleApplication.AssemblyGuid}");
      if (!SingleInstance.WaitOne(0, false))
      {
        reportPastSelf();
        exit();
      }

      // Copy files required for app to run locally
      if (!inDesiredDir && MainConfig.AutoRun)
      {
        string[] filePaths = { ConsoleApplication.ExecutablePath, MainConfig.ParserLocation,
          ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath };

        var langFolder = new DirectoryInfo(ConfigurationManager.AppSettings[Variables.LangFolderKey]);
        var desiredAppSubdirectory = new DirectoryInfo(Path.Combine(DesiredAppDirectory.FullName, langFolder.Name));

        try
        {
          foreach (var filePath in filePaths)
          {
            File.Copy(filePath, Path.Combine(DesiredAppDirectory.FullName, Path.GetFileName(filePath)), true);
          }

          langFolder.CopyTo(desiredAppSubdirectory, false);
        }
        catch (Exception exc)
        {
          reportGeneralError(exc.Message);
          exit();
        }
      }


      bool firstRun = !MainConfig.ExistsLocally;
      //bool promptShown = false;
      if (firstRun)
      {
        //
        //
        MainConfig.MachinePassword = passArg;
        MainConfig.AutoRun = autorunArgFound;
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
          MainConfig.AutoRun = !MainConfig.AutoRun;
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

      // Add or remove autorun entry if required
      if (MainConfig.AutoRun)
      {
        App.SwitchAutorun(desiredAppName, Path.Combine(DesiredAppDirectory.FullName, realAppName), true);
      }
      else
      {
        App.SwitchAutorun(desiredAppName);
      }

      // Initialize & start main thread
      var timeThread = new Thread(sendMachineTime);
      timeThread.IsBackground = true;
      timeThread.Start();

      Thread.Sleep(Variables.GeneralDelay); // needed

      // Initialize & start connection thread
      connectionThread = new Thread(handleConnection);
      connectionThread.IsBackground = true;
      busyConnectionWise = true;
      connectionThread.Start();

      // Initialize command thread
      commandThread = new Thread(awaitCommands);
      commandThread.IsBackground = true;
      busyCommandWise = false;

      // Initialize chat thread
      chatThread = new Thread(serveMessages);
      chatThread.IsBackground = true;
      busyChatWise = false;

      // Do not close app - go on in main thread
      timeThread.Join();

      /*              |
                      |
                      V
       */
    }

    private static void sendMachineTime()
    {
      reportThreadStart(resources.TimeStart);

      while (true)
      {
        var now = DateTime.Now;

        if (resources.WebErrorMessage != set($"time.{MainConfig.MachineName}",
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
        bool remotePasswordAccepted = get(MainConfig.MachineName) == MainConfig.MachinePassword;
        bool localCommandSupplied = !String.IsNullOrWhiteSpace(MainConfig.LoginCommand);

        if (remotePasswordAccepted || localCommandSupplied)
        {
          MainConfig.LoginCommand = remotePasswordAccepted ? String.Empty : MainConfig.LoginCommand;

          busyCommandWise = !busyCommandWise;
          SetUntilSet(MainConfig.MachineName, String.Empty);

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

        remoteCommand = GetUntilGet($"commands.{MainConfig.MachineName}");

        if (String.Empty == remoteCommand || remoteCommand.StartsWith(ans))
        {
          continue;
        }

        string[] commandParts = remoteCommand.Split(new char[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        bool isCommandSpecial = commandParts?[0] != remoteCommand;

        if (isCommandSpecial)
        {
          char? specialCommandIdentifier = commandParts?[0]?[0];

          bool localCommandSupplied = !String.IsNullOrWhiteSpace(MainConfig.LoginCommand);
          if (localCommandSupplied)
          {
            specialCommandIdentifier = Variables.RepeatCommand;
          }

          switch (specialCommandIdentifier)
          {
            case Variables.QuitCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + Variables.GeneralOKMsg);
              exitCommand();
              break;

            case Variables.HideCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + hideCommand());
              break;

            case Variables.ShowCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + showCommand());
              break;

            case Variables.DownloadCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + downloadCommand(commandParts));
              break;

            case Variables.MessageCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + messageCommand(commandParts));
              break;

            case Variables.PowershellCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + powershellCommand(commandParts));
              break;

            case Variables.RepeatCommand:
              if (commandParts.Length < 2)
              {
                goto default;
              }

              if (!localCommandSupplied)
              {
                MainConfig.LoginCommand = remoteCommand;
              }
              else if (remoteCommand != MainConfig.LoginCommand)
              {
                goto default;
              }

              string[] repeatCommandParts = new string[commandParts.Length - 1];
              for (int i = 1; i < commandParts.Length; ++i)
              {
                repeatCommandParts[i - 1] = commandParts[i];
              }

              bool isRepeatCommandSpecial = $"{commandParts?[0]}{sep}{repeatCommandParts?[0]}" != remoteCommand;

              if (isRepeatCommandSpecial)
              {
                char? specialRepeatCommandIdentifier = repeatCommandParts?[0]?[0];

                switch (specialRepeatCommandIdentifier)
                {
                  case Variables.QuitCommand:
                    exitCommand();
                    break;

                  case Variables.PowershellCommand:
                    powershellCommand(repeatCommandParts);
                    break;

                  default:
                    reportGeneralError(resources.WrongCommandErrMsg + remoteCommand);
                    break;
                }
              }
              else
              {
                executeCommand(repeatCommandParts?[0]);
              }
              break;

            default:
              reportGeneralError(resources.WrongCommandErrMsg + remoteCommand);
              break;
          }
        }
        else
        {
          SetUntilSet($"commands.{MainConfig.MachineName}", ans + executeCommand(commandParts?[0]));
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

        remoteMessage = GetUntilGet($"messages.{MainConfig.MachineName}");

        if (!String.IsNullOrWhiteSpace(UserChatMessage))
        {
          SetUntilSet($"messages.{MainConfig.MachineName}", ans + UserChatMessage);
          UserChatMessage = null;
        }

        if (remoteMessage == previousRemoteMessage || String.Empty == remoteMessage || remoteMessage.StartsWith(ans))
        {
          continue;
        }

        previousRemoteMessage = remoteMessage;

        string[] messageParts = remoteMessage.Split(new char[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        if (messageParts.Length < 1)
        {
          continue;
        }

        SupportChatMessage = messageParts?[0];
        ChatCommand = (messageParts.Length >= 2) ? messageParts?[1] : ChatCommand;
      }

      reportThreadStop(resources.ChatStop);
    }
  }
}
