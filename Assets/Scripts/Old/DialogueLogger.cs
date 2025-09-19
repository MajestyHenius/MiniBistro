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

    // ����Զ����湦��
    private const string SAVE_PATH = "NPC_Dialogues.json";
    private const float SAVE_INTERVAL = 30f; // ÿ30�뱣��һ��
    private float lastSaveTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("DialogueLogger ʵ������");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        lastSaveTime = Time.time;
        // ���Լ������жԻ�
        LoadDialogueHistory();
    }

    void Update()
    {
        // �����Զ�����
        if (Time.time - lastSaveTime > SAVE_INTERVAL)
        {
            SaveDialogueHistory();
            lastSaveTime = Time.time;
        }
    }

    public void LogDialogue(string speaker, string listener, string content, Vector3 location)
    {
        // ȷ��ʱ�����������
        string timestamp = "00:00";
        if (TimeManager.Instance != null)
        {
            timestamp = TimeManager.Instance.GetCurrentTime();
        }
        else
        {
            Debug.LogWarning("TimeManager �����ڣ�ʹ��Ĭ��ʱ��");
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
        Debug.Log($"[�Ի�] {speaker} �� {listener}: {content}");

        // �������棨��ѡ��
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
            //Debug.Log($"�Ի���ʷ�ѱ��浽: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"����Ի���ʷʧ��: {e.Message}");
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
                Debug.Log($"�Ѽ�����ʷ�Ի�: {history.Count} ����¼");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"���ضԻ���ʷʧ��: {e.Message}");
            }
        }
    }

    void OnApplicationQuit()
    {
        SaveDialogueHistory();
    }

    // �������������л��б�
    [System.Serializable]
    private class Wrapper<T>
    {
        public List<T> Items;
    }
}