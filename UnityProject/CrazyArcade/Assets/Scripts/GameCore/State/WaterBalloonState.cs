public enum BalloonStatus
{
    Waiting,
    Exploding,
    Destroyed
}

public class WaterBalloonState
{
    public uint Id;
    public ulong Owner;
    public Int2 Pos;
    public int ExplodeTick;
    public int Range;
    public BalloonStatus Status;
}