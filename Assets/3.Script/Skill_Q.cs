using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_Q : MonoBehaviour
{
    private TrainConsist consist;    

    public string SkillName = "[SerializeTrain]";

    private void Awake()
    {
        consist = FindFirstObjectByType<TrainConsist>();
    }

    public void Straighten()
    {
        if (consist == null || consist.Locomotive == null) 
        {
            return; //방어 코드
        }

        Rigidbody carriage = consist.Locomotive.Body;
        Quaternion targetRot = carriage.rotation; //객차의 회전
        Vector3 chainAnchor = carriage.position; //객차의 위치

        TrainCar previous = consist.Locomotive;

        foreach (TrainCar car in consist.Cars)
        {
            if (car.Equals(consist.Locomotive))
            {
                continue;
            }

            if (car.IsDetached)
            {
                break;
            }

            //객차들의 위치를 이전 객차들을 기준으로 이동
            Vector3 desiredAnchor = previous.Body.position 
                + previous.Body.rotation * previous.RearAnchorLocal
                - (carriage.rotation * Vector3.forward) * consist.Gap;
            Vector3 currentAnchor = car.Body.position + car.Body.rotation * car.FrontAnchorLocal;

            Vector3 correction = desiredAnchor - currentAnchor;
            car.Body.MovePosition(car.Body.position + correction);

            //객차들의 회전을 기관차와 동일하게 만들기
            car.Body.MoveRotation(targetRot);
            
        }

    }
    

}
