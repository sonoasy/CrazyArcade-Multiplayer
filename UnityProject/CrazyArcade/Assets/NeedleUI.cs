using TMPro;
using UnityEngine;

public class NeedleUI : MonoBehaviour
{
    public static NeedleUI Instance;
    public TextMeshProUGUI needleCountText;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetCount(int count)
    {
        if (needleCountText == null)
        {
            Debug.LogError("[NeedleUI] needleCountText ¿¬°á ¾È µÊ");
            return;
        }

        if (count <= 0)
        {
            needleCountText.text = "x0";
            needleCountText.gameObject.SetActive(false);
        }
        else
        {
            needleCountText.gameObject.SetActive(true);
            needleCountText.text = $"x{count}";
        }
    }
}
