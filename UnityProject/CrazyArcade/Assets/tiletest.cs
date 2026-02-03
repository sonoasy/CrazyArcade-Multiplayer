using UnityEngine;
using UnityEngine.Tilemaps;

public class tileTest : MonoBehaviour
{
    void Start()
    {
        Tilemap ground = GameObject.Find("Ground")?.GetComponent<Tilemap>();

        if (ground != null)
        {
            // 파란색 위치의 월드 좌표
            Vector3 worldPos = new Vector3(-123, 18, 0);

            // 그리드 좌표로 변환
            Vector3Int gridPos = ground.WorldToCell(worldPos);

            Debug.Log($"월드 좌표 {worldPos} -> 그리드 좌표: ({gridPos.x}, {gridPos.y})");
        }
    }
}