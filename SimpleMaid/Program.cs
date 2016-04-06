using IniParser;
using IniParser.Model;
using Svetomech.Utilities;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using static Svetomech.Utilities.NativeMethods;
using static Svetomech.Utilities.SimpleApp;
using static Svetomech.Utilities.SimplePlatform;

namespace SimpleMaid
{
  internal static partial class Program
  {
    private static bool Hidden { get; set; } = false;
    private static string State { set { Console.Title = $"{Application.ProductName}: {value}"; } }

    private static DirectoryInfo desiredAppDirectory;
    private static FileInfo mainConfigFile;
    private static IntPtr mainWindowHandle;
    private static Mutex singleInstance;

    private static Thread connectionThread;
    private static Thread commandThread;
    private static Thread chatThread;
    private static volatile bool busyConnectionWise = true;
    private static volatile bool busyCommandWise = false;
    private static volatile bool busyChatWise = false;

    private static readonly bool runningWindows = (RunningPlatform() == Platform.Windows);
    private static volatile bool internetAlive = true;

    private static volatile string machineName;
    private static volatile string machinePassword;

    internal static frmChatWindow ChatboxWindow = null;
    internal static volatile bool ChatboxExit = false;
    internal static volatile string SupportChatMessage;
    internal static volatile string UserChatMessage;
    internal static volatile string ChatCommand;


    // TODO: Set&Get vs SetUntilSet&GetUntilGet - clarify use cases
    private static string Set(string tag, string value)
    {
      tag = $"{Application.ProductName}_{tag}";

      var encoding = new UTF8Encoding();
      byte[] requestBody = encoding.GetBytes($"tag={tag}&value={value}&fmt=html");

      var request = (HttpWebRequest)WebRequest.Create($"{Variables.ServerAddress}/storeavalue");
      request.Method = "POST";
      request.Credentials = Variables.AccountCredentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;
      request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";

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
      try { using (var response = request.GetResponse()) ;}
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
      request.Credentials = Variables.AccountCredentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;
      request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1";

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


    [STAThread]
    private static void Main(string[] args)
    {
      System.Windows.Forms.Application.EnableVisualStyles();
      Console.Clear();


      if (IsElevated())
      {
        reportGeneralError(resources.AdminErrorMessage);
        exit();
      }


      desiredAppDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), Application.CompanyName, Application.ProductName));
      mainConfigFile = new FileInfo(Path.Combine(desiredAppDirectory.FullName,
        (Variables.ConfigName != Variables.KeywordDefault) ? Variables.ConfigName : $"{Application.ProductName}.ini"));


      bool rogueArgFound = false;
      bool autorunArgFound = false;
      bool passArgFound = false; // <-- only need it for clarity
      bool langArgFound = false; // <-- only need it for clarity

      string passArg = Variables.DefaultPassword;
      string langArg = CultureInfo.InstalledUICulture.Name;

      if (args.Length >= 1)
      {
        int adjustedLength = args.Length;

        for (int i = 0; i < args.Length; ++i)
        {
          if (!rogueArgFound && (rogueArgFound = (args[i] == Variables.RogueArgument)))
            adjustedLength--;

          if (!autorunArgFound && (autorunArgFound = (args[i] == Variables.AutorunArgument)))
            adjustedLength--;

          if (adjustedLength >= 2)
          {
            if (!passArgFound && (args[i] == Variables.PasswordArgument))
            {
              passArg = (passArgFound = (i + 1 < adjustedLength)) ? args[i + 1] : passArg;

              if (passArgFound)
                adjustedLength -= 2;
              else
                adjustedLength--;
            }
            else if (!langArgFound && (args[i] == Variables.LanguageArgument))
            {
              langArg = (langArgFound = (i + 1 < adjustedLength)) ? args[i + 1] : langArg;

              if (langArgFound)
                adjustedLength -= 2;
              else
                adjustedLength--;
            }
          }
        }
      }


      mainWindowHandle = GetConsoleWindow();
      bool inDesiredDir = desiredAppDirectory.IsEqualTo(Application.StartupPath);
      if (inDesiredDir || rogueArgFound)
      {
        ShowWindow(mainWindowHandle, SW_HIDE);
        Program.Hidden = true;
      }


      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(langArg);


      // TODO: Move so it happens AFTER startup directory management
      singleInstance = new Mutex(false, "Local\\" + Application.AssemblyGuid);
      if (!singleInstance.WaitOne(0, false))
      {
        reportPastSelf();
        exit();
      }


