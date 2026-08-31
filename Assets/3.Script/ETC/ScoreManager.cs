using UnityEngine;
using UnityEngine.UI;


public class ScoreManager : MonoBehaviour
{
        
    [SerializeField] private Text scoreText;
    [SerializeField] private PlayerSkillPoint skillPoint;

    public int Score { get; private set; }

    private void Start()
    {
        RefreshUI();
    }

    private void OnDestroy()
    {
        if(SaveManager.Instance != null)
        SaveManager.Instance.PlayerScore = Score;
    }

    public void AddScore(int amount)
    {
        Score += amount;
        skillPoint.AddSkillPoint();

        Debug.Log($"현재 점수: {Score}");
        RefreshUI();
    }

    public void ResetScore()
    {
        Score = 0;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (scoreText != null) scoreText.text = $"Score : {Score.ToString()}";
    }
}
