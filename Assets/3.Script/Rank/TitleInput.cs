
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleInput : MonoBehaviour
{
    [SerializeField] private InputField inputField;
    [SerializeField] private Button startButton;

    [SerializeField] private CanvasGroup BeforeInputUI;
    [SerializeField] private CanvasGroup AfterInputUI;


    public void OnEnable()
    {
        BeforeInputUI.gameObject.SetActive(true);
        AfterInputUI.gameObject.SetActive(false);
    }

    public void OnclickSubmit()
    {
        SaveManager.Instance.PlayerName = inputField.text;

        BeforeInputUI.gameObject.SetActive(false);
        AfterInputUI.gameObject.SetActive(true);
    }

    public void OnclickGameStart()
    {
        // 게임 씬 로드 메서드
        Debug.Log($"이름 : {SaveManager.Instance.PlayerName} 로 게임을 시작합니다.");
        SceneManager.LoadScene("Result"); //나중에 게임 씬으로 바꿔야 함
    }

    public void Debug_RandomScore()
    {
        SaveManager.Instance.PlayerScore = Random.Range(1, 30000);
    }
}
