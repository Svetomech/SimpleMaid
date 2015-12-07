using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SimpleMaid
{
  public class Application
  {
    private Assembly assembly;
    private FileVersionInfo assemblyInfo;


    private string companyName;
    private string productName;
    private string productVersion;
    private string executablePath;
    private string directory;
    private string guid;
    private bool hidden;

    private string state;


    public string CompanyName { get { return companyName; } }
    public string ProductName { get { return productName; } }
    public string ProductVersion { get { return productVersion; } }
    public string ExecutablePath { get { return executablePath; } }
    public string Directory { get { return directory; } }
    public string Guid { get { return guid; } }
    public bool Hidden { get { return hidden; } set { hidden = value; } }

    /// <summary>
    /// Supports: "idle", "busy", "fail"
    /// </summary>
    public string State
    {
      get { return state; }
      set
      {
        state = value;
        Console.Title = String.Format("{0}: {1}", productName, state);
      }
    }


    public Application()
    {
      assembly = Assembly.GetEntryAssembly();
      assemblyInfo = FileVersionInfo.GetVersionInfo(assembly.Location);


      companyName = assemblyInfo.CompanyName;
      productName = assemblyInfo.ProductName;
      productVersion = assemblyInfo.ProductVersion;
      executablePath = assembly.Location;
      directory = Path.GetDirectoryName(executablePath) + "\\";
      guid = ((GuidAttribute)assembly.GetCustomAttributes(
        typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
      hidden = false;

      state = null;
    }
  }
}
