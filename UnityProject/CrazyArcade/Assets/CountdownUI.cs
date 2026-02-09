using TMPro;
using UnityEngine;
using System.Collections;

public class CountdownUI : MonoBehaviour
{
    public static CountdownUI Instance;
    public TextMeshProUGUI text;

    void Awake()
    {
        Instance = this;
        text.text = "Loading...";
    }

    // 숫자 카운트다운 표시
    public void SetCountdown(int value)
    {
        text.text = value.ToString();
    }

    // GameStart! → 1초 후 공백
    public void ShowGameStart()
    {
        StartCoroutine(GameStartRoutine());
    }

    private IEnumerator GameStartRoutine()
    {
        text.text = "GameStart!";
        yield return new WaitForSeconds(1f);
        text.text = ""; // 안 보이게
    }
}
