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
            return;
        }

        Rigidbody carriage = consist.Locomotive.Body;
        Quaternion targetRot = carriage.rotation;
        Vector3 chainAnchor = carriage.position;

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

            Vector3 desiredAnchor = previous.transform.TransformPoint(previous.RearAnchorLocal)
                        - carriage.transform.forward * consist.Gap;
            Vector3 currentAnchor = car.transform.TransformPoint(car.FrontAnchorLocal);
            car.transform.position += desiredAnchor - currentAnchor;
        }

    }
    

}
