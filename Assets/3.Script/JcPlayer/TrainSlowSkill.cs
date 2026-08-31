using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class TrainSlowSkill : MonoBehaviour
{
    [SerializeField] private TrainConsist consist;
    [SerializeField] private TrainPathFollower pathFollower;

    [Header("입력")]
    [SerializeField] private Key SkillKey = Key.E;

    [Header("조준")]
    [SerializeField, Range(0.05f, 1f)] private float SlowScale = 0.25f;

    [Header("돌진")]
    [SerializeField] private float bashForce = 70f;
    [SerializeField] private float bashDuration = 0.35f;
    [SerializeField] private float invincibleDuration = 0.4f;

    private Rigidbody rb;
    private bool isAiming;
    private bool isBashing;
    private Coroutine bashRoutine;

    public bool IsAiming => isAiming;
    public bool IsBashing => isBashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (consist == null) consist = GetComponent<TrainConsist>();
        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();
        if (pathFollower == null) pathFollower = GetComponent<TrainPathFollower>();
        if (pathFollower == null) pathFollower = FindFirstObjectByType<TrainPathFollower>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[SkillKey].wasPressedThisFrame && !isBashing)
        {
            StartAim();
        }
        else if (keyboard[SkillKey].wasReleasedThisFrame && isAiming)
        {
            ReleaseBash();
        }
    }
    private void FixedUpdate()
    {
        if (!isAiming || consist == null) return;
        foreach (var car in consist.Cars)
        {
            if (car == null || car.IsDetached || car.Body == rb) continue;
            car.Body.MoveRotation(rb.rotation);
        }
    }

    private void StartAim()
    {
        isAiming = true;
        Time.timeScale = SlowScale;
        if (pathFollower != null) pathFollower.ExternalOverride = true;

        // 속도를 0으로 누르는 대신, 아예 물리 시뮬레이션에서 빼서 완전히 고정
        SetAllKinematic(true);
    }

    private void ReleaseBash()
    {
        isAiming = false;
        Time.timeScale = 1f;
        SetAllKinematic(false);

        Vector3 direction = transform.forward;
        if (bashRoutine != null) StopCoroutine(bashRoutine);
        bashRoutine = StartCoroutine(BashRoutine(direction));
    }

    private IEnumerator BashRoutine(Vector3 direction)
    {
        isBashing = true;
        consist?.ForceInvincible(invincibleDuration);

        Vector3 velocity = direction.normalized * bashForce;
        ApplyVelocity(rb, velocity);

        if (consist != null)
        {
            foreach (var car in consist.Cars)
            {
                if (car == null || car.IsDetached) continue;
                if (car.Body == rb) continue;
                ApplyVelocity(car.Body, velocity);
            }
        }

        yield return new WaitForSeconds(bashDuration);

        if (pathFollower != null) pathFollower.ExternalOverride = false;
        isBashing = false;
        bashRoutine = null;
    }

    private void SetAllKinematic(bool kinematic)
    {
        rb.isKinematic = kinematic;
        if (consist == null) return;

        foreach (var car in consist.Cars)
        {
            if (car == null || car.IsDetached) continue;
            if (car.Body == rb) continue;
            car.Body.isKinematic = kinematic;
        }
    }

    private static void ApplyVelocity(Rigidbody body, Vector3 velocity)
    {
        if (body == null || body.isKinematic) return;
        body.linearVelocity = velocity;
    }

    private void OnDisable()
    {
        if (isAiming)
        {
            Time.timeScale = 1f;
            SetAllKinematic(false);
        }
    }
}