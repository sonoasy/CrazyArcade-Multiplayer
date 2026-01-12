using System.Collections.Generic;

public class GameState
{
    public Dictionary<ulong, PlayerState> Players = new();
    public Dictionary<uint, WaterBalloonState> Balloons = new();
    public Dictionary<uint, ItemState> Items = new();

    // MapState는 Issue #3에서 구현
    public MapState MapState;
}