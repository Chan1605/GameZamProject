using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SortRank : MonoBehaviour
{

    [SerializeField] SaveManager saveManager;

    private SaveData saveData;

    public Record curRecord;

    // 이번 플레이어의 기록을 생성한 후, 삽입 정렬 방식으로 기존 기록에 넣은 후 덮어씌우기
    // 인덱스 기반으로 관리

    private void Start()
    {
        this.saveData = saveManager.saveData;
    }

    private void InsertSort()
    {
        saveData.saveRecords.Add(curRecord);

        if (saveData.saveRecords.Count <= 1) // 기록에 자신뿐이라면
        {
            return;
        }

        //아니면 비교 시작
        for (int i = saveData.saveRecords.Count - 1; i > 0; i--) // 끝부터 시작 (지금 들어온 기록)
        {
            Record pastRank = saveData.saveRecords[i-1];

            if(pastRank.score < curRecord.score) // 지금 들어온 기록 i 보다 i-1의 기록이 작다면 뒤로 보낸다.
            {

            }
            else
            {
                break; // 지금 들어온 기록 i 보다 i-1의 기록이 크다면 반복 끝
            }


        }
    }



}
