using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SortRank : MonoBehaviour
{

    [SerializeField] SaveManager saveManager;

    private SaveData saveData;

    // 이번 플레이어의 기록을 생성한 후, 삽입 정렬 방식으로 기존 기록에 넣은 후 덮어씌우기
    // 인덱스 기반으로 관리

    private void Start()
    {
        this.saveData = saveManager.saveData;
    }

    private void InsertSort()
    {

    }



}
