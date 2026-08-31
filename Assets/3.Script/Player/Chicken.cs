using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Chicken : MonoBehaviour
{
    private Rigidbody rb;
    private Animator ani;

    [SerializeField] private TrainConsist consist;
    private TrainPathFollower follower;

    [Header("닭의 최대 비행 시간")]
    [Tooltip("닭의 최대 비행 시간")]
    [SerializeField] private float flightTime = 15f;

    [Header("각도 조작 (WASD)")]
    [SerializeField] private bool controlRotation = true;
    [SerializeField] private float pitchSpeed = 70f;
    [SerializeField] private float yawSpeed = 70f;
    [SerializeField] private float maxPitchAngle = 60f;
    [SerializeField] private float maxYawAngle = 60f;
    [SerializeField] private bool invertPitchInput = false;
    [SerializeField] private bool invertYawInput = false;
    [SerializeField] private float rotationSmoothSpeed = 10f;

    [Header("점프 설정")]
    [SerializeField] private float jumpHeight = 10f;

    [Header("충돌 판정")]
    [SerializeField] private LayerMask obstacleLayers = 0;

    private Coroutine flight_co;
    private float jumpVelocity;
    private float initialYaw;
    private float initialRoll;
    private float targetPitch;
    private float targetYawOffset;

    private bool isFlying;

    public event Action FlightEnded;

    private void Awake()
    {
        TryGetComponent(out rb);
        TryGetComponent(out ani);
        TryGetComponent(out follower);

        RecalculateJumpVelocity();

        if (controlRotation)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }

        gameObject.SetActive(false);
    }

    private void RecalculateJumpVelocity()
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        jumpVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
    }

    private void Update()
    {
        if (!isFlying)
        {
            return; // 비행 종료 후엔 입력 무시
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) 
        {
            return;
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            Jump();
        }
        if (controlRotation)
        {
            ReadRotationInput(keyboard);
        }
    }

    private void Jump()
    {

        if (rb == null || rb.isKinematic)
        {
            return;
        }


        Vector3 velocity = rb.linearVelocity;
        velocity.y = jumpVelocity;
        rb.linearVelocity = velocity;

    }

    private void ReadRotationInput(Keyboard keyboard)
    {
        float pitchInput = 0f;
        if (keyboard.wKey.isPressed) pitchInput += 1f;
        if (keyboard.sKey.isPressed) pitchInput -= 1f;
        if (invertPitchInput) pitchInput *= -1f;

        float yawInput = 0f;
        if (keyboard.dKey.isPressed) yawInput += 1f;
        if (keyboard.aKey.isPressed) yawInput -= 1f;
        if (invertYawInput) yawInput *= -1f;

        targetPitch = Mathf.Clamp(targetPitch + pitchInput * pitchSpeed * Time.deltaTime, -maxPitchAngle, maxPitchAngle);
        targetYawOffset = Mathf.Clamp(targetYawOffset + yawInput * yawSpeed * Time.deltaTime, -maxYawAngle, maxYawAngle);
    }

    private void FixedUpdate()
    {
        if (isFlying && controlRotation) ApplyRotation();
    }

    private void ApplyRotation()
    {
        Quaternion targetRotation = Quaternion.Euler(targetPitch, initialYaw + targetYawOffset, initialRoll);
        Quaternion next = Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.fixedDeltaTime));

        rb.MoveRotation(next);
    }

    public void LastFlight(TrainCar car) //기차가 사라지는 순간에 호출해서 값을 받는 코드
    {
        TrainPathFollower locofollower = car.GetComponent<TrainPathFollower>();
        //
        Vector3 position = car.Body.position;
        Quaternion rotation = car.Body.rotation;
        IReadOnlyList<Vector3> snapPath = locofollower != null ? locofollower.RawWaypoints : null;

        gameObject.SetActive(true);

        rb.position = position;
        rb.rotation = rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 startEuler = rotation.eulerAngles;
        initialYaw = startEuler.y;
        initialRoll = startEuler.z;
        targetPitch = 0f;
        targetYawOffset = 0f;

        if (snapPath != null && follower != null)
        {
            follower.SetPath(snapPath);
            follower.ExternalOverride = false;
        }

        StartFlightTimer();
    }

    private void StartFlightTimer()
    {
        isFlying = true;

        if (flight_co != null) StopCoroutine(flight_co);
        flight_co = StartCoroutine(FlightTimer());
    }

    private void OnEnable()
    {
        StartFlightTimer();
    }

    private void OnDisable()
    {
        if (flight_co != null)
        {
            StopCoroutine(flight_co);
            flight_co = null;
        }
    }

    private IEnumerator FlightTimer()
    {
        yield return new WaitForSeconds(flightTime);
        StopFlying();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isFlying) return;
        if (obstacleLayers.value == 0) return;
        if ((obstacleLayers.value & (1 << collision.gameObject.layer)) == 0) return;

        StopFlying();
    }

    private void StopFlying()
    {
        isFlying = false;

        if (follower != null)
        {
            follower.ExternalOverride = true; // 경로 추종/컨트롤 정지 → 이후 FixedUpdate가 속도를 건드리지 않음
        }

        if (flight_co != null)
        {
            StopCoroutine(flight_co);
            flight_co = null;
        }

        FlightEnded?.Invoke();
    }
}
