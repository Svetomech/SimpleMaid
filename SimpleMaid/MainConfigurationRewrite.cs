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
  }
}
