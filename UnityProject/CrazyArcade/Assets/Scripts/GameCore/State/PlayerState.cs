 public enum PlayerMoveState
{
    Idle,
    Moving
}

public enum BaseState
{
    Normal,
    Trapped,
    Dead,
    Riding
}

// ★ 아이템 타입 추가

public class PlayerStats
{
    public int MoveCostTick = 6;      // 기본 6틱당 1칸
    public int BalloonCount = 1;      // 설치 가능 풍선 개수
    public int BalloonRange = 1;      // 물줄기 길이
    public bool CanSwim = false;      // 물 타일 이동 가능 여부
                                      // ★ 아이템 효과 추가
    public bool HasKick = false;       // 발차기
    public bool HasGlove = false;      // 장갑
    public bool HasNeedle = false;     // 바늘
    public bool IsRidingShark = false; // 상어 타기
    public int SharkBalloonCount = 0;  // 상어가 삼킨 물풍선
}

public class PlayerState
{
    public ulong PlayerId;

    // 위치
    public Int2 GridPos;
    public Int2? TargetGridPos;
    public string Nickname;
    // 이동 상태
    public PlayerMoveState MoveState;
    public int MoveStartTick;
    public int MoveEndTick;

    // 기본 상태
    public BaseState BaseState;

    // 능력치
    public PlayerStats Stats = new PlayerStats();

    // 물풍선 제한
    public int PlacedBalloonCount;
}