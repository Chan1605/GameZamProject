using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TrainConsist consist;
    [SerializeField] private ScrollController scroll;
    [SerializeField] private CameraCtrl cameraCtrl;
    [SerializeField] private Chicken chicken;
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
        if (cameraCtrl == null) cameraCtrl = FindFirstObjectByType<CameraCtrl>();
        if (chicken == null) chicken = FindAnyObjectByType<Chicken>(FindObjectsInactive.Include);
        //비활성화 상태로 있어서 평범하게는 검색이 안 됨
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (consist == null)
        {
            enabled = false;
            return;
        }

        // 머리가 트레일 없이 직접 부딪혔을 때만 게임오버
        consist.LocomotiveCrashed += HandleLocomotiveCrashed;

        if (chicken != null)
        {
            chicken.FlightEnded += HandleFlightEnded; // ← 추가
        }
    }

    private void Start()
    {
        SoundMgr.Instance.SoundOnOff(true) ;
    }

    private void HandleLocomotiveCrashed(TrainCar car, Collision collision)
    {
        chicken.LastFlight(car);
        if (cameraCtrl != null)
        {
            cameraCtrl.SetTarget(chicken.transform);
        }
        car.gameObject.SetActive(false);
    }

    private void HandleFlightEnded()
    {
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

        SceneManager.LoadScene("Result");
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

        if (chicken != null)
        {
            chicken.FlightEnded -= HandleFlightEnded; 
        }
    }
}