using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chicken : MonoBehaviour
{
    private Rigidbody rb;
    private Animator ani;

    [SerializeField] private TrainConsist consist;
    private TrainPathFollower follower;

    [Header("닭의 최대 비행 시간")]
    [Tooltip("닭의 최대 비행 시간")]
    [SerializeField] private float flightTime = 15f;

    private void Awake()
    {
        TryGetComponent(out rb);
        TryGetComponent(out ani);
        TryGetComponent(out follower);

        gameObject.SetActive(false);
    }

    public void LastFlight(TrainCar car) //기차가 사라지는 순간에 호출해서 값을 받는 코드
    {
        follower = car.GetComponent<TrainPathFollower>();
        //
        Vector3 position = car.Body.position;
        Quaternion rotation = car.Body.rotation;
        //IReadOnlyList<Vector3> snapPath = follower != null ? follower.RawWaypoints : null;

        rb.position = position;
        rb.rotation = rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        //if (snapPath != null)
        //{
        //    follower.SetPath(snapPath);
        //}

        gameObject.SetActive(true);

        //기차 사라지는 부분 추가 필요함
    }

    private void OnEnable()
    {
        
    }
}
