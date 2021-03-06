using Svetomech.Utilities;
using Svetomech.Utilities.Types;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Threading;
using static Svetomech.Utilities.SimplePlatform;

namespace SimpleMaid
{
  internal static partial class Program
  {
    internal static DirectoryInfo DesiredAppDirectory;
    internal static MainConfiguration MainConfig;
    internal static Window MainWindow;

    internal static FrmChatWindow ChatboxWindow;
    internal static bool ChatboxExit;
    internal static string SupportChatMessage;
    internal static string UserChatMessage;
    internal static string ChatCommand;

    private static readonly bool RunningWindows = (RunningPlatform() == Platform.Windows);
    private static volatile bool _internetAlive = true;
    private static Mutex _singleInstance;

    private static Thread _connectionThread;
    private static Thread _commandThread;
    private static Thread _chatThread;
    private static bool _busyConnectionWise;
    private static bool _busyCommandWise;
    private static bool _busyChatWise;

    internal static void AddToTitle(string value)
    {
      Title = Title.Insert(Title.IndexOf(' ') + 1, value + " ");
    }

    private static string Title
    {
      set
      {
        Console.Title = $@"{ConsoleApplication.ProductName}: {value} ";
      }

      get
      {
        return Console.Title.Substring(Console.Title.IndexOf(':') + 2);
      }
    }

