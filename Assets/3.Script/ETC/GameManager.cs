using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TrainConsist consist;
    [SerializeField] private ScrollController scroll;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverText;
    [SerializeField] private Key restartKey = Key.R;

    [Header("보너스 타임 (닭)")]
    [SerializeField] private float chickenBonusDuration = 3f;
    //[SerializeField] private GameObject[] trainVisuals;
    //[SerializeField] private GameObject chickenVisual;

    private bool bonusStarted;

    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();
        if (scroll == null) scroll = FindFirstObjectByType<ScrollController>();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (consist == null)
        {
            enabled = false;
            return;
        }

        // 머리가 트레일 없이 직접 부딪혔을 때만 게임오버
        consist.LocomotiveCrashed += HandleLocomotiveCrashed;
    }

    private void HandleLocomotiveCrashed(TrainCar car, Collision collision)
    {
        if (bonusStarted || IsGameOver) return;
        bonusStarted = true;
        StartCoroutine(ChickenBonusThenGameOver());
    }

    private IEnumerator ChickenBonusThenGameOver()
    {
        consist.ForceInvincible(chickenBonusDuration + 0.5f);

        if (consist.Locomotive != null && consist.Locomotive.TryGetComponent(out TrainController controller))
        {
            //controller.SetControllable(false);   // 조작(입력)만 비활성화, 이동은 유지
            controller.ActivateChickenVisual();  // 비주얼만 교체
        }

        yield return new WaitForSeconds(chickenBonusDuration);
        TriggerGameOver();
    }


    private void TriggerGameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        Debug.Log("게임오버! R 키로 재시작할 수 있습니다.");
        if (scroll != null) scroll.Stop();

        if (consist.Locomotive != null && consist.Locomotive.TryGetComponent(out TrainController controller))
        {
           // controller.enabled = false;
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverText != null) gameOverText.text = "GAME OVER\nPress R to Restart";
    }

    private void Update()
    {
        if (!IsGameOver) return;
        if (Keyboard.current != null && Keyboard.current[restartKey].wasPressedThisFrame)
        {
            Restart();
        }
    }

    private void Restart()
    {
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    private void OnDestroy()
    {
        if (consist != null)
        {
            consist.LocomotiveCrashed -= HandleLocomotiveCrashed;
        }
    }
}