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
    public string saveDate; // 实际保存日期
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
        Debug.Log("调用保存记忆函数");
        string fileName = $"{npc.occupation}_{npc.npcName}.json";
        string filePath = Path.Combine(MemoryBasePath, fileName);

        // 确保目录存在
        if (!Directory.Exists(MemoryBasePath))
        {
            Directory.CreateDirectory(MemoryBasePath);
        }

        try
        {
            NPCMemoryData memoryData;

            // 读取现有数据或创建新数据
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

            // 获取当前天数
            int currentDay = TimeManager.Instance.GetDayCount();

            // 检查是否已有当天的记忆
            DailyMemory todayMemory = memoryData.dailyMemories.Find(d => d.dayCount == currentDay);

            if (todayMemory == null)
            {
                // 创建新的当日记忆
                todayMemory = new DailyMemory
                {
                    dayCount = currentDay,
                    saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                memoryData.dailyMemories.Add(todayMemory);
            }
            else
            {
                // 更新保存时间
                todayMemory.saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            // 更新当日记忆
            todayMemory.memories = npc.GetAllMemories();

            // 保持最近30天的记忆，删除过旧的记忆
            memoryData.dailyMemories.RemoveAll(d => currentDay - d.dayCount > 30);

            // 按天数排序
            memoryData.dailyMemories.Sort((a, b) => a.dayCount.CompareTo(b.dayCount));

            // 写入JSON文件
            string json = JsonUtility.ToJson(memoryData, true);
            File.WriteAllText(filePath, json);

            Debug.Log($"成功保存{npc.npcName}的记忆到: {filePath}，当前第{currentDay}天，共{todayMemory.memories.Count}条记忆");
        }
        catch (Exception e)
        {
            Debug.LogError($"保存记忆失败: {e.Message}");
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
                Debug.Log($"成功读取{npc.npcName}的记忆，共{memoryData.dailyMemories.Count}天的记录");
                return memoryData;
            }
            else
            {
                Debug.Log($"未找到{npc.npcName}的记忆文件，将创建新文件");
                return new NPCMemoryData
                {
                    npcName = npc.npcName,
                    occupation = npc.occupation
                };
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"读取记忆失败: {e.Message}");
            return new NPCMemoryData
            {
                npcName = npc.npcName,
                occupation = npc.occupation
            };
        }
    }

    // 获取指定天数的记忆
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

    // 获取最近几天的记忆摘要
  
}