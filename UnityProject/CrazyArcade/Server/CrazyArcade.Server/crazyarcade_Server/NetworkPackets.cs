using System;

// 클라이언트 → 서버: 이동 입력
[Serializable]
public class PlayerMovePacket : NetworkPacket
{
    public ulong PlayerId;
    public Int2 TargetGridPos;  // 목표 위치
    
    public PlayerMovePacket() : base(PacketType.PlayerMove)
    {
    }
}

// 서버 → 클라이언트: 플레이어 상태 업데이트
[Serializable]
public class PlayerStatePacket : NetworkPacket
{
    public PlayerState Player;
    
    public PlayerStatePacket() : base(PacketType.PlayerState)
    {
    }
}

// 연결 패킷
[Serializable]
public class ConnectPacket : NetworkPacket
{
    public ulong PlayerId;
    public string PlayerName;
    
    public ConnectPacket() : base(PacketType.Connect)
    {
    }
}

// 연결 해제 패킷
[Serializable]
public class DisconnectPacket : NetworkPacket
{
    public ulong PlayerId;
    
    public DisconnectPacket() : base(PacketType.Disconnect)
    {
    }
}

// 전체 게임 상태 (접속 시 받음)
[Serializable]
public class GameStatePacket : NetworkPacket
{
    public ulong MyPlayerId;
    public PlayerState[] Players;
    
    public GameStatePacket() : base(PacketType.GameState)
    {
    }
}
