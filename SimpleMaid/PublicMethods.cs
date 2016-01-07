using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SimpleMaid
{
  public static class PublicMethods
  {
    public static bool IsAppElevated()
    {
      try { using (Registry.LocalMachine.OpenSubKey("Software\\", true)); }
      catch { return false; }
      return true;
    }

    public static void SwitchAppAutorun(bool switcher, string app_name, string app_path = null)
    {
      string regValue = String.Empty;
      
      regValue = Convert.ToString(Registry.CurrentUser.OpenSubKey(
        "Software\\Microsoft\\Windows\\CurrentVersion\\Run", false).GetValue(app_name));
      if (regValue == String.Empty) regValue = null;

      if (regValue != app_path)
      {
        using (RegistryKey reg = Registry.CurrentUser.CreateSubKey(
          "Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
        {
          if (switcher)
            reg.SetValue(app_name, app_path);
          else
            reg.DeleteValue(app_name);
        }
      }
    }

    public static string ProgramFilesx86()
    {
      if (8 == IntPtr.Size
          || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
      {
        return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
      }

      return Environment.GetEnvironmentVariable("ProgramFiles");
    }

    public static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
    {
      // Get the subdirectories for the specified directory.
      DirectoryInfo dir = new DirectoryInfo(sourceDirName);

      if (!dir.Exists)
      {
        throw new DirectoryNotFoundException(
            "Source directory does not exist or could not be found: "
            + sourceDirName);
      }

      DirectoryInfo[] dirs = dir.GetDirectories();
      // If the destination directory doesn't exist, create it.
      if (!Directory.Exists(destDirName))
      {
        Directory.CreateDirectory(destDirName);
      }

      // Get the files in the directory and copy them to the new location.
      FileInfo[] files = dir.GetFiles();
      foreach (FileInfo file in files)
      {
        string temppath = Path.Combine(destDirName, file.Name);
        file.CopyTo(temppath, false);
      }

      // If copying subdirectories, copy them and their contents to new location.
      if (copySubDirs)
      {
        foreach (DirectoryInfo subdir in dirs)
        {
          string temppath = Path.Combine(destDirName, subdir.Name);
          DirectoryCopy(subdir.FullName, temppath, copySubDirs);
        }
      }
    }

    // Get filename from link (hacky way! need to reimplement this later)
    public static string UrlToFile(string url)
    {
      string[] url_parts = url.Split(new char[] { '/' },
          StringSplitOptions.RemoveEmptyEntries);
      string file_part = Uri.UnescapeDataString(url_parts[url_parts.Length - 1]);

      return file_part;
    }

    public static string GetFilledLine(char c)
    {
      string s = String.Empty;

      Console.BufferWidth = Console.WindowWidth;
      for (int i = 0; i < Console.BufferWidth; ++i)
      {
        s += c.ToString();
      }

      return s;
    }

    public static void ClearConsoleLine()
    {
      int currentLineCursor = Console.CursorTop;
      Console.SetCursorPosition(0, Console.CursorTop);
      Console.Write(new string(' ', Console.WindowWidth));
      Console.SetCursorPosition(0, currentLineCursor);
    }

    public static void ExecuteCmdAsync(string command)
    {
      var objThread = new Thread(new ParameterizedThreadStart(executeCmdSync));

      objThread.IsBackground = true;
      objThread.Priority = ThreadPriority.AboveNormal;

      objThread.Start(command);
    }

    public static void ExecutePowershellAsync(string command)
    {
      var objThread = new Thread(new ParameterizedThreadStart(executePowershellSync));

      objThread.IsBackground = true;
      objThread.Priority = ThreadPriority.AboveNormal;

      objThread.Start(command);
    }

    private static void executeCmdSync(object command)
    {
      var procStartInfo = new ProcessStartInfo("cmd", "/c " + command);

      procStartInfo.RedirectStandardOutput = true;
      procStartInfo.UseShellExecute = false;
      procStartInfo.CreateNoWindow = true;
      
      Process proc = new Process();
      proc.StartInfo = procStartInfo;
      proc.Start();
      
      //string result = proc.StandardOutput.ReadToEnd();
    }

    private static void executePowershellSync(object command)
    {
      var procStartInfo = new ProcessStartInfo("powershell", "-command " + command);

      procStartInfo.RedirectStandardOutput = true;
      procStartInfo.UseShellExecute = false;
      procStartInfo.CreateNoWindow = true;

      Process proc = new Process();
      proc.StartInfo = procStartInfo;
      proc.Start();

      //string result = proc.StandardOutput.ReadToEnd();
    }

    public static string PackmaniseString(string line, int startIndex, char escapeChar)
    {
      string packmanLine = null;
      foreach (char c in line.Remove(0, startIndex))
      {
        if (c == escapeChar) break;
        packmanLine += c;
      }
      return packmanLine;
    }

    public static IEnumerable<int> AllIndexesOf(this string str, string value)
    {
      if (String.IsNullOrEmpty(value))
        throw new ArgumentException("the string to find may not be empty", "value");
      for (int index = 0; ; index += value.Length)
      {
        index = str.IndexOf(value, index);
        if (index == -1)
          break;
        yield return index;
      }
    }

    // Case-insensitive
    public static bool StringContains(string word, string s)
    {
        return s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static string EncodeNonAsciiCharacters(string value)
    {
      StringBuilder sb = new StringBuilder();
      foreach (char c in value)
      {
        if (c > 127)
        {
          // This character is too big for ASCII
          string encodedValue = "\\u" + ((int)c).ToString("x4");
          sb.Append(encodedValue);
        }
        else
        {
          sb.Append(c);
        }
      }
      return sb.ToString();
    }

    public static string DecodeEncodedNonAsciiCharacters(string value)
    {
      return Regex.Replace(
          value,
          @"\\u(?<Value>[a-zA-Z0-9]{4})",
          m =>
          {
            return ((char)int.Parse(m.Groups["Value"].Value, NumberStyles.HexNumber)).ToString();
          });
    }

    /*public static string DecodeEncodedNonAsciiCharacters(string value)
    {
      var buffer = new List<char>();
      string[] subbuffer = value.Split('\\');
      foreach(string s in subbuffer)
      {
        buffer.Add(char.Parse("\\" + s));
      }
      value = String.Empty;
      foreach (char c in buffer)
      {
        value += c.ToString();
      }
      return value;
    }*/

    public static string GetVariableName<T>(T item) where T : class
    {
      return typeof(T).GetProperties()[0].Name;
    }
  }
}
