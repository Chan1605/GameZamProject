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
        Vector3 chainAnchor = carriage.position + targetRot * consist.Locomotive.RearAnchorLocal
                     - (targetRot * Vector3.forward) * consist.Gap; //다음 차량이 붙어야 할 좌표

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
            Vector3 currentAnchor = car.Body.position + targetRot * car.FrontAnchorLocal;
            Vector3 newPos = car.Body.position + (chainAnchor - currentAnchor);
            //newPos로 따로 저장해서 지연 없이 확실하게 반영되게 만듦
            car.Body.MovePosition(newPos);

            //객차들의 회전을 기관차와 동일하게 만들기
            car.Body.MoveRotation(targetRot);

            //다음 차량이 붙어야 할 자리를 계산함
            chainAnchor = newPos + targetRot * car.RearAnchorLocal - (targetRot * Vector3.forward) * consist.Gap;
        }

    }
    

}
