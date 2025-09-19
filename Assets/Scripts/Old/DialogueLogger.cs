// DialogueLogger.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DialogueLogger : MonoBehaviour
{
    public static DialogueLogger Instance { get; private set; }

    [System.Serializable]
    public class DialogueEntry
    {
        public string timestamp;
        public string realTime;
        public string speaker;
        public string listener;
        public string content;
        public string location;
    }

    public List<DialogueEntry> history = new List<DialogueEntry>();

    // 添加自动保存功能
    private const string SAVE_PATH = "NPC_Dialogues.json";
    private const float SAVE_INTERVAL = 30f; // 每30秒保存一次
    private float lastSaveTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("DialogueLogger 实例创建");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        lastSaveTime = Time.time;
        // 尝试加载已有对话
        LoadDialogueHistory();
    }

    void Update()
    {
        // 定期自动保存
        if (Time.time - lastSaveTime > SAVE_INTERVAL)
        {
            SaveDialogueHistory();
            lastSaveTime = Time.time;
        }
    }

    public void LogDialogue(string speaker, string listener, string content, Vector3 location)
    {
        // 确保时间管理器存在
        string timestamp = "00:00";
        if (TimeManager.Instance != null)
        {
            timestamp = TimeManager.Instance.GetCurrentTime();
        }
        else
        {
            Debug.LogWarning("TimeManager 不存在，使用默认时间");
        }

        DialogueEntry entry = new DialogueEntry
        {
            timestamp = timestamp,
            realTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            speaker = speaker,
            listener = listener,
            content = content,
            location = $"{location.x:F1},{location.y:F1}"
        };

        history.Add(entry);
        Debug.Log($"[对话] {speaker} → {listener}: {content}");

        // 立即保存（可选）
        // SaveDialogueHistory();
    }

    public void SaveDialogueHistory()
    {
        if (history.Count == 0) return;
        string projectPath = Path.GetDirectoryName(Application.dataPath);
        string folderPath = Path.Combine(projectPath, "Dialogues");
        string json = JsonUtility.ToJson(new Wrapper<DialogueEntry> { Items = history }, true);
        string path = Path.Combine(folderPath, SAVE_PATH);

        try
        {
            //File.WriteAllText(path, json);
            //Debug.Log($"对话历史已保存到: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存对话历史失败: {e.Message}");
        }
    }

    private void LoadDialogueHistory()
    {
        string path = Path.Combine(Application.persistentDataPath, SAVE_PATH);
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                Wrapper<DialogueEntry> wrapper = JsonUtility.FromJson<Wrapper<DialogueEntry>>(json);
                history = wrapper.Items;
                Debug.Log($"已加载历史对话: {history.Count} 条记录");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"加载对话历史失败: {e.Message}");
            }
        }
    }

    void OnApplicationQuit()
    {
        SaveDialogueHistory();
    }

    // 辅助类用于序列化列表
    [System.Serializable]
    private class Wrapper<T>
    {
        public List<T> Items;
    }
}