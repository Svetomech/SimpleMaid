using IniParser;
using IniParser.Model;
using Svetomech.Utilities;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using static Svetomech.Utilities.PasswordStrength;

namespace SimpleMaid
{
  internal class MainConfiguration
  {
    private FileInfo file;
    private FileIniDataParser parser;
    private IniData data;

    private string mainSectionName = "Service";

    internal MainConfiguration(string fileName)
    {
      file = new FileInfo(fileName);
      parser = new FileIniDataParser();
      data = new IniData();
    }

    internal bool ExistsLocally => file.Exists;
    internal string ParserLocation { get; } = Assembly.GetAssembly(typeof(FileIniDataParser)).Location;
    internal string UtilitiesLocation { get; } = Assembly.GetAssembly(typeof(PasswordStrength)).Location;


    internal bool MachineConfigured
    {
      get
      {
        return bool.Parse(data[mainSectionName]["bMachineConfigured"]);
      }

      private set
      {
        data[mainSectionName]["bMachineConfigured"] = value.ToString();
      }
    }

    internal string MachineName
    {
      get
      {
        return data[mainSectionName]["sMachineName"];
      }

      private set
      {
        data[mainSectionName]["sMachineName"] = value;

        MachineConfigured = false;
      }
    }

    internal string MachinePassword
    {
      get
      {
        return data[mainSectionName]["sMachinePassword"];
      }

      set
      {
        if (value == data[mainSectionName]["sMachinePassword"])
        {
          return;
        }

        data[mainSectionName]["sMachinePassword"] = value;

        if (!isValid(nameof(MachinePassword))) loadDefault(nameof(MachinePassword));

        MachineConfigured = false;
      }
    }

    internal bool AutoRun
    {
      get
      {
        return bool.Parse(data[mainSectionName]["bAutoRun"]);
      }

      set
      {
        if (value.ToString() == data[mainSectionName]["bAutoRun"])
        {
          return;
        }

        data[mainSectionName]["bAutoRun"] = value.ToString();

        if (!isValid(nameof(AutoRun))) loadDefault(nameof(AutoRun));

        MachineConfigured = false;
      }
    }

    internal string LoginCommand
    {
      get
      {
        return data[mainSectionName]["sLoginCommand"];
      }

      set
      {
        if (value == data[mainSectionName]["sLoginCommand"])
        {
          return;
        }

        data[mainSectionName]["sLoginCommand"] = value;

        MachineConfigured = false;
      }
    }


    internal void Load()
    {
      if (ExistsLocally)
      {
        data = parser.ReadFile(file.FullName, Encoding.UTF8);

        validate();
      }
      else
      {
        loadDefaults();
      }
    }

    internal void Save()
    {
      if (MachineConfigured)
      {
        return;
      }

      configureMachine();
      MachineConfigured = true;

      parser.WriteFile(file.FullName, data, Encoding.UTF8);
    }

    private void loadDefaults()
    {
      data.Sections.AddSection(mainSectionName);

      loadDefault(nameof(MachineConfigured));
      loadDefault(nameof(MachineName));
      loadDefault(nameof(MachinePassword));
      loadDefault(nameof(AutoRun));
      loadDefault(nameof(LoginCommand));
    }

    private void loadDefault(string settingName)
    {
      switch (settingName)
      {
        case nameof(MachineConfigured):
          MachineConfigured = false;
          break;

        case nameof(MachineName):
          MachineName = Guid.NewGuid().ToString();
          break;

        case nameof(MachinePassword):
          MachinePassword = passwordPromptValidated();
          break;

        case nameof(AutoRun):
          AutoRun = false;
          break;

        case nameof(LoginCommand):
          LoginCommand = String.Empty;
          break;

        default:
          Program.ReportGeneralError(resources.SettingErrorMessage + settingName);
          break;
      }
    }

    // TODO: Use backing fields instead of nameofs
    private void validate()
    {
      if (!isValid(nameof(MachineConfigured))) loadDefault(nameof(MachineConfigured));
      if (!isValid(nameof(MachineName)))       loadDefault(nameof(MachineName));
      if (!isValid(nameof(MachinePassword)))   loadDefault(nameof(MachinePassword));
      if (!isValid(nameof(AutoRun)))           loadDefault(nameof(AutoRun));
      if (!isValid(nameof(LoginCommand)))      loadDefault(nameof(LoginCommand));
    }

    // TODO: Implement second argument settingValue
    private bool isValid(string settingName)
    {
      switch (settingName)
      {
        case nameof(MachineConfigured):
          bool configured; // TODO: Waiting for C# 7.0 to turn this into one-liner
          return bool.TryParse(data[mainSectionName]["bMachineConfigured"], out configured);

        case nameof(MachineName):
          Guid name; // TODO: Waiting for C# 7.0 to turn this into one-liner
          return Guid.TryParse(data[mainSectionName]["sMachineName"], out name);

        case nameof(MachinePassword):
          return CheckStrength(data[mainSectionName]["sMachinePassword"]) >= Variables.MinimalPasswordScore;

        case nameof(AutoRun):
          bool autorun; // TODO: Waiting for C# 7.0 to turn this into one-liner
          return bool.TryParse(data[mainSectionName]["bAutoRun"], out autorun);

        case nameof(LoginCommand):
          return true; // validation happens later

        default:
          Program.ReportGeneralError(resources.SettingErrorMessage + settingName);
          return false;
      }
    }

    private void configureMachine()
    {
      int valueLength = MachineName.Length + 1; // Variables.MachinesDelimiter
      int realValueLimit = (int)Math.Floor((float)Variables.IndividualValueLimit / valueLength) * valueLength;

      int listIndex = -1;
      string currentList;
      do
      {
        listIndex++;
        currentList = Program.GetUntilGet($"machines{listIndex}");
        if (currentList.Contains(MachineName))
        {
          return;
        }
      } while (currentList.Length >= realValueLimit);

      Program.SetUntilSet($"machines{listIndex}", String.Join(Variables.MachinesDelimiter.ToString(),
        currentList.TrimEnd(Variables.MachinesDelimiter), MachineName));
    }

    private string passwordPromptValidated()
    {
      string password;
      PasswordScore score;

      while ((score = CheckStrength(password = passwordPrompt())) < Variables.MinimalPasswordScore)
      {
        Program.ReportWeakPassword();
      }

      Program.AddToTitle($"[{nameof(PasswordScore)}: {score}]");

      return password;
    }

    private string passwordPrompt() => SimpleConsole.InsecurePasswordPrompt(resources.PasswordEnterTip);
  }
}
