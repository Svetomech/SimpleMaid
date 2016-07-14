using IniParser;
using IniParser.Model;
using Svetomech.Utilities;
using System;
using System.IO;
using System.Text;
using static Svetomech.Utilities.PasswordStrength;

namespace SimpleMaid
{
  internal class mainConfiguration
  {
    private FileInfo file;
    private FileIniDataParser parser;
    private IniData data;

    public mainConfiguration(string fileName)
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

        string mainSectionName = "Service";
        data.Sections.AddSection(mainSectionName);

        data[mainSectionName]["bMachineConfigured"] = "False";
        data[mainSectionName]["sMachineName"]       = "default";
        data[mainSectionName]["sMachinePassword"]   = "default";
        data[mainSectionName]["bAutoRun"]           = "False";
        data[mainSectionName]["sLogonCommand"]      = String.Empty;
      }
    }

    public bool ExistsLocally => file.Exists;


    public bool machineConfigured
    {
      set
      {
        data["Service"]["bMachineConfigured"] = value.ToString();
      }

      get
      {
        return (bool.Parse(data["Service"]["bMachineConfigured"]));
      }
    }

    public string machineName
    {
      set
      {
        machineConfigured = false;

        data["Service"]["sMachineName"] = value;
      }

      get
      {
        Guid temp; // TODO: Waiting for C# 7.0 to turn this into one-liner
        if (!Guid.TryParse(data["Service"]["sMachineName"], out temp))
        {
          machineName = ;
        }

        return data["Service"]["sMachineName"];
      }
    }

    public string machinePassword
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

    public bool autoRun
    {
      set
      {
        machineConfigured = false;

        data["Service"]["bAutoRun"] = value.ToString();
      }

      get
      {
        return (bool.Parse(data["Service"]["bAutoRun"]));
      }
    }

    /*public string logonCommand
    {

    }*/

    //update

    //merge

    //promptshown?

    // !String.IsNullOrWhiteSpace(mainConfigData["Service"]["sLogonCommand"]) - logonAutomatically, logon to login
  }
}
