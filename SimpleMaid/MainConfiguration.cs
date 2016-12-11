using IniParser;
using IniParser.Model;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace SimpleMaid
{
  internal class MainConfigurationOld
  {

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

    // !String.IsNullOrWhiteSpace(mainConfigData["Service"]["sLogonCommand"]) - logonAutomatically

    // TODO: Unite these two into validatePassword
    private static void validateMemoryPassword(ref IniData configuration, ref bool promptShown)
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
    }
  }
}
