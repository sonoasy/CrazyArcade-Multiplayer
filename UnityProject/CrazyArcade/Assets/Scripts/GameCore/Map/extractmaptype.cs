using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

public class MapExporter : MonoBehaviour
{
    public Tilemap wallTilemap;
    public Tilemap objectTilemap;

    [ContextMenu("Export Map JSON")]
    public void ExportMap()
    {
        List<TilePos> walls = GetTilePositions(wallTilemap);
        List<TilePos> blocks = GetTilePositions(objectTilemap);

        MapDataExport mapData = new MapDataExport();
        mapData.walls = walls;
        mapData.blocks = blocks;

        string json = JsonUtility.ToJson(mapData, true);
        string path = Application.dataPath + "/map.json";
        File.WriteAllText(path, json);

        Debug.Log($"맵 저장 완료! {path}");
        Debug.Log($"벽: {walls.Count}개, 블록: {blocks.Count}개");
    }

    List<TilePos> GetTilePositions(Tilemap tilemap)
    {
        List<TilePos> positions = new List<TilePos>();
        BoundsInt bounds = tilemap.cellBounds;

        for (int x = bounds.min.x; x < bounds.max.x; x++)
        {
            for (int y = bounds.min.y; y < bounds.max.y; y++)
            {
                if (tilemap.HasTile(new Vector3Int(x, y, 0)))
                {
                    positions.Add(new TilePos { x = x, y = y });
                }
            }
        }
        return positions;
    }
}

[System.Serializable]
public class TilePos
{
    public int x;
    public int y;
}

[System.Serializable]
public class MapDataExport
{
    public List<TilePos> walls;
    public List<TilePos> blocks;
}