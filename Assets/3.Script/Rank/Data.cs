using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData // 직렬화 될 전체 데이터입니다.
{
    public List<Record> saveRecords=new List<Record>();
}

[System.Serializable] // 플레이어별로 저장하여 랭크를 매길 데이터입니다.
public class Record
{
    public string name;
    public int score;
}
