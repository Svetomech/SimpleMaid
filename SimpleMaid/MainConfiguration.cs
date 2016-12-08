using IniParser;
using IniParser.Model;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace SimpleMaid
{
  internal class MainConfiguration
  {
    internal bool ExistsLocally => file.Exists;
    internal string ParserLocation => Assembly.GetAssembly(typeof(FileIniDataParser)).Location;

    internal MainConfiguration(string fileName)
    {
      file = new FileInfo(fileName);
      parser = new FileIniDataParser();

      if (file.Exists)
      {
        data = parser.ReadFile(file.FullName, Encoding.UTF8);
      }
      else
      {
        data = new IniData();

        mainSection = "Service";
        machineConfiguredKey = "bMachineConfigured";
        machineNameKey       = "sMachineName";
        machinePasswordKey   = "sMachinePassword";
        autoRunKey           = "bAutoRun";
        loginCommandKey      = "sLoginCommand";

        data.Sections.AddSection(mainSection);
        data[mainSection][machineConfiguredKey] = "False";
        data[mainSection][machineNameKey]       = "default";
        data[mainSection][machinePasswordKey]   = "default";
        data[mainSection][autoRunKey]           = "False";
        data[mainSection][loginCommandKey]      = String.Empty;
      }
    }

    internal void Update()
    {
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
    }

    internal void Save()
    {
      if (MachineConfigured)
      {
        return;
      }

      // Actually save file
    }

    private FileInfo file;
    private FileIniDataParser parser;
    private IniData data;

    private string mainSection;
    private string machineConfiguredKey;
    private string machineNameKey;
    private string machinePasswordKey;
    private string autoRunKey;
    private string loginCommandKey;


    internal bool MachineConfigured
    {
      set
      {
        data[mainSection][machineConfiguredKey] = value.ToString();
      }

      get
      {
        return (bool.Parse(data[mainSection][machineConfiguredKey]));
      }
    }

    internal string MachineName
    {
      set
      {
        MachineConfigured = false;

        data[mainSection][machineNameKey] = value;
      }

      get
      {
        Guid temp; // TODO: Waiting for C# 7.0 to turn this into one-liner
        if (!Guid.TryParse(data[mainSection][machineNameKey], out temp))
        {
          MachineName = ;
        }

        return data[mainSection][machineNameKey];
      }
    }

    internal string MachinePassword
    {
      set
      {
        //validateMemoryPassword(ref mainConfigData, ref promptShown); если не Exists
        //validateConfigPassword(ref mainConfigData, ref promptShown); если Exists
      }

      get
      {
        //validateConfigPassword(ref mainConfigData, ref promptShown); если Exists
      }
    }

    internal bool AutoRun
    {
      set
      {
        MachineConfigured = false;

        data["Service"]["bAutoRun"] = value.ToString();
      }

      get
      {
        return (bool.Parse(data["Service"]["bAutoRun"]));
      }
    }

    internal string LoginCommand
    {
      set;
      get;
    }

    //update

    //merge

    //promptshown?

    // !String.IsNullOrWhiteSpace(mainConfigData["Service"]["sLogonCommand"]) - logonAutomatically

    private static string CreateMachine()
    {
      return Guid.NewGuid().ToString();
    }

    private static void ConfigureMachine()
    {
      int valueLength = MainConfig.MachineName.Length + 1; // Variables.MachinesDelimiter
      int realValueLimit = (int)Math.Floor((float)Variables.IndividualValueLimit / valueLength) * valueLength;

      int listIndex = -1;
      string currentList;
      do
      {
        listIndex++;
        currentList = GetUntilGet($"machines{listIndex}");
        if (currentList.Contains(MainConfig.MachineName))
        {
          return;
        }
      } while (currentList.Length >= realValueLimit);

      string machines = currentList;

      SetUntilSet($"machines{listIndex}", $"{machines}{MainConfig.MachineName}{Variables.MachinesDelimiter}");
    }

    private static bool IsNameOk(string name)
    {
      Guid temp; // TODO: Waiting for C# 7.0 to turn this into one-liner
      return Guid.TryParse(name, out temp);
    }

    private static bool IsPasswordOk(string password)
    {
      var strength = PasswordStrength.CheckStrength(password);

      Program.State = $"{resources.MainWindowTitle} [{nameof(PasswordStrength)}: {strength}]";

      return (strength >= Variables.MinimalPasswordStrength);
    }

    private static string PasswordEntered()
    {
      return InsecurePasswordPrompt(resources.PasswordEnterTip);
    }

    // TODO: Unite these two into validatePassword
    /*private static void validateMemoryPassword(ref IniData configuration, ref bool promptShown)
    {
      if (isPasswordOK(machinePassword))
      {
        configuration["Service"].AddKey("sMachinePassword", machinePassword);
      }
      else
      {
        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_SHOW);
        }

        while (!isPasswordOK(machinePassword = passwordPrompt()))
        {
          reportWeakPassword();
        }
        promptShown = true;

        configuration["Service"].AddKey("sMachinePassword", machinePassword);

        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_HIDE);
        }
      }
    }
    private static void validateConfigPassword(ref IniData configuration, ref bool promptShown)
    {
      if (isPasswordOK(configuration["Service"]["sMachinePassword"]))
      {
        machinePassword = configuration["Service"]["sMachinePassword"];
      }
      else
      {
        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_SHOW);
        }

        while (!isPasswordOK(machinePassword = passwordPrompt()))
        {
          reportWeakPassword();
        }
        promptShown = true;

        configuration["Service"]["sMachinePassword"] = machinePassword;

        if (Program.Hidden)
        {
          ShowWindow(mainWindowHandle, SW_HIDE);
        }
      }
    }*/
  }
}
