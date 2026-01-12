public static class DirectionExtensions
{
    public static Int2 ToVector(this Direction dir)
    {
        return dir switch
        {
            Direction.Up => new Int2(0, 1),
            Direction.Down => new Int2(0, -1),
            Direction.Left => new Int2(-1, 0),
            Direction.Right => new Int2(1, 0),
            _ => new Int2(0, 0)
        };
    }
}