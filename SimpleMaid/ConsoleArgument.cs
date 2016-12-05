using System;

namespace SimpleMaid
{
  public struct ConsoleArgument
  {
    public ConsoleArgument(string defaultValue)
    {
      Found = false;
      Value = defaultValue;
    }

    public bool Found { get; set; }
    public string Value { get; set; }
  }
}
