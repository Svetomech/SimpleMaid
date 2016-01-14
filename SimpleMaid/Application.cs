using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SimpleMaid
{
  public class Application
  {
    private static readonly Assembly assembly = Assembly.GetEntryAssembly();
    private static readonly FileVersionInfo assemblyInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

    public string CompanyName => assemblyInfo.CompanyName;
    public string ProductName => assemblyInfo.ProductName;
    public string ProductVersion => assemblyInfo.ProductVersion;
    public string ExecutablePath => assembly.Location;
    public string Directory => Path.GetDirectoryName(assembly.Location) + "\\";
    public string Guid => ((GuidAttribute)assembly.GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value;
    public bool Hidden { get; set; } = false;
    public string State { get { return State; } set { Console.Title = $"{ProductName}: {State}"; } }
  }
}
