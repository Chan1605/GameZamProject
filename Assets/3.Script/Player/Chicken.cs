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

    [Header("점프 설정")]
    [SerializeField] private float jumpHeight = 10f;

    private Coroutine flight_co;
    private float jumpVelocity;
    private bool isFlying;

    private void Awake()
    {
        TryGetComponent(out rb);
        TryGetComponent(out ani);
        TryGetComponent(out follower);

        RecalculateJumpVelocity();

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

    public void LastFlight(TrainCar car) //기차가 사라지는 순간에 호출해서 값을 받는 코드
    {
        TrainPathFollower locofollower = car.GetComponent<TrainPathFollower>();
        //
        Vector3 position = car.Body.position;
        Quaternion rotation = car.Body.rotation;
        IReadOnlyList<Vector3> snapPath = locofollower != null ? locofollower.RawWaypoints : null;

        rb.position = position;
        rb.rotation = rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (snapPath != null && follower != null)
        {
            follower.SetPath(snapPath);
            follower.ExternalOverride = false;
        }

        StartFlightTimer();

        gameObject.SetActive(true);

        //기차 사라지는 부분 추가 필요함
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

    private void StopFlying()
    {
        isFlying = false;

        if (follower != null)
        {
            follower.ExternalOverride = true; // 경로 추종/컨트롤 정지 → 이후 FixedUpdate가 속도를 건드리지 않음
        }
    }
}
