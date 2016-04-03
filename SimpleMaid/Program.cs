using IniParser;
using IniParser.Model;
using Svetomech.Utilities;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static Svetomech.Utilities.NativeMethods;
using static Svetomech.Utilities.PasswordStrength;
using static Svetomech.Utilities.SimpleApp;
using static Svetomech.Utilities.SimpleConsole;
using static Svetomech.Utilities.SimplePlatform;

namespace SimpleMaid
{
  internal class Program
  {
    #region Properties
    public static bool Hidden { get; set; } = false;
    public static string State { set { Console.Title = $"{Application.ProductName}: {value}"; } }
    #endregion

    private static DirectoryInfo desiredAppDirectory;
    private static FileInfo mainConfigFile;
    private static IntPtr handle;
    private static Mutex  programMutex;

    #region Global threads
    private static Thread connectionThread;
    private static Thread commandThread;
    private static Thread chatThread;

    private static volatile bool busyCommandWise = false;
    private static volatile bool busyChatWise = false;
    #endregion
    private static readonly bool runningWindows =
      (RunningPlatform() == Platform.Windows);
    private static volatile bool internetAlive = true;

    #region Global settings
    // TODO: Get rid of these
    private static volatile string machine;
    private static volatile string pass;
    #endregion

    #region Across forms
    public static frmChatWindow ChatboxWindow = null;
    public static volatile bool ChatboxExit = false;
    public static volatile string SupportChatMessage;
    public static volatile string UserChatMessage;
    public static volatile string ChatCommand;
    #endregion


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

      Console.BackgroundColor = ConsoleColor.Black;
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

      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.DarkGreen;
      Console.WriteLine($"GET  {tag}  {value}\n");

      return value;
    }

    private static void SetUntilSet(string tag, string value)
    {
      while (resources.WebErrorMessage == Set(tag, value))
      {
        Thread.Sleep(Variables.GeneralDelay);
      }
    }

    private static string GetUntilGet(string tag)
    {
      string value;

      while (resources.WebErrorMessage == (value = Get(tag)))
      {
        Thread.Sleep(Variables.GeneralDelay);
      }

      return value;
    }


