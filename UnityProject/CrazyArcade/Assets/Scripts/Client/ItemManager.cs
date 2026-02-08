using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance;

    public GameObject itemPrefab;
    public Tilemap groundTilemap;

    // 아이템별 스프라이트 (Inspector에서 연결)
    public Sprite balloonSprite;
    public Sprite potionSprite;
    public Sprite rollerSprite;
    public Sprite needleSprite;
    public Sprite kickSprite;
    public Sprite gloveSprite;
    public Sprite sharkSprite;

    private Dictionary<string, GameObject> activeItems = new Dictionary<string, GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public void SpawnItem(string itemId, ItemType itemType, Int2 gridPos)
    {
        if (activeItems.ContainsKey(itemId)) return;

        Vector3Int pos = new Vector3Int(gridPos.X, gridPos.Y, 0);
        Vector3 worldPos = groundTilemap.GetCellCenterWorld(pos);

        GameObject item = Instantiate(itemPrefab, worldPos, Quaternion.identity);
        item.transform.position = new Vector3(worldPos.x, worldPos.y, -0.3f);

        // 아이템 타입에 따라 스프라이트 변경
        SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = GetSpriteForItem(itemType);
        }

        activeItems[itemId] = item;
    }

    public void RemoveItem(string itemId)
    {
        if (activeItems.TryGetValue(itemId, out GameObject item))
        {
            Destroy(item);
            activeItems.Remove(itemId);
        }
    }

    Sprite GetSpriteForItem(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Balloon: return balloonSprite;
            case ItemType.Potion: return potionSprite;
            case ItemType.Roller: return rollerSprite;
            case ItemType.Needle: return needleSprite;
            case ItemType.Kick: return kickSprite;
            case ItemType.Glove: return gloveSprite;
            case ItemType.Shark: return sharkSprite;
            default: return balloonSprite;
        }
    }
}