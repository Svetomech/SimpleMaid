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
      // Do I need this?
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

    }

    //update

    //merge

    //promptshown?

    // !String.IsNullOrWhiteSpace(mainConfigData["Service"]["sLogonCommand"]) - logonAutomatically
  }
}
