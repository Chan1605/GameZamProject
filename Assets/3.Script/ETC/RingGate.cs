using UnityEngine;


public class RingGate : MonoBehaviour
{

    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private TrainConsist consist;

    [SerializeField] private bool scoreOncePerActivation = true;

    private bool scored;

    private void OnEnable()
    {
  
        scored = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (scoreOncePerActivation && scored) return;


        var controller = other.GetComponentInParent<TrainController>();
        if (controller == null) return;

        scored = true;

        if (scoreManager == null) scoreManager = FindFirstObjectByType<ScoreManager>();
        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();

        if (scoreManager != null)
        {
            // 트레일(머리 제외) 개수만큼 배율. 트레일 다 잃으면 0점.
            int carCount = consist != null ? consist.AttachedCarCount : 1;
            scoreManager.AddScore(Mathf.Max(1, carCount));
        }
    }

    
}