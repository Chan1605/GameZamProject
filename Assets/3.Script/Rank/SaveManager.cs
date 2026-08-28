using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance = null;

    public SaveData saveData; // 저장 파일
    private string path;

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