using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultUI : MonoBehaviour
{
    [SerializeField] private Text playerName_t;
    [SerializeField] private Text playerScore_t;

    [SerializeField] private Text[] rankerName_t = new Text[10];
    [SerializeField] private Text[] rankerScore_t = new Text[10];

    private void Start()
    {
        UpdatePlayerUI();
    }

    public void UpdatePlayerUI()
    {
        playerName_t.text = SaveManager.Instance.PlayerName;
        playerScore_t.text = SaveManager.Instance.PlayerScore.ToString();
    }

    public void UpdateRanking()
    {
        for(int i = 0; i < SaveManager.Instance.saveData.saveRecords.Count; i++)
        {
            if(i >= rankerName_t.Length)
            {
                break;
            }

            rankerName_t[i].text = SaveManager.Instance.saveData.saveRecords[i].name;
            rankerScore_t[i].text = SaveManager.Instance.saveData.saveRecords[i].score.ToString();
        }
    }

    public void OnclickToTitle()
    {
        Debug.Log("버튼 눌림");
        SceneManager.LoadScene("Title"); 
    }
}
