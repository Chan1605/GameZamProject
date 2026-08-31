using UnityEngine;
using UnityEngine.UI;


public class ScoreManager : MonoBehaviour
{
        
    [SerializeField] private Text scoreText;

    public int Score { get; private set; }

    private void Start()
    {
        RefreshUI();
    }

    public void AddScore(int amount)
    {
        Score += amount;
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
