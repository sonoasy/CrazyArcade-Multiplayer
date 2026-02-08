using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class NicknameSceneUI : MonoBehaviour
{
    public TMP_InputField nicknameInput;

    public void OnConfirm()
    {
        string nickname = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(nickname))
            nickname = "Player";

        // NetworkClient에 닉네임 저장
        //NetworkClient.Instance.myNickname = nickname;
        PlayerPrefs.SetString("NICKNAME", nickname);
        // 게임 씬으로 이동
        SceneManager.LoadScene("Aqua");
    }
}
