using System;

// 패킷 타입 정의
public enum PacketType
{
    // 연결
    Connect = 1,
    Disconnect = 2,
    
    // 플레이어 이동
    PlayerMove = 10,
    PlayerState = 11,
    
    // 게임 상태
    GameState = 20,
    
    // 물풍선 (나중에)
    PlaceBalloon = 30,
    BalloonExplode = 31,
}

// 기본 패킷 클래스
[Serializable]
public class NetworkPacket
{
    public PacketType Type;
    public long Timestamp;
    
    public NetworkPacket(PacketType type)
    {
        Type = type;
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }



    // 물풍선 설치 패킷
    [Serializable]
    public class PlaceBalloonPacket : NetworkPacket
    {
        public ulong PlayerId;
        public Int2 GridPos;
        public int Range;

        public PlaceBalloonPacket() : base(PacketType.PlaceBalloon) { }
    }

    // 물풍선 폭발 패킷
    [Serializable]
    public class BalloonExplodePacket : NetworkPacket
    {
        public Int2 GridPos;
        public Int2[] AffectedCells;  // 폭발 범위

        public BalloonExplodePacket() : base(PacketType.BalloonExplode) { }
    }
}
