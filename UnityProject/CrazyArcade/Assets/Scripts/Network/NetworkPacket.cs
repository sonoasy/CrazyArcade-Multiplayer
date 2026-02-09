using System;

// 패킷 타입 정의
public enum PacketType
{
    // 연결
    Connect = 1,
    Disconnect = 2,
    Join=3,
    GameStartCountdown = 4,
    GameStart = 5,
    // 플레이어 이동
    PlayerMove = 10,
    PlayerState = 11,
    
    // 게임 상태
    GameState = 20,
    
    // 물풍선 (나중에)
    PlaceBalloon = 30,
    BalloonExplode = 31,
    UseNeedle=32,

    PlayerDie = 40,        // ★ 추가
    GameOver = 41,         // ★ 추가
    GameTimer = 42,         // ★ 추가
    PlayerTrapped = 43,    // ★ 추가
    PlayerRescued = 44,     // ★ 추가

    ItemSpawn = 50,
    ItemPickup = 51,
    BlockDestroy = 60   // ★ 추가
}

// 아이템 타입
public enum ItemType
{
    Balloon,    // 물풍선 개수 +1 (최대 15)
    Potion,     // 물줄기 범위 +1
    Roller,     // 이동속도 +10
    Needle,     // 갇힘 탈출
    Kick,       // 발차기
    Glove,      // 던지기
    Shark       // 상어 타기
}
// ★ 게임 시작 카운트다운 패킷
[Serializable]
public class GameStartCountdownPacket : NetworkPacket
{
    public int Remaining; // 10, 9, 8 ...

    public GameStartCountdownPacket()
        : base(PacketType.GameStartCountdown) { }
}

// ★ 게임 시작 패킷
[Serializable]
public class GameStartPacket : NetworkPacket
{
    public GameStartPacket()
        : base(PacketType.GameStart) { }
}
[Serializable]
public class UseNeedlePacket : NetworkPacket
{
    public ulong PlayerId;
    public UseNeedlePacket() : base(PacketType.UseNeedle) { }
}

[Serializable]
public class JoinPacket : NetworkPacket
{
    public string Nickname { get; set; }
    public JoinPacket() : base(PacketType.Join) { }
}

// ★ 블록 파괴 패킷
[Serializable]
public class BlockDestroyPacket : NetworkPacket
{
    public Int2 GridPos;
    public BlockDestroyPacket() : base(PacketType.BlockDestroy) { }

}
// 아이템 스폰 패킷
[Serializable]
public class ItemSpawnPacket : NetworkPacket
{
    public string ItemId;
    public ItemType ItemType;
    public Int2 GridPos;
    public ItemSpawnPacket() : base(PacketType.ItemSpawn) { }
}

// 아이템 획득 패킷
[Serializable]
public class ItemPickupPacket : NetworkPacket
{
    public string ItemId;
    public ulong PlayerId;
    public ItemType ItemType;
    public ItemPickupPacket() : base(PacketType.ItemPickup) { }
}



// ★ 플레이어 죽음 패킷
[Serializable]
public class PlayerDiePacket : NetworkPacket
{
    public ulong PlayerId;

    public PlayerDiePacket() : base(PacketType.PlayerDie) { }
}

// ★ 게임 종료 패킷
[Serializable]
public class GameOverPacket : NetworkPacket
{
    public ulong WinnerPlayerId;
    public string Reason;  // "killed" or "timeout"

    public GameOverPacket() : base(PacketType.GameOver) { }
}

// ★ 타이머 동기화 패킷
[Serializable]
public class GameTimerPacket : NetworkPacket
{
    public float RemainingTime;  // 남은 시간 (초)

    public GameTimerPacket() : base(PacketType.GameTimer) { }
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
    // ★ 플레이어 갇힘 패킷
    [Serializable]
    public class PlayerTrappedPacket : NetworkPacket
    {
        public ulong PlayerId;
        public PlayerTrappedPacket() : base(PacketType.PlayerTrapped) { }
    }

    // ★ 플레이어 구출 패킷
    [Serializable]
    public class PlayerRescuedPacket : NetworkPacket
    {
        public ulong PlayerId;
        public ulong RescuerId;
        public PlayerRescuedPacket() : base(PacketType.PlayerRescued) { }
    }
}
