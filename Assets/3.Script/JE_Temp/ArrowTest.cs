using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowTest : MonoBehaviour
{
    public Vector3 spawnPoint;
    public Vector3 nextPoint;

    public GameObject arrowprepeb;

    private void Awake()
    {
        spawnPoint = transform.position;
        InstantiateArrow();
    }

    private void InstantiateArrow()
    {
        GameObject newArrow=Instantiate(arrowprepeb);
        newArrow.GetComponent<CoursArrowPath>().Init(spawnPoint, nextPoint);
    }
}
