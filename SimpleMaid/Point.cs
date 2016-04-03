namespace Svetomech.SimpleUtilities
{
  public struct Point
  {
    public int X { set; get; }
    public int Y { set; get; }

    public Point(int x, int y)
    {
      X = x;
      Y = y;
    }
  }
}