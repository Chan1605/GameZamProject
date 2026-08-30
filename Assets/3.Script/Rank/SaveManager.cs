using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance = null;

    public SaveData saveData; // 저장 파일
    private string path;

    public string PlayerName; 
    public int PlayerScore; // 스코어 관리 매니저에서 게임 오버 시에 부여해주세요.

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
        Debug.Log($"{path} 경로 로드 시도");
        LoadData();

    }

    public void LoadData()
    {
        if(!File.Exists(path))
        {
            saveData = new SaveData();
        }
        else
        {
            string saveFile = File.ReadAllText(path);
            saveData = JsonUtility.FromJson<SaveData>(saveFile);

            if (saveData == null)
            {
                saveData = new SaveData();
            }
            if (saveData.saveRecords == null)
            {
                saveData.saveRecords = new List<Record>();
            }
        }
    }

    public void SaveGame()
    {
        File.WriteAllText(path, JsonUtility.ToJson(saveData));
        Debug.Log($"{path} 경로에 저장됨");
    }

}