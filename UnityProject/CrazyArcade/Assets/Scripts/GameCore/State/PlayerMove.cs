using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public Tilemap objectTilemap;
    public WaterBalloonManager balloonManager;

    private BaseState currentState = BaseState.Normal;
    private float trappedMoveSpeed = 0.5f;
    private float trappedTimer = 0f;
    private float trappedDuration = 10f;

    public int balloonRange = 2;
    private int maxBalloons = 5;
    private int placedBalloonCount = 0;

    private Vector3Int currentGridPos;
    private Vector3Int balloonAtMyFeet;
    private bool isOnMyBalloon = false;
    private bool hasLeftMyBalloon = false;  // ← 추가: 한 번이라도 벗어났는지

    void Start()
    {
        Vector3Int startCell = groundTilemap.WorldToCell(transform.position);
        transform.position = groundTilemap.GetCellCenterWorld(startCell);
        currentGridPos = startCell;
        balloonAtMyFeet = new Vector3Int(-9999, -9999, 0);
    }

    void Update()
    {
        if (currentState == BaseState.Dead)
        {
            return;
        }

        if (currentState == BaseState.Trapped)
        {
            trappedTimer += Time.deltaTime;

            if (trappedTimer >= trappedDuration)
            {
                Die();
                return;
            }
        }

        currentGridPos = groundTilemap.WorldToCell(transform.position);

        // 물풍선 설치
        if (currentState == BaseState.Normal && Input.GetKeyDown(KeyCode.Space))
        {
            if (placedBalloonCount < maxBalloons)
            {
                if (balloonManager.PlaceBalloon(currentGridPos, balloonRange, this))
                {
                    balloonAtMyFeet = currentGridPos;
                    isOnMyBalloon = true;
                    hasLeftMyBalloon = false;  // ← 리셋
                    placedBalloonCount++;
                    Debug.Log($"물풍선 설치! ({placedBalloonCount}/{maxBalloons})");
                }
            }
            else
            {
                Debug.Log("물풍선 최대 개수!");
            }
        }

        // 이동 입력
        float h = 0;
        float v = 0;

        if (Input.GetKey(KeyCode.UpArrow)) v = 1;
        else if (Input.GetKey(KeyCode.DownArrow)) v = -1;
        if (Input.GetKey(KeyCode.LeftArrow)) h = -1;
        else if (Input.GetKey(KeyCode.RightArrow)) h = 1;

        if (h != 0) v = 0;

        if (h != 0 || v != 0)
        {
            float currentMoveSpeed = currentState == BaseState.Trapped ? trappedMoveSpeed : moveSpeed;

            Vector3 moveVec = new Vector3(h, v, 0) * currentMoveSpeed * Time.deltaTime;
            Vector3 nextPos = transform.position + moveVec;
            Vector3Int nextCell = groundTilemap.WorldToCell(nextPos);

            bool hasGround = groundTilemap.HasTile(nextCell);
            bool hasWall = wallTilemap.HasTile(nextCell);
            bool hasObject = objectTilemap.HasTile(nextCell);
            bool hasBalloon = balloonManager.HasBalloon(nextCell);

            // 내 발 밑 물풍선에서 벗어났는지 체크
            if (isOnMyBalloon && nextCell != balloonAtMyFeet)
            {
                isOnMyBalloon = false;
                hasLeftMyBalloon = true;  // ← 한 번 벗어남!
                Debug.Log("물풍선에서 벗어남! 이제 못 들어감");
            }

            bool canMove = hasGround && !hasWall && !hasObject;

            // 물풍선 체크
            if (hasBalloon)
            {
                // 내가 설치한 물풍선이면서 아직 위에 있거나, 한 번도 안 벗어났으면 통과
                if (nextCell == balloonAtMyFeet && !hasLeftMyBalloon)
                {
                    // 통과 가능
                }
                else
                {
                    // 못 지나감
                    canMove = false;
                }
            }

            if (canMove)
            {
                transform.position = nextPos;
            }
        }
    }

    public void OnBalloonExploded()
    {
        placedBalloonCount--;
        Debug.Log($"물풍선 폭발! 남은: ({placedBalloonCount}/{maxBalloons})");
    }

    public void GetTrapped()
    {
        if (currentState == BaseState.Normal)
        {
            currentState = BaseState.Trapped;
            trappedTimer = 0f;
            Debug.Log("물방울에 갇힘! 10초 후 죽음!");

            GetComponent<SpriteRenderer>().color = Color.cyan;
        }
    }

    public void Rescue()
    {
        if (currentState == BaseState.Trapped)
        {
            currentState = BaseState.Normal;
            trappedTimer = 0f;
            Debug.Log("구출됨!");

            GetComponent<SpriteRenderer>().color = Color.white;
        }
    }

    private void Die()
    {
        currentState = BaseState.Dead;
        Debug.Log("플레이어 사망!");

        GetComponent<SpriteRenderer>().color = Color.gray;
    }

    public BaseState GetState()
    {
        return currentState;
    }
}