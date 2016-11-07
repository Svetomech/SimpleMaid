using System.Net;
using static Svetomech.Utilities.PasswordStrength;

namespace SimpleMaid
{
  internal static class Variables
  {
    // Web strings
    internal const string ServerAddress = "https://apptinywebdb.appspot.com";
    internal const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64)";
    internal static readonly NetworkCredential Credentials = CredentialCache.DefaultNetworkCredentials;

    // Some defaults
    internal const string KeywordDefault = "default";
    internal const string ConfigName = KeywordDefault;
    internal const string DefaultPassword = KeywordDefault;
    internal const PasswordScore MinimalPasswordStrength = PasswordScore.Weak;
    internal const string LangFolderKey = "LangFolderName"; // -> App.config

    // Console arguments
    internal const string PasswordArgument  = "--pass";     // SimpleMaid.exe --pass NewPassword
    internal const string LanguageArgument  = "--lang";     // SimpleMaid.exe --lang ru
    internal const string AutorunArgument   = "--autorun";  // SimpleMaid.exe --autorun
    internal const string RogueArgument     = "--rogue";    // SimpleMaid.exe --rogue

    // Syntax mostly
    internal const char MachinesDelimiter = ':';
    internal const char CommandsSeparator = ';';
    internal const string AnswerPrefix = "A: ";
    internal const string EvaluateVariable = "eval<";
    internal const string EvaluateVariableEnd = ">";
    internal const double IndividualValueLimit = 500; // [characters]

    // Custom commands
    internal const char QuitCommand       = 'q';
    internal const char HideCommand       = 'h';
    internal const char ShowCommand       = 's';
    internal const char DownloadCommand   = 'd';
    internal const char MessageCommand    = 'm';
    internal const char PowershellCommand = 'p';
    internal const char RepeatCommand     = 'r';

    // Non-localisible
    internal const string GeneralOKMsg = "OK!";
    internal const string PowershellLinuxErrMsg = "https://github.com/powershell/powershell";
    internal const string IncompleteCommandErrMsg = "One good turn deserves another.";

    // In milliseconds
    internal const int GeneralDelay       = 1000;
    internal const int WindowCloseDelay   = 2000;
    internal const int PasswordWeakDelay  = 500;
  }
}
