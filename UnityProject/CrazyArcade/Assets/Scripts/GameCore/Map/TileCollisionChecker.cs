using UnityEngine;
using UnityEngine.Tilemaps;

public class TileCollisionChecker : MonoBehaviour
{
    public Tilemap groundTilemap;   // 파란색 - 이동 가능
    public Tilemap wallTilemap;     // 초록색 - 영구 장애물 (못 지나감)
    public Tilemap objectTilemap;   // 분홍색 - 부서지는 장애물 (못 지나감)

    /// <summary>
    /// 해당 월드 좌표로 이동 가능한지 체크
    /// </summary>
    public bool CanMoveTo(Vector3 worldPosition)
    {
        Vector3Int cellPos = groundTilemap.WorldToCell(worldPosition);

        // 벽이 있으면 못 감 (초록색 - 영구 장애물)
        if (wallTilemap.HasTile(cellPos))
        {
            return false;
        }

        // 오브젝트가 있으면 못 감 (분홍색 - 부서지는 장애물)
        if (objectTilemap.HasTile(cellPos))
        {
            return false;
        }

        // Ground 타일이 없으면 못 감 (맵 밖)
        if (!groundTilemap.HasTile(cellPos))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 충돌 체크하면서 다음 위치 계산
    /// </summary>
    public Vector3 GetNextPosition(Vector3 currentPos, Vector3 direction, float distance)
    {
        Vector3 targetPos = currentPos + direction * distance;

        // 목표 위치로 갈 수 있으면 이동
        if (CanMoveTo(targetPos))
        {
            return targetPos;
        }

        // 못 가면 제자리
        return currentPos;
    }

    /// <summary>
    /// 해당 위치의 Object 타일 제거 (물풍선이 터졌을 때 호출)
    /// </summary>
    public void DestroyObjectAt(Vector3Int cellPos)
    {
        if (objectTilemap.HasTile(cellPos))
        {
            objectTilemap.SetTile(cellPos, null);

            // TODO: 여기서 랜덤 아이템 생성
            // SpawnRandomItem(cellPos);
        }
    }

    /// <summary>
    /// 월드 좌표를 그리드 좌표로 변환
    /// </summary>
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return groundTilemap.WorldToCell(worldPos);
    }
}