    [STAThread]
    static void Main(string[] args)
    {
      System.Windows.Forms.Application.EnableVisualStyles();
      Console.Clear();

      #region Exit (if running as admin/root)
      if (IsElevated())
      {
        reportGeneralError(resources.AdminErrorMessage);
        exit();
      }
      #endregion

      desiredAppDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Application.CompanyName, Application.ProductName));
      mainConfigFile = new FileInfo(Path.Combine(desiredAppDirectory.FullName, Variables.ConfigName));

      #region Console arguments
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
      #endregion

      #region Hide window (if autorun)
      handle = GetConsoleWindow();
      bool inDesiredDir = desiredAppDirectory.IsEqualTo(Application.StartupPath);
      if (inDesiredDir || rogueArgFound)
      {
        ShowWindow(handle, SW_HIDE);
        Program.Hidden = true;
      }
      #endregion

      // Localization
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(langArg);

      // TODO: Move so it happens AFTER startup directory management
      #region Exit (if already running)
      programMutex = new Mutex(false, "Local\\" + Application.AssemblyGuid);
      if (!programMutex.WaitOne(0, false))
      {
        reportPastSelf();
        exit();
      }
      #endregion

      #region Startup directory management
      if (!inDesiredDir)
      {
        var svtFolder = new DirectoryInfo(ConfigurationManager.AppSettings["SvtFolderName"]);
        string[] filePaths = { Application.ExecutablePath, ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath,
          Assembly.GetAssembly(typeof(FileIniDataParser)).Location };
        try
        {
          svtFolder.CopyTo(desiredAppDirectory, false);
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
      #endregion

      #region Necessary INI declarations
      var config_parser = new FileIniDataParser();
      IniData configuration;
      #endregion

      #region Settings to read
      bool   machineConfigured = false;
      string machineName       = createMachine();
      string machinePassword   = passArg;
      bool   autoRun           = !autorunArgFound;
      #endregion

      #region Compose configuration file
      bool firstRun;
      bool promptShown = false;
      if (firstRun = !mainConfigFile.Exists)
      {
        configuration = new IniData();

        configuration.Sections.AddSection("Service");

        configuration["Service"].AddKey("bMachineConfigured", machineConfigured.ToString());
        configuration["Service"].AddKey("sMachineName", machineName);
        #region configuration["Service"].AddKey("sMachinePassword", machinePassword);
        if (!isPasswordOK(machinePassword))
        {
          if (Program.Hidden)
            ShowWindow(handle, SW_SHOW);

          string passwordValue;
          while (!isPasswordOK(passwordValue = passwordPrompt()))
          {
            reportWeakPassword(passwordValue);
          }
          configuration["Service"].AddKey("sMachinePassword", passwordValue);
          promptShown = true;

          if (Program.Hidden)
            ShowWindow(handle, SW_HIDE);
        }
        else
        {
          configuration["Service"].AddKey("sMachinePassword", machinePassword);
        }
        #endregion
        #region configuration["Service"].AddKey("bAutoRun", autoRun.ToString());
        configuration["Service"].AddKey("bAutoRun", autoRun.ToString());
        #endregion
      }
      else
      {
        configuration = config_parser.ReadFile(mainConfigFile.FullName, Encoding.UTF8);

        machineConfigured = bool.Parse(configuration["Service"]["bMachineConfigured"]);
        #region machineName = configuration["Service"]["sMachineName"];
        if (isNameOK(configuration["Service"]["sMachineName"]))
        {
          machineName = configuration["Service"]["sMachineName"];
        }
        else
        {
          configuration["Service"]["sMachineName"] = machineName;
          machineConfigured = false;
        }
        #endregion
        #region machinePassword = configuration["Service"]["sMachinePassword"];
        machinePassword = configuration["Service"]["sMachinePassword"];

        if (!passArgFound)
        {
          if (!isPasswordOK(machinePassword))
          {
            if (Program.Hidden)
              ShowWindow(handle, SW_SHOW);

            string passwordValue;
            while (!isPasswordOK(passwordValue = passwordPrompt()))
            {
              reportWeakPassword(passwordValue);
            }
            configuration["Service"]["sMachinePassword"] = passwordValue;
            promptShown = true;
            machinePassword = configuration["Service"]["sMachinePassword"];

            if (Program.Hidden)
              ShowWindow(handle, SW_HIDE);
          }
        }
        else
        {
          if (isPasswordOK(passArg))
          {
            configuration["Service"]["sMachinePassword"] = passArg;
            machinePassword = configuration["Service"]["sMachinePassword"];
          }
          else
          {
            if (!isPasswordOK(machinePassword))
            {
              if (Program.Hidden)
                ShowWindow(handle, SW_SHOW);

              string passwordValue;
              while (!isPasswordOK(passwordValue = passwordPrompt()))
              {
                reportWeakPassword(passwordValue);
              }
              configuration["Service"]["sMachinePassword"] = passwordValue;
              promptShown = true;
              machinePassword = configuration["Service"]["sMachinePassword"];

              if (Program.Hidden)
                ShowWindow(handle, SW_HIDE);
            }
          }
        }
        #endregion
        #region autoRun = bool.Parse(configuration["Service"]["bAutoRun"])
        if (!autorunArgFound)
        {
          autoRun = bool.Parse(configuration["Service"]["bAutoRun"]);
        }
        else
        {
          configuration["Service"]["bAutoRun"] = (!bool.Parse(configuration["Service"]["bAutoRun"])).ToString();
          autoRun = bool.Parse(configuration["Service"]["bAutoRun"]);
        }
        #endregion
      }
      #endregion

      // TODO: Get rid of these
      machine = machineName;
      pass = machinePassword;

      // Enable/disable autorun
      if (runningWindows)
      {
        if (autoRun)
          SwitchAutorun(Application.ProductName, Path.Combine(desiredAppDirectory.FullName, Path.GetFileName(Application.ExecutablePath)));
        else
          SwitchAutorun(Application.ProductName);
      }

      #region Update INI file
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
      #endregion

      #region 1 - Time Thread
      var timeThread = new Thread(sendMachineTime);
      timeThread.IsBackground = true;
      timeThread.Start();
      #endregion
      Thread.Sleep(1000);

      #region 2 - Connection Thread
      connectionThread = new Thread(handleConnection);
      connectionThread.IsBackground = true;
      connectionThread.Start();
      #endregion

      #region 3 - Command Thread
      commandThread = new Thread(awaitCommands);
      commandThread.IsBackground = true;
      // Starts in a different place
      #endregion

      #region 4 - Chat Thread
      chatThread = new Thread(serveMessages);
      chatThread.IsBackground = true;
      // Starts in a different place
      #endregion

      timeThread.Join();
    }


    private static void exit()
    {
      Thread.Sleep(Variables.GeneralCloseDelay);
      Environment.Exit(0);
    }

    private static string createMachine()
    {
      return Guid.NewGuid().ToString();
    }

    private static void configureMachine()
    {
      int valueLength = machine.Length + 1; // Variables.MachinesDelimiter
      int realValueLimit = (int) Math.Floor(Variables.IndividualValueLimit / valueLength) * valueLength;

      int listIndex = -1;
      string currentList;
      do
      {
        listIndex++;
        currentList = GetUntilGet($"machines{listIndex}");
        if (currentList.Contains(machine))
        {
          return;
        }
      } while (currentList.Length >= realValueLimit);

      string machines = currentList;

      SetUntilSet($"machines{listIndex}", $"{machines}{machine}{Variables.MachinesDelimiter}");
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
      return new NetworkCredential(String.Empty, PasswordPrompt(resources.PasswordEnterTip)).Password;
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

    #region Reports
    private static void reportWeakPassword(string pas)
    {
      string middlePractical = "| " + resources.PasswordWeakHint;
      string middle = middlePractical + " |";
      middle = middlePractical + Line.GetFilled(' ').Remove(0, middle.Length) + " |";

      Console.Write("#" + Line.GetFilled('-').Remove(0, 2) + "#");
      Console.Write(middle);
      Console.Write("#" + Line.GetFilled('-').Remove(0, 2) + "#");
      Console.CursorVisible = false;

      pas = null;
      Thread.Sleep(Variables.PasswordWeakDelay);

      Console.CursorVisible = true;
    }

    private static void reportGeneralError(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Blue;
      Console.ForegroundColor = ConsoleColor.White;
      Console.WriteLine(msg + "\n");

      if (Program.Hidden)
      {
        ShowWindow(handle, SW_SHOW);
        Console.Beep();
        Thread.Sleep(Variables.GeneralCloseDelay);
        ShowWindow(handle, SW_HIDE);
      }
    }

    private static void reportWebError()
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.DarkYellow;
      Console.WriteLine(resources.WebErrorMessage + "\n");
    }

    private static void reportPastSelf()
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.DarkMagenta;
      Console.WriteLine(resources.PastSins + "\n");
    }

    private static void reportThreadStart(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine(msg + "\n");

      if (resources.CommandStart == msg)
      {
        Console.Beep();
      }
    }

    private static void reportThreadStop(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(msg + "\n");

      if (resources.CommandStop == msg)
      {
        Console.Beep();
      }
    }
    #endregion

    private static void openChatWindow()
    {
      busyChatWise = true;

      ChatboxWindow = new frmChatWindow();
      ChatboxWindow.ShowDialog();

      // ! code below only executes after ChatboxWindow is closed

      ChatboxExit = false;

      busyChatWise = false;

      SetUntilSet($"commands.{machine}", $"{Variables.AnswerPrefix}{Variables.MessageCommand},{ChatboxWindow.Visible}");
    }

    private static void closeChatWindow()
    {
      busyChatWise = false;

      ChatboxExit = true;
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


    private static void resurrectDeadThreads()
    {
      if (connectionThread != null && !connectionThread.IsAlive)
      {
        connectionThread = new Thread(handleConnection);
        connectionThread.IsBackground = true;
        connectionThread.Start();
      }
      if (busyCommandWise && commandThread != null && !commandThread.IsAlive)
      {
        commandThread = new Thread(awaitCommands);
        commandThread.IsBackground = true;
        commandThread.Start();
      }
      if (busyChatWise && chatThread != null && !chatThread.IsAlive)
      {
        chatThread = new Thread(serveMessages);
        chatThread.IsBackground = true;
        chatThread.Start();
      }
    }

    private static void sendMachineTime()
    {
      reportThreadStart(resources.TimeStart);

      while (true)
      {
        var now = DateTime.Now;

        if (resources.WebErrorMessage != Set($"time.{machine}" /* PUN NOT INTENDED */, $"{now.ToShortDateString()} {now.ToLongTimeString()}"))
        {
          if (!internetAlive)
          {
            resurrectDeadThreads();
          }

          internetAlive = true;
        }

        Thread.Sleep(Variables.GeneralDelay - now.Millisecond);
      }
    }

    private static void handleConnection()
    {
      reportThreadStart(resources.ConnectionStart);

      while (internetAlive)
      {
        if (Get(machine) == pass)
        {
          busyCommandWise = !busyCommandWise;
          SetUntilSet(machine, String.Empty);

          if (busyCommandWise && commandThread != null && !commandThread.IsAlive)
          {
            commandThread = new Thread(awaitCommands);
            commandThread.IsBackground = true;
            commandThread.Start();
          }
        }

        Thread.Sleep(Variables.GeneralDelay);
      }

      reportThreadStop(resources.ConnectionStop);
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

        remoteMessage = GetUntilGet("messages." + machine);

        if (!String.IsNullOrWhiteSpace(UserChatMessage))
        {
          SetUntilSet("messages." + machine, ans + UserChatMessage);
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
          

        remoteCommand = GetUntilGet("commands." + machine);

        if (String.Empty == remoteCommand || remoteCommand.StartsWith(ans))
        {
          continue;
        }

        #region Parsing command
        string[] commandParts = remoteCommand.Split(new char[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        bool isCommandSpecial = (commandParts[0] != remoteCommand);

        if (isCommandSpecial)
        {
          char specialCommandIdentifier = commandParts[0].ToCharArray()[0];

          switch (specialCommandIdentifier)
          {
            case Variables.QuitCommand:
              SetUntilSet("commands." + machine, ans + Variables.GeneralOKMsg);
              exitCommand();
              break;

            case Variables.HideCommand:
              SetUntilSet("commands." + machine, ans + hideCommand());
              break;

            case Variables.ShowCommand:
              SetUntilSet("commands." + machine, ans + showCommand());
              break;

            case Variables.DownloadCommand:
              SetUntilSet("commands." + machine, ans + downloadCommand(commandParts));
              break;

            case Variables.MessageCommand:
              SetUntilSet("commands." + machine, ans + messageCommand(commandParts));
              break;

            case Variables.PowershellCommand:
              SetUntilSet("commands." + machine, ans + powershellCommand(commandParts));
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
          SetUntilSet("commands." + machine, ans + executeCommand(commandParts[0]));
        }
        #endregion
      }

      reportThreadStop(resources.CommandStop);
    }


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

      ShowWindow(handle, SW_HIDE);
      Program.Hidden = true;

      return Variables.GeneralOKMsg;
    }

    private static string showCommand()
    {
      if (!Program.Hidden)
      {
        return Variables.GeneralOKMsg;
      }
        

      ShowWindow(handle, SW_SHOW);
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
        string ev = Variables.EvaluateCmdVariable;
        char evd = char.Parse(Variables.EvaluateCmdVariableEnd);

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
          Thread.Sleep(1000);
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
  }
}
