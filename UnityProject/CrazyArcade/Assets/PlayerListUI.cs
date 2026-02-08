using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class PlayerListUI : MonoBehaviour
{
    public static PlayerListUI Instance;

    [SerializeField] private List<PlayerSlotUI> slots;

    void Awake()
    {
        Instance = this;
        Debug.Log("[PlayerListUI] Awake");
    }

    public void AddPlayer(ulong playerId, string nickname)
    {
        foreach (var slot in slots)
        {
            if (!slot.IsOccupied)
            {
                slot.SetPlayer(playerId, nickname);
                return;
            }
        }

        Debug.LogWarning("[UI] ∫Û ΩΩ∑‘¿Ã æ¯¿Ω");
    }
}
