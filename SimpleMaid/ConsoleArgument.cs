using System;

namespace SimpleMaid
{
  public struct ConsoleArgument
  {
    public bool Found { get; set; }
    public string Value { get; set; }

    public ConsoleArgument(bool found)
    {
      Found = found;
      Value = String.Empty;
    }
    public ConsoleArgument(bool found, string value)
    {
      Found = found;
      Value = value;
    }
  }
}
