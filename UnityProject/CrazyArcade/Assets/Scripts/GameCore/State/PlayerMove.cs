using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 100f;
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public Tilemap objectTilemap;
    public WaterBalloonManager balloonManager;

    public bool isLocalPlayer = false;
    public ulong playerId;

    private BaseState currentState = BaseState.Normal;
    private float trappedMoveSpeed = 0.5f;
    private float trappedTimer = 0f;
    private float trappedDuration = 10f;



    public int balloonRange = 2;
    public int maxBalloons = 5;
    private int placedBalloonCount = 0;
    public int needleCount = 0;
    private Vector3Int currentGridPos;
    private Vector3Int balloonAtMyFeet = new Vector3Int(-9999, -9999, 0);
    private bool isOnMyBalloon = false;
    private bool hasLeftMyBalloon = false;

    // ★ 여기에 추가!
    private float lastSendTime = 0f;
    private float sendInterval = 0.05f;  // 0.05초마다 전송
    private Vector3Int lastSentPosition = new Vector3Int(-9999, -9999, 0);
    public bool IsTrapped()
    {
        return currentState == BaseState.Trapped;
    }
    public void UpdateStats(PlayerStats stats)
    {
        balloonRange = stats.BalloonRange;
        maxBalloons = stats.BalloonCount;
        Debug.Log($"[Stats] Range: {balloonRange}, Count: {maxBalloons}");
    }

    public void Initialize(ulong id, bool isMe)
    {
        this.playerId = id;
        this.isLocalPlayer = isMe;

        // ★ "Ground" → "GroudTilemap"으로 수정!
        if (groundTilemap == null) groundTilemap = GameObject.Find("GroudTilemap")?.GetComponent<Tilemap>();
        if (wallTilemap == null) wallTilemap = GameObject.Find("WallTilemap")?.GetComponent<Tilemap>();
        if (objectTilemap == null) objectTilemap = GameObject.Find("ObjectTilemap")?.GetComponent<Tilemap>();
        if (balloonManager == null) balloonManager = FindObjectOfType<WaterBalloonManager>();

        // ★ 디버그 로그 추가
        Debug.Log($"[PlayerMove] groundTilemap 찾음? {groundTilemap != null}");
    }

    void Start()
    {
        if (groundTilemap != null)
        {
            Vector3Int startCell = groundTilemap.WorldToCell(transform.position);
            transform.position = groundTilemap.GetCellCenterWorld(startCell);
            currentGridPos = startCell;
        }

       
    }

    void Update()
    {
        // ★ 순서 중요: null 체크를 먼저!
        if (NetworkClient.Instance == null) return;
        if (!isLocalPlayer) return;  // 원격 플레이어는 입력 처리 안 함
        if (!NetworkClient.Instance.isConnected) return;
        if (currentState == BaseState.Dead) return;

        // ★ groundTilemap null 체크 추가!
        if (groundTilemap == null) return;
        /*
        if (currentState == BaseState.Trapped)
        {
            trappedTimer += Time.deltaTime;
            if (trappedTimer >= trappedDuration) { Die(); return; }
        }
        */
        currentGridPos = groundTilemap.WorldToCell(transform.position);

        if (currentState == BaseState.Normal && Input.GetKeyDown(KeyCode.Space))
        {
            HandlePlaceBalloon();
        }

        HandleMovement();

        // ★ 여기에 추가! 일정 간격으로 위치 전송
        if (Time.time - lastSendTime >= sendInterval && currentGridPos != lastSentPosition)
        {
            NetworkClient.Instance.SendMyMove(new Int2(currentGridPos.x, currentGridPos.y));
            lastSendTime = Time.time;
            lastSentPosition = currentGridPos;
        }


        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TryUseNeedle();
        }
    }
    void TryUseNeedle()
    {
        if (needleCount <= 0) return;
        if (!IsTrapped()) return;

        NetworkClient.Instance.SendUseNeedle();

        // 로컬 반응성용 (서버 패킷 오면 다시 동기화됨)
        needleCount--;
        if (NeedleUI.Instance != null)
            NeedleUI.Instance.SetCount(needleCount);
    }

    void HandleMovement()
    {
        float h = 0, v = 0;
        if (Input.GetKey(KeyCode.UpArrow)) v = 1;
        else if (Input.GetKey(KeyCode.DownArrow)) v = -1;
        if (Input.GetKey(KeyCode.LeftArrow)) h = -1;
        else if (Input.GetKey(KeyCode.RightArrow)) h = 1;

        if (h != 0) v = 0;

        if (h != 0 || v != 0)
        {
            float speed = (currentState == BaseState.Trapped) ? trappedMoveSpeed : moveSpeed;
            Vector3 nextPos = transform.position + new Vector3(h, v, 0) * speed * Time.deltaTime;
            Vector3Int nextCell = groundTilemap.WorldToCell(nextPos);

            if (CanMove(nextCell))
            {
                transform.position = nextPos;
               // ★ 이 부분 삭제!(Update에서 일괄 처리)
            // if (nextCell != currentGridPos)
            // {
            //     NetworkClient.Instance.SendMyMove(new Int2(nextCell.x, nextCell.y));
            // }
            }
        }
    }

    bool CanMove(Vector3Int nextCell)
    {
        if (!groundTilemap.HasTile(nextCell) || wallTilemap.HasTile(nextCell) || objectTilemap.HasTile(nextCell)) return false;

        bool hasBalloon = balloonManager.HasBalloon(nextCell);
        if (isOnMyBalloon && nextCell != balloonAtMyFeet) { isOnMyBalloon = false; hasLeftMyBalloon = true; }

        if (hasBalloon && !(nextCell == balloonAtMyFeet && !hasLeftMyBalloon)) return false;

        return true;
    }

    void HandlePlaceBalloon()
    {
        if (placedBalloonCount < maxBalloons)
        {
            // ★ 서버로 물풍선 설치 알림 (먼저!)
            NetworkClient.Instance.SendPlaceBalloon(currentGridPos, balloonRange);

            // 로컬에서도 즉시 생성 (반응성을 위해)
            if (balloonManager.PlaceBalloon(currentGridPos, balloonRange, this))
            {
                balloonAtMyFeet = currentGridPos;
                isOnMyBalloon = true;
                hasLeftMyBalloon = false;
                placedBalloonCount++;
            }
        }
    }

    public void OnBalloonExploded() => placedBalloonCount--;

    public void GetTrapped()
    {
        if (currentState == BaseState.Normal)
        {
            currentState = BaseState.Trapped;
            trappedTimer = 0f;
            GetComponent<SpriteRenderer>().color = Color.cyan;
        }
    }

    public void Rescue()
    {
        if (currentState == BaseState.Trapped)
        {
            currentState = BaseState.Normal;
            GetComponent<SpriteRenderer>().color = Color.white;
        }
    }

    public void Die()
    {
        currentState = BaseState.Dead;
        GetComponent<SpriteRenderer>().color = Color.gray;
        Debug.Log($"[PlayerMove] I'm dead, can't move anymore");
    }
}