using TMPro;
using UnityEngine;

public class PlayerSlotUI : MonoBehaviour
{
    public TextMeshProUGUI nicknameText;
    public bool IsOccupied { get; private set; }

    public void SetPlayer(ulong playerId, string nickname)
    {
        nicknameText.text = nickname;
        IsOccupied = true;
        gameObject.SetActive(true);
    }
}