      if (!inDesiredDir)
      {
        var svtFolder = new DirectoryInfo(ConfigurationManager.AppSettings["SvtFolderName"]);
        string[] filePaths = { Application.ExecutablePath, ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath,
          Assembly.GetAssembly(typeof(FileIniDataParser)).Location };
        try
        {
          var desiredAppSubdirectory = new DirectoryInfo(Path.Combine(desiredAppDirectory.FullName, svtFolder.Name));

          svtFolder.CopyTo(desiredAppSubdirectory, false);

          foreach (var filePath in filePaths)
          {
            File.Copy(filePath, Path.Combine(desiredAppDirectory.FullName, Path.GetFileName(filePath)), true);
          }
        }
        catch //unauthorized, io, notsupported
        {
          // 1. stop other instance using guid technique
          //   if there is no other instance running, (?recursive or ?continue)
          // 2. try to copy again
          //   if unlucky, (?recursive or ?continue)
          // 3. start that instance (?with a delay through .bat file)
          // 4. exit
        }
      }


      var config_parser = new FileIniDataParser();
      IniData configuration;

      bool   machineConfigured = false;
             machineName       = createMachine();
             machinePassword   = passArg;
      bool   autoRun           = autorunArgFound;

      bool firstRun;
      bool promptShown = false;
      if (firstRun = !mainConfigFile.Exists)
      {
        configuration = new IniData();
        configuration.Sections.AddSection("Service");


        configuration["Service"].AddKey("bMachineConfigured", machineConfigured.ToString());
        configuration["Service"].AddKey("sMachineName", machineName);

        validateMemoryPassword(ref configuration, ref promptShown);

        configuration["Service"].AddKey("bAutoRun", autoRun.ToString());
      }
      else
      {
        configuration = config_parser.ReadFile(mainConfigFile.FullName, Encoding.UTF8);


        machineConfigured = bool.Parse(configuration["Service"]["bMachineConfigured"]);

        if (isNameOK(configuration["Service"]["sMachineName"]))
        {
          machineName = configuration["Service"]["sMachineName"];
        }
        else
        {
          configuration["Service"]["sMachineName"] = machineName;
          machineConfigured = false;
        }

        if (!passArgFound)
        {
          validateConfigPassword(ref configuration, ref promptShown);
        }
        else
        {
          if (isPasswordOK(machinePassword))
          {
            configuration["Service"]["sMachinePassword"] = machinePassword;
          }
          else
          {
            validateConfigPassword(ref configuration, ref promptShown);
          }
        }

        if (!autorunArgFound)
        {
          autoRun = bool.Parse(configuration["Service"]["bAutoRun"]);
        }
        else
        {
          autoRun = !bool.Parse(configuration["Service"]["bAutoRun"]);
          configuration["Service"]["bAutoRun"] = autoRun.ToString();
        }
      }

      if (firstRun || !machineConfigured || autorunArgFound || passArgFound || promptShown)
      {
        if (!machineConfigured)
        {
          configureMachine();
          machineConfigured = true;
          configuration["Service"]["bMachineConfigured"] = machineConfigured.ToString();
        }

        config_parser.WriteFile(mainConfigFile.FullName, configuration, Encoding.UTF8);
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
      connectionThread.Start();

      commandThread = new Thread(awaitCommands);
      commandThread.IsBackground = true;
      // Starts in a different place

      chatThread = new Thread(serveMessages);
      chatThread.IsBackground = true;
      // Starts in a different place


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
        if (Get(machineName) == machinePassword)
        {
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

              string[] command_parts_fixed = new string[commandParts.Length - 1];
              for (int i = 1; i < commandParts.Length; ++i)
              {
                command_parts_fixed[i - 1] = commandParts[i];
              }

              bool isRepeatCommandSpecial = ($"{commandParts[0]}{sep}{command_parts_fixed[0]}" != remoteCommand);

              if (!isRepeatCommandSpecial)
              {
                executeCommand(command_parts_fixed[0]);
              }
              else
              {
                char specialRepeatCommandIdentifier = command_parts_fixed[0].ToCharArray()[0];

                switch (specialRepeatCommandIdentifier)
                {
                  case Variables.QuitCommand:
                    exitCommand();
                    break;

                  case Variables.PowershellCommand:
                    powershellCommand(command_parts_fixed);
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
