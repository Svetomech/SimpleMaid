using System.Net;
using static Svetomech.Utilities.PasswordStrength;

namespace SimpleMaid
{
  internal static class Variables
  {
    internal static readonly string ServerAddress = "https://apptinywebdb.appspot.com";
    internal static readonly NetworkCredential AccountCredentials = CredentialCache.DefaultNetworkCredentials;

    internal const string KeywordDefault = "default";
    internal const string ConfigName = KeywordDefault;
    internal const string DefaultPassword = KeywordDefault;
    internal const PasswordScore MinimalPasswordStrength = PasswordScore.Weak;

    internal const string PasswordArgument  = "--pass";     // SimpleMaid.exe --pass NewPassword
    internal const string AutorunArgument   = "--autorun";  // SimpleMaid.exe --autorun
    internal const string LanguageArgument  = "--lang";     // SimpleMaid.exe --lang ru, SimpleMaid.exe --lang ru-RU
    internal const string RogueArgument     = "--rogue";    // SimpleMaid.exe --rogue

    internal const double IndividualValueLimit = 500; // (characters)
    internal const char MachinesDelimiter = ':';
    internal const char CommandsSeparator = ';';
    internal const string AnswerPrefix = "A: ";
    internal const string GeneralOKMsg = "OK!";
    internal const string IncompleteCommandErrMsg = "One good turn deserves another.";
    internal const string EvaluateVariable = "eval<";
    internal const string EvaluateVariableEnd = ">";

    internal const char QuitCommand       = 'q';
    internal const char HideCommand       = 'h';
    internal const char ShowCommand       = 's';
    internal const char DownloadCommand   = 'd';
    internal const char MessageCommand    = 'm'; // TODO: Add Linux support
    internal const char PowershellCommand = 'p';
    internal const char RepeatCommand     = 'r'; // TODO: Optimize Linux experience

    internal const int GeneralDelay       = 1000; // [milliseconds]
    internal const int GeneralCloseDelay  = 2000; // [milliseconds]
    internal const int PasswordWeakDelay  = 500;  // [milliseconds]
  }
}
