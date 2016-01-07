using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

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

    public static void SwitchAppAutorun(bool switcher, string appName, string appPath = null)
    {
      string regValue = String.Empty;
      
      regValue = Convert.ToString(Registry.CurrentUser.OpenSubKey(
        "Software\\Microsoft\\Windows\\CurrentVersion\\Run", false).GetValue(appName));
      if (regValue == String.Empty) regValue = null;

      if (regValue != appPath)
      {
        using (RegistryKey reg = Registry.CurrentUser.CreateSubKey(
          "Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
        {
          if (switcher)
            reg.SetValue(appName, appPath);
          else
            reg.DeleteValue(appName);
        }
      }
    }

    public static void DirectoryCopy(string sourceDirName, string destDirName)
    {
      DirectoryInfo dir = new DirectoryInfo(sourceDirName);

      if (!dir.Exists)
        return;

      DirectoryInfo[] dirs = dir.GetDirectories();

      Directory.CreateDirectory(destDirName);

      FileInfo[] files = dir.GetFiles();
      foreach (FileInfo file in files)
      {
        string temppath = Path.Combine(destDirName, file.Name);
        file.CopyTo(temppath, true);
      }

      foreach (DirectoryInfo subdir in dirs)
      {
        string temppath = Path.Combine(destDirName, subdir.Name);
        DirectoryCopy(subdir.FullName, temppath);
      }
    }

    // Get filename from link (hacky way! need to reimplement this later)
    public static string UrlToFile(string url)
    {
      string[] urlParts = url.Split(new char[] { '/' },
          StringSplitOptions.RemoveEmptyEntries);
      string filePart = Uri.UnescapeDataString(urlParts[urlParts.Length - 1]);

      return filePart;
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
  }
}
