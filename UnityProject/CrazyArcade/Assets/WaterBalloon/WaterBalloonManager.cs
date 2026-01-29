using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class WaterBalloonManager : MonoBehaviour
{
    public GameObject balloonPrefab;
    public GameObject waterEffectPrefab;
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public Tilemap objectTilemap;

    private Dictionary<Vector3Int, BalloonInfo> activeBalloons = new Dictionary<Vector3Int, BalloonInfo>();

    // 물풍선 정보
    private class BalloonInfo
    {
        public GameObject balloonObject;
        public PlayerMove owner;  // 설치한 플레이어
    }

    // 물풍선 설치
    public bool PlaceBalloon(Vector3Int gridPos, int range, PlayerMove owner)
    {
        if (activeBalloons.ContainsKey(gridPos))
        {
            return false;
        }

        Vector3 worldPos = groundTilemap.GetCellCenterWorld(gridPos);
        GameObject balloon = Instantiate(balloonPrefab, worldPos, Quaternion.identity);
        balloon.transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);

        activeBalloons[gridPos] = new BalloonInfo
        {
            balloonObject = balloon,
            owner = owner
        };

        StartCoroutine(ExplodeBalloon(gridPos, range));

        return true;
    }

    public bool HasBalloon(Vector3Int gridPos)
    {
        return activeBalloons.ContainsKey(gridPos);
    }

    private System.Collections.IEnumerator ExplodeBalloon(Vector3Int gridPos, int range)
    {
        yield return new WaitForSeconds(3f);

        BalloonInfo info = null;
        if (activeBalloons.ContainsKey(gridPos))
        {
            info = activeBalloons[gridPos];
            Destroy(info.balloonObject);
            activeBalloons.Remove(gridPos);

            // 설치한 플레이어의 카운트 감소
            if (info.owner != null)
            {
                info.owner.OnBalloonExploded();
            }
        }

        // 중앙 물줄기
        Vector3 centerPos = groundTilemap.GetCellCenterWorld(gridPos);
        GameObject centerWater = Instantiate(waterEffectPrefab, centerPos, Quaternion.identity);
        centerWater.transform.position = new Vector3(centerPos.x, centerPos.y, -0.5f);
        Destroy(centerWater, 0.5f);

        // 중앙 플레이어 체크
        CheckPlayerHit(gridPos);

        // 4방향 물줄기
        CreateWaterStream(gridPos, Vector3Int.up, range - 1);
        CreateWaterStream(gridPos, Vector3Int.down, range - 1);
        CreateWaterStream(gridPos, Vector3Int.left, range - 1);
        CreateWaterStream(gridPos, Vector3Int.right, range - 1);
    }

    private void CreateWaterStream(Vector3Int startPos, Vector3Int direction, int range)
    {
        for (int i = 1; i <= range; i++)
        {
            Vector3Int currentPos = startPos + direction * i;

            if (!groundTilemap.HasTile(currentPos))
            {
                break;
            }

            if (wallTilemap.HasTile(currentPos))
            {
                break;
            }

            if (objectTilemap.HasTile(currentPos))
            {
                objectTilemap.SetTile(currentPos, null);

                Vector3 worldPos = groundTilemap.GetCellCenterWorld(currentPos);
                GameObject water = Instantiate(waterEffectPrefab, worldPos, Quaternion.identity);
                water.transform.position = new Vector3(worldPos.x, worldPos.y, -0.5f);
                Destroy(water, 0.5f);

                // 플레이어 체크
                CheckPlayerHit(currentPos);

                break;
            }

            Vector3 worldPos2 = groundTilemap.GetCellCenterWorld(currentPos);
            GameObject water2 = Instantiate(waterEffectPrefab, worldPos2, Quaternion.identity);
            water2.transform.position = new Vector3(worldPos2.x, worldPos2.y, -0.5f);
            Destroy(water2, 0.5f);

            // 플레이어 체크
            CheckPlayerHit(currentPos);
        }
    }

    private void CheckPlayerHit(Vector3Int gridPos)
    {
        PlayerMove[] players = FindObjectsOfType<PlayerMove>();

        foreach (PlayerMove player in players)
        {
            Vector3Int playerGridPos = groundTilemap.WorldToCell(player.transform.position);

            if (playerGridPos == gridPos)
            {
                player.GetTrapped();
            }
        }
    }
}