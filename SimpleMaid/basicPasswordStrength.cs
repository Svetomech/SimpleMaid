using System.Linq;
using System.Text.RegularExpressions;

namespace SimpleMaid
{
  public class basicPasswordStrength
  {
    public enum PasswordScore
    {
      Blank = 0,
      VeryWeak = 1,
      Weak = 2,
      Fair = 3,
      Medium = 4,
      Strong = 5,
      VeryStrong = 6
    }

    public static PasswordScore CheckStrength(string password)
    {
      int score = 1;

      bool restrictToFair = false;

      if (password.Length < 1)
        return PasswordScore.Blank;
      if (password.Length < 4)
        return PasswordScore.VeryWeak;

      if (password.Length >= 8)
        score++;
      else
        restrictToFair = true;

      if (password.Length >= 12)
        score++;

      if (Regex.IsMatch(password, @"[\d]", RegexOptions.ECMAScript) && !Regex.IsMatch(password, @"^\d+$"))
        score++;
      if (password.Any(c => char.IsLower(c)) &&
          password.Any(c => char.IsUpper(c)))
        score++;
      if (Regex.IsMatch(password, @"[~`!@#$%\^\&\*\(\)\-_\+=\[\{\]\}\|\\;:'\""<\,>\.\?\/£]", RegexOptions.ECMAScript) && score > 1)
        score++;

      var lstPass = password.ToCharArray();
      if (lstPass.Length > 2)
      {
        for (int i = 2; i < lstPass.Length; ++i)
        {
          if (lstPass[i] == lstPass[i - 1] && lstPass[i] == lstPass[i - 2] && score > 1)
            score--;
        }
      }

      if (restrictToFair)
        return ((PasswordScore)score > PasswordScore.Fair) ? PasswordScore.Fair : (PasswordScore)score;
      else
        return (PasswordScore)score;
    }
  }
}
