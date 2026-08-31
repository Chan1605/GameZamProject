using UnityEngine;

public class CollisionCameraShake : MonoBehaviour
{
    [SerializeField] private TrainConsist consist;
    [SerializeField] private CameraCtrl cam;
    [SerializeField] private float hitForce = 0.15f;
    [SerializeField] private float crashForce = 0.3f;

    private void Awake()
    {
        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();
        if (cam == null)
        {
            cam = FindAnyObjectByType<CameraCtrl>();
        }
        if (consist == null) return;

        consist.CouplerBroken += _ => cam?.Shake(0.12f, hitForce);
        consist.LocomotiveCrashed += (_, __) => cam?.Shake(0.2f, crashForce);
    }
}