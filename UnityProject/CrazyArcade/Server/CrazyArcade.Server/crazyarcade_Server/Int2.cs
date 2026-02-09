using System;

[Serializable]
public struct Int2
{
    public int X; //{ get; set; }  // 대문자!
    public int Y;// { get; set; }  // 대문자!

    public Int2(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

    public override bool Equals(object obj)
    {
        if (obj is Int2 other)
        {
            return X == other.X && Y == other.Y;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(Int2 a, Int2 b)
    {
        return a.X == b.X && a.Y == b.Y;
    }

    public static bool operator !=(Int2 a, Int2 b)
    {
        return !(a == b);
    }
}