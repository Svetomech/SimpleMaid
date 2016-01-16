using System;
using System.IO;
using static System.IO.Path;
using static System.String;
using static System.StringComparison;

namespace SimpleMaid
{
  public static class SimpleIO
  {
    public static class Path
    {
      /// <summary>
      /// Platform-independent paths' equality check.
      /// </summary>
      public static bool Equals(string pathA, string pathB)
      {
        return (SimplePlatform.Platform.Unix == SimplePlatform.runningPlatform()) ?
          0 == Compare(GetFullPath(pathA).TrimEnd(DirectorySeparatorChar), GetFullPath(pathB).TrimEnd(DirectorySeparatorChar)) :
          0 == Compare(GetFullPath(pathA).TrimEnd(DirectorySeparatorChar), GetFullPath(pathB).TrimEnd(DirectorySeparatorChar), OrdinalIgnoreCase);
      }
    }

    public static class Directory
    {
      /// <summary>
      /// Doesn't complain if source directory isn't present or is the same as target,
      /// overwrites the existing files (standart exception is thrown if they're in use).
      /// </summary>
      /// <param name="sourceDir">source directory.</param>
      /// <param name="destDir">target directory.</param>
      /// <param name="copyRootFiles">false to exclude files in source directory from being copied; otherwise, true.</param>
      public static void Copy(DirectoryInfo sourceDir, DirectoryInfo destDir, bool copyRootFiles = true)
      {
        if (!sourceDir.Exists || Path.Equals(sourceDir.FullName, destDir.FullName))
          return;
        destDir.Create();

        if (copyRootFiles)
        {
          var filesInSource = sourceDir.GetFiles();
          foreach (var file in filesInSource)
          {
            file.CopyTo(Combine(destDir.FullName, file.Name), true);
          }
        }

        var dirsInSource = sourceDir.GetDirectories();
        foreach (var dir in dirsInSource)
        {
          Copy(dir, new DirectoryInfo(Combine(destDir.FullName, dir.Name)));
        }
      }
    }


    #region Extension methods

    /// <summary>
    /// Platform-independent paths' equality check.
    /// </summary>
    public static bool IsEqualTo(this DirectoryInfo dir, string pathToCompare)
    {
      if (null == dir)
        throw new ArgumentNullException(nameof(dir));

      return Path.Equals(dir.FullName, pathToCompare);
    }

    /// <summary>
    /// Platform-independent paths' equality check.
    /// </summary>
    public static bool IsEqualTo(this FileInfo file, string pathToCompare)
    {
      if (null == file)
        throw new ArgumentNullException(nameof(file));

      return Path.Equals(file.FullName, pathToCompare);
    }

    /// <summary>
    /// Doesn't complain if source directory isn't present or is the same as target,
    /// overwrites the existing files (standart exception is thrown if they're in use).
    /// </summary>
    /// <param name="dir">source directory.</param>
    /// <param name="destDir">target directory.</param>
    /// <param name="copyRootFiles">false to exclude files in source directory from being copied; otherwise, true.</param>
    public static void CopyTo(this DirectoryInfo dir, DirectoryInfo destDir, bool copyRootFiles = true)
    {
      if (null == dir)
        throw new ArgumentNullException(nameof(dir));

      Directory.Copy(dir, destDir, copyRootFiles);
    }

    #endregion
  }
}