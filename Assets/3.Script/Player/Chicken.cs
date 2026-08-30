using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chicken : MonoBehaviour
{
    private Rigidbody rb;
    private Animator ani;

    private PlayerInupt input;
    private TrainConsist consist;
    private TrainCar train;
    private TrainController controller; //이 코드는 연동되는 걸 실험하기 위해 만든 코드입니다.
    //나중에 제대로 연결되면 삭제하세요.

    [Header("닭의 최대 비행 시간")]
    [Tooltip("닭의 최대 비행 시간")]
    [SerializeField] private float flightTime = 15f;

    private void Awake()
    {
        TryGetComponent(out rb);
        TryGetComponent(out ani);

        consist = FindFirstObjectByType<TrainConsist>();
        if (consist != null)
        {
            train = consist.Locomotive;
        }
    }

}
