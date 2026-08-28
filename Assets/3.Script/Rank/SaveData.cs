using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData // 직렬화 될 전체 데이터입니다.
{
    public List<Record> saveRecords;
}

[System.Serializable] // 플레이어별로 저장하여 랭크를 매길 데이터입니다.
public class Record
{
    public string name;
    public int score;
}

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance = null;

    public SaveData saveData; // 저장 파일
    public string path;

    private void Awake()
    {
        if(Instance ==null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (!Directory.Exists(Application.persistentDataPath + "/TraInCity/"))
        {
            Directory.CreateDirectory(Application.persistentDataPath + "/TraInCity/");
        }

        path = Application.persistentDataPath + "/TraInCity/SaveData.json";
        LoadData();

    }

    public void LoadData()
    {
        if(!File.Exists(path))
        {
            saveData = new SaveData();
            saveData.saveRecords = new List<Record>();
        }
        else
        {
            string saveFile = File.ReadAllText(path);
            saveData = JsonUtility.FromJson<SaveData>(saveFile);
        }
    }

    public void SaveGame()
    {
        File.WriteAllText(path, JsonUtility.ToJson(saveData));
        Debug.Log($"{path} 경로에 저장됨");
    }

}