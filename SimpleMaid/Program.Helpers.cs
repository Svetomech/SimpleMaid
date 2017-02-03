using Svetomech.Utilities;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace SimpleMaid
{
  internal static partial class Program
  {
    internal static void SetUntilSet(string tag, string value)
    {
      while (_internetAlive && resources.WebErrorMessage == Set(tag, value))
      {
        Thread.Sleep(Variables.GeneralDelay);
      }
    }

    internal static string GetUntilGet(string tag)
    {
      string value = resources.WebErrorMessage;

      while (_internetAlive && resources.WebErrorMessage == (value = Get(tag)))
      {
        Thread.Sleep(Variables.GeneralDelay);
      }

      return value;
    }

    private static string Set(string tag, string value)
    {
      tag = $"{ConsoleApplication.ProductName}_{tag}";

      var encoding = new UTF8Encoding();
      byte[] requestBody = encoding.GetBytes($"tag={tag}&value={value}&fmt=html");

      var request = (HttpWebRequest)WebRequest.Create($"{Variables.ServerAddress}/storeavalue");
      request.Method = "POST";
      request.Credentials = Variables.Credentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;
      request.UserAgent = Variables.UserAgent;

      try
      {
        using (var requestStream = request.GetRequestStream())
        {
          requestStream.Write(requestBody, 0, requestBody.Length);
        }
      }
      catch (WebException)
      {
        ReportWebError();
        _internetAlive = false;
        return resources.WebErrorMessage;
      }
      // ReSharper disable once EmptyEmbeddedStatement
      try { using (request.GetResponse()) ; }
      catch (WebException)
      {
        ReportWebError();
        _internetAlive = false;
        return resources.WebErrorMessage;
      }

      Console.ResetColor();
      Console.ForegroundColor = ConsoleColor.Gray;
      Console.WriteLine($@"SET  {tag}  {value}{Environment.NewLine}");

      return String.Empty;
    }

    private static string Get(string tag)
    {
      tag = $"{ConsoleApplication.ProductName}_{tag}";

      string value;

      var encoding = new UTF8Encoding();
      byte[] requestBody = encoding.GetBytes($"tag={tag}&fmt=html");

      var request = (HttpWebRequest)WebRequest.Create($"{Variables.ServerAddress}/getvalue");
      request.Method = "POST";
      request.Credentials = Variables.Credentials;
      request.ContentType = "application/x-www-form-urlencoded";
      request.ContentLength = requestBody.Length;
      request.UserAgent = Variables.UserAgent;

      try
      {
        using (var requestStream = request.GetRequestStream())
        {
          requestStream.Write(requestBody, 0, requestBody.Length);
        }
      }
      catch (WebException)
      {
        ReportWebError();
        _internetAlive = false;
        return resources.WebErrorMessage;
      }

      // TODO: Refactor
      try
      {
        using (var response = request.GetResponse())
        using (var responseStream = response.GetResponseStream())
        // ReSharper disable once AssignNullToNotNullAttribute
        using (var sr = new StreamReader(responseStream))
        {
          value = sr.ReadToEnd();
          value = value.Substring(value.IndexOf(tag, StringComparison.Ordinal) + tag.Length + 4);
          value = value.Remove(value.IndexOf("\"", StringComparison.Ordinal));
        }
      }
      catch (WebException)
      {
        ReportWebError();
        _internetAlive = false;
        return resources.WebErrorMessage;
      }

      // TODO: Rewrite - problems with decoding
      value = value.DecodeNonAsciiCharacters();
      value = value.Replace(@"\/", @"/");
      value = value.Replace(@"\\", @"\");
      value = WebUtility.HtmlDecode(value);

      Console.ResetColor();
      Console.ForegroundColor = ConsoleColor.DarkGreen;
      Console.WriteLine($@"GET  {tag}  {value}{Environment.NewLine}");

      return value;
    }

    private static void Exit()
    {
      Thread.Sleep(Variables.WindowCloseDelay);
      Environment.Exit(0);
    }

    private static void OpenChatWindow()
    {
      _busyChatWise = true;

      ChatboxWindow = new FrmChatWindow();
      ChatboxWindow.ShowDialog();

      // ! code below only executes after ChatboxWindow is closed

      ChatboxExit = false;

      _busyChatWise = false;
    }
  }
}
