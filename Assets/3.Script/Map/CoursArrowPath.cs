using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoursArrowPath : MonoBehaviour
{
    // 이동 포인트와 현재 포인트의 방향 추출하여 조정
    // Instantiate 한 후에 함수호출하여 매개변수 전달해주세요.
    public void Init(Vector3 position, Vector3 nextPoint)
    {
        transform.position = position;
        SetArrowRotation(nextPoint);
    }

    public void SetArrowRotation(Vector3 nextPoint) 
    {
        Vector3 direction = (nextPoint - transform.position);
        Quaternion arrowRotation = Quaternion.LookRotation(direction);

        transform.rotation = arrowRotation;
    }

}
