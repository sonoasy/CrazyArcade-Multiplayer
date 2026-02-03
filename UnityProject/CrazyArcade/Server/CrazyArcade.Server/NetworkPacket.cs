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
}
