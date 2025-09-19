using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class NPCMemoryData
{
    public string npcName;
    public string occupation;
    public List<DailyMemory> dailyMemories = new List<DailyMemory>();
}

[System.Serializable]
public class DailyMemory
{
    public int dayCount;
    public string saveDate; // ʵ�ʱ�������
    public List<string> memories = new List<string>();
}

public class MemoryManager : MonoBehaviour
{
    private NPCBehavior npc;

    private void Awake()
    {
        npc = GetComponent<NPCBehavior>();
    }

    private string MemoryBasePath => Path.Combine(Application.streamingAssetsPath, "NPCMemories");

    public void SaveMemoryToFile(NPCBehavior npc)
    {
        Debug.Log("���ñ�����亯��");
        string fileName = $"{npc.occupation}_{npc.npcName}.json";
        string filePath = Path.Combine(MemoryBasePath, fileName);

        // ȷ��Ŀ¼����
        if (!Directory.Exists(MemoryBasePath))
        {
            Directory.CreateDirectory(MemoryBasePath);
        }

        try
        {
            NPCMemoryData memoryData;

            // ��ȡ�������ݻ򴴽�������
            if (File.Exists(filePath))
            {
                string existingJson = File.ReadAllText(filePath);
                memoryData = JsonUtility.FromJson<NPCMemoryData>(existingJson);
            }
            else
            {
                memoryData = new NPCMemoryData
                {
                    npcName = npc.npcName,
                    occupation = npc.occupation
                };
            }

            // ��ȡ��ǰ����
            int currentDay = TimeManager.Instance.GetDayCount();

            // ����Ƿ����е���ļ���
            DailyMemory todayMemory = memoryData.dailyMemories.Find(d => d.dayCount == currentDay);

            if (todayMemory == null)
            {
                // �����µĵ��ռ���
                todayMemory = new DailyMemory
                {
                    dayCount = currentDay,
                    saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                memoryData.dailyMemories.Add(todayMemory);
            }
            else
            {
                // ���±���ʱ��
                todayMemory.saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            // ���µ��ռ���
            todayMemory.memories = npc.GetAllMemories();

            // �������30��ļ��䣬ɾ�����ɵļ���
            memoryData.dailyMemories.RemoveAll(d => currentDay - d.dayCount > 30);

            // ����������
            memoryData.dailyMemories.Sort((a, b) => a.dayCount.CompareTo(b.dayCount));

            // д��JSON�ļ�
            string json = JsonUtility.ToJson(memoryData, true);
            File.WriteAllText(filePath, json);

            Debug.Log($"�ɹ�����{npc.npcName}�ļ��䵽: {filePath}����ǰ��{currentDay}�죬��{todayMemory.memories.Count}������");
        }
        catch (Exception e)
        {
            Debug.LogError($"�������ʧ��: {e.Message}");
        }
    }

    public NPCMemoryData LoadMemoryFromFile(NPCBehavior npc)
    {
        string fileName = $"{npc.occupation}_{npc.npcName}.json";
        string filePath = Path.Combine(MemoryBasePath, fileName);

        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                NPCMemoryData memoryData = JsonUtility.FromJson<NPCMemoryData>(json);
                Debug.Log($"�ɹ���ȡ{npc.npcName}�ļ��䣬��{memoryData.dailyMemories.Count}��ļ�¼");
                return memoryData;
            }
            else
            {
                Debug.Log($"δ�ҵ�{npc.npcName}�ļ����ļ������������ļ�");
                return new NPCMemoryData
                {
                    npcName = npc.npcName,
                    occupation = npc.occupation
                };
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"��ȡ����ʧ��: {e.Message}");
            return new NPCMemoryData
            {
                npcName = npc.npcName,
                occupation = npc.occupation
            };
        }
    }

    // ��ȡָ�������ļ���
    public List<string> GetMemoriesForDay(NPCBehavior npc, int dayCount)
    {
        NPCMemoryData memoryData = LoadMemoryFromFile(npc);
        DailyMemory targetDay = memoryData.dailyMemories.Find(d => d.dayCount == dayCount);

        if (targetDay != null)
        {
            return new List<string>(targetDay.memories);
        }

        return new List<string>();
    }

    // ��ȡ�������ļ���ժҪ
  
}