    private static void Main(string[] args)
    {
      // Highly optimised console arguments' searching
      var rogueArg = new ConsoleArgument();
      var autorunArg = new ConsoleArgument();
      var langArg = new ConsoleArgument(CultureInfo.InstalledUICulture.Name);
      var passArg = new ConsoleArgument();

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
            // ReSharper disable once AssignmentInConditionalExpression
            langArg.Value = (langArg.Found = i + 1 < adjustedLength) ? args[i + 1] : langArg.Value;

            adjustedLength -= langArg.Found ? 2 : 1;
            i += langArg.Found ? 1 : 0;
          }
          else if (!passArg.Found && (args[i] == Variables.PasswordArgument))
          {
            // ReSharper disable once AssignmentInConditionalExpression
            passArg.Value = (passArg.Found = i + 1 < adjustedLength) ? args[i + 1] : passArg.Value;

            adjustedLength -= passArg.Found ? 2 : 1;
            i += passArg.Found ? 1 : 0;
          }
        }
      }

      // Localize app strings according to resources.xx
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(langArg.Value);

      // Some initialisation work
      Console.Clear();
      System.Windows.Forms.Application.EnableVisualStyles();
      MainWindow = NativeMethods.GetConsoleWindow();
      ChatboxWindow = null;
      ChatboxExit = false;
      Title = resources.MainWindowTitle;

      // Forbid executing as admin/root
      if (Variables.ForbidElevatedExecution && App.IsElevated())
      {
        ReportGeneralError(resources.AdminErrorMessage);
        Exit();
      }

      // Setting application file names
      string desiredAppName = ConsoleApplication.ProductName;
      string realAppName = Path.GetFileName(ConsoleApplication.ExecutablePath);

      // Generate app directory path in a cross-platform way
      DesiredAppDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), ConsoleApplication.CompanyName, desiredAppName));
      DesiredAppDirectory.Create();

      // Initialize main config file based on app directory
      MainConfig = new MainConfiguration(Path.Combine(DesiredAppDirectory.FullName,
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (Variables.ConfigName != Variables.KeywordDefault) ? Variables.ConfigName : $"{desiredAppName}.ini"));

      // Close app if there is another instance running
      _singleInstance = new Mutex(false, $@"Local\{ConsoleApplication.AssemblyGuid}");
      if (!_singleInstance.WaitOne(0, false))
      {
        ReportPastSelf();
        Exit();
      }

      // Read main config file, pass arguments, save it
      try
      {
        MainConfig.Load();

        MainConfig.AutoRun = autorunArg.Found ? !MainConfig.AutoRun : MainConfig.AutoRun;
        MainConfig.MachinePassword = passArg.Found ? passArg.Value : MainConfig.MachinePassword;

        MainConfig.Save();
      }
      catch (Exception exc)
      {
        ReportGeneralError(exc.Message);
        Exit();
      }

      // Don't show main window if app was autorun
      bool inDesiredDir = DesiredAppDirectory.IsEqualTo(ConsoleApplication.StartupPath);
      if (inDesiredDir || rogueArg.Found)
      {
        MainWindow.Hide();
      }

      // Copy files required for app to run locally
      if (MainConfig.AutoRun && !inDesiredDir)
      {
        string[] filePaths = { ConsoleApplication.ExecutablePath, MainConfig.ParserLocation, MainConfig.UtilitiesLocation,
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
          ReportGeneralError(exc.Message);
          Exit();
        }
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
      var timeThread = new Thread(SendMachineTime) {IsBackground = true};
      timeThread.Start();

      Thread.Sleep(Variables.GeneralDelay);

      // Initialize & start connection thread
      _connectionThread = new Thread(HandleConnection) {IsBackground = true};
      _busyConnectionWise = true;
      _connectionThread.Start();

      // Initialize command thread
      _commandThread = new Thread(AwaitCommands) {IsBackground = true};
      _busyCommandWise = false;

      // Initialize chat thread
      _chatThread = new Thread(ServeMessages) {IsBackground = true};
      _busyChatWise = false;

      // Do not close app - go on in main thread
      timeThread.Join();

      /*              |
                      |
                      V
       */
    }

    private static void SendMachineTime()
    {
      ReportThreadStart(resources.TimeStart);

      while (true)
      {
        var now = DateTime.Now;

        if (resources.WebErrorMessage != Set($"time.{MainConfig.MachineName}",
          $"{now.ToShortDateString()} {now.ToLongTimeString()}"))
        {
          if (!_internetAlive)
          {
            _internetAlive = true;

            var flags   = new bool[]        { _busyConnectionWise, _busyCommandWise, _busyChatWise };
            var threads = new Thread[]      { _connectionThread,   _commandThread,   _chatThread };
            var starts  = new ThreadStart[] { HandleConnection,    AwaitCommands,    ServeMessages };

            for (int i = 0; i < threads.Length; ++i)
            {
              if (!flags[i]) continue;

              if (threads[i].IsAlive) threads[i].Join();

              threads[i] = new Thread(starts[i]) { IsBackground = true };
              threads[i].Start();
            }
          }
        }

        Thread.Sleep(Variables.GeneralDelay - now.Millisecond);
      }
      // ReSharper disable once FunctionNeverReturns
    }

    private static void HandleConnection()
    {
      ReportThreadStart(resources.ConnectionStart);

      while (_busyConnectionWise && _internetAlive)
      {
        bool remotePasswordAccepted = Get(MainConfig.MachineName) == MainConfig.MachinePassword;
        bool localCommandSupplied = !String.IsNullOrWhiteSpace(MainConfig.LoginCommand);

        if (remotePasswordAccepted)
        {
          MainConfig.LoginCommand = String.Empty;
          MainConfig.Save();

          _busyCommandWise = !_busyCommandWise;
          SetUntilSet(MainConfig.MachineName, String.Empty);

          if (_busyCommandWise)
          {
            if (_commandThread.IsAlive) _commandThread.Join();

            _commandThread = new Thread(AwaitCommands) { IsBackground = true };
            _commandThread.Start();
          }
        }
        else if (localCommandSupplied)
        {
          _busyCommandWise = true;

          if (!_commandThread.IsAlive)
          {
            _commandThread = new Thread(AwaitCommands) { IsBackground = true };
            _commandThread.Start();
          }
        }

        Thread.Sleep(Variables.GeneralDelay);
      }

      ReportThreadStop(resources.ConnectionStop);
    }

    private static void AwaitCommands()
    {
      ReportThreadStart(resources.CommandStart);

      const string ans = Variables.AnswerPrefix;
      const char sep = Variables.CommandsSeparator;

      string remoteCommand = null;

      while (_busyCommandWise && _internetAlive)
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

        string[] commandParts = remoteCommand.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        bool isCommandSpecial = commandParts[0] != remoteCommand;

        if (isCommandSpecial)
        {
          char? specialCommandIdentifier = commandParts[0]?[0];

          bool localCommandSupplied = !String.IsNullOrWhiteSpace(MainConfig.LoginCommand);
          if (localCommandSupplied)
          {
            specialCommandIdentifier = Variables.RepeatCommand;
          }

          switch (specialCommandIdentifier)
          {
            case Variables.QuitCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + Variables.GeneralOkMsg);
              ExitCommand();
              break;

            case Variables.HideCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + HideCommand());
              break;

            case Variables.ShowCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + ShowCommand());
              break;

            case Variables.DownloadCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + DownloadCommand(commandParts));
              break;

            case Variables.MessageCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + MessageCommand(commandParts));
              break;

            case Variables.PowershellCommand:
              SetUntilSet($"commands.{MainConfig.MachineName}", ans + PowershellCommand(commandParts));
              break;

            case Variables.RepeatCommand:
              if (commandParts.Length < 2)
              {
                goto default;
              }

              if (!localCommandSupplied)
              {
                MainConfig.LoginCommand = remoteCommand;
                MainConfig.Save();
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

              bool isRepeatCommandSpecial = $"{commandParts[0]}{sep}{repeatCommandParts[0]}" != remoteCommand;

              if (isRepeatCommandSpecial)
              {
                char? specialRepeatCommandIdentifier = repeatCommandParts[0]?[0];

                switch (specialRepeatCommandIdentifier)
                {
                  case Variables.QuitCommand:
                    ExitCommand();
                    break;

                  case Variables.PowershellCommand:
                    PowershellCommand(repeatCommandParts);
                    break;

                  default:
                    ReportGeneralError(resources.WrongCommandErrMsg + remoteCommand);
                    break;
                }
              }
              else
              {
                RunCommand(repeatCommandParts[0]);
              }
              break;

            default:
              ReportGeneralError(resources.WrongCommandErrMsg + remoteCommand);
              break;
          }
        }
        else
        {
          SetUntilSet($"commands.{MainConfig.MachineName}", ans + RunCommand(commandParts[0]));
        }
      }

      ReportThreadStop(resources.CommandStop);
    }

    // TODO: Resolve colission - user and support sending messages simultaneously
    private static void ServeMessages()
    {
      ReportThreadStart(resources.ChatStart);

      const string ans = Variables.AnswerPrefix;
      const char sep = Variables.CommandsSeparator;

      string remoteMessage = null;
      string previousRemoteMessage = null;

      while (_busyChatWise && _internetAlive)
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

        string[] messageParts = remoteMessage.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        if (messageParts.Length < 1)
        {
          continue;
        }

        SupportChatMessage = messageParts[0];
        ChatCommand = (messageParts.Length >= 2) ? messageParts[1] : ChatCommand;
      }

      ReportThreadStop(resources.ChatStop);
    }
  }
}
