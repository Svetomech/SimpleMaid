using IniParser;
using IniParser.Model;
using Svetomech.Utilities;
using System;
using System.IO;
using System.Reflection;
using System.Text;

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

    internal bool MachineConfigured
    {
      get
      {
        return bool.Parse(data[mainSectionName]["bMachineConfigured"]);
      }

      set
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

      set
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
        data[mainSectionName]["sMachinePassword"] = value;

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
        data[mainSectionName]["bAutoRun"] = value.ToString();

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
        data[mainSectionName]["sLoginCommand"] = value;

        MachineConfigured = false;
      }
    }

    internal void Load() //params args
    {
      if (ExistsLocally)
      {
        data = parser.ReadFile(file.FullName, Encoding.UTF8);
      }
      else
      {
        loadDefaults();
      }

      validate();
    }

    internal void Save()
    {
      if (MachineConfigured)
      {
        return;
      }

      parser.WriteFile(file.FullName, data, Encoding.UTF8);
    }

    private void loadDefaults()
    {
      data.Sections.AddSection(mainSectionName);
      data[mainSectionName]["bMachineConfigured"] = "False";
      data[mainSectionName]["sMachineName"] = "default";
      data[mainSectionName]["sMachinePassword"] = "default";
      data[mainSectionName]["bAutoRun"] = "False";
      data[mainSectionName]["sLoginCommand"] = String.Empty;
    }

    private void validate()
    {
      throw new NotImplementedException();
    }

    private string createMachine()
    {
      return Guid.NewGuid().ToString();
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

    private bool isNameOk(string name)
    {
      Guid temp; // TODO: Waiting for C# 7.0 to turn this into one-liner
      return Guid.TryParse(name, out temp);
    }

    private bool isPasswordOk(string password)
    {
      var strength = PasswordStrength.CheckStrength(password);

      Program.AddToTitle($"[{nameof(PasswordStrength)}: {strength}]");

      return strength >= Variables.MinimalPasswordStrength;
    }

    private string passwordPrompt()
    {
      return SimpleConsole.InsecurePasswordPrompt(resources.PasswordEnterTip);
    }
  }
}
