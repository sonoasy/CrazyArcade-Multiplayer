using UnityEngine;

public class StateTest : MonoBehaviour
{
    void Start()
    {
        TestStates();
    }

    void TestStates()
    {
        // GameState 생성
        var gameState = new GameState();

        // PlayerState 생성
        var player = new PlayerState
        {
            PlayerId = 1,
            GridPos = new Int2(5, 5),
            MoveState = PlayerMoveState.Idle,
            BaseState = BaseState.Normal,
            Stats = new PlayerStats()
        };

        // Dictionary에 추가
        gameState.Players[1] = player;

        // 확인
        Debug.Log($"Player Count: {gameState.Players.Count}");
        Debug.Log($"Player Position: {player.GridPos}");
        Debug.Log($"Move Cost: {player.Stats.MoveCostTick}");
        Debug.Log($"Balloon Count: {player.Stats.BalloonCount}");

        // WaterBalloonState 테스트
        var balloon = new WaterBalloonState
        {
            Id = 1,
            Owner = 1,
            Pos = new Int2(5, 5),
            ExplodeTick = 60,
            Range = 1,
            Status = BalloonStatus.Waiting
        };

        gameState.Balloons[1] = balloon;

        Debug.Log($"Balloon Count: {gameState.Balloons.Count}");
        Debug.Log($"Balloon Position: {balloon.Pos}");

        Debug.Log("All State Tests Passed!");
    }
}