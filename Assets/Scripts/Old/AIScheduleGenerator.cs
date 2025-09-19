// AIScheduleGenerator.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static NPCBehavior;

[System.Serializable]
public class AIGeneratedSchedule
{
    public string npcName;
    public string date;
    public List<ScheduleItem> activities;
    public string reasoning; // AI的决策理由
}

[System.Serializable]
public class ScheduleItem
{
    public string startTime; // "08:00"
    public int duration; // 分钟
    public string activity;
    public string location;
    public string description; // 详细描述
    //public float priority; // 1-10 优先级

    // 转换为DailyActivity
    public DailyActivity ToDailyActivity(Dictionary<string, Vector3> locationMap)
    {
        var parts = startTime.Split(':');
        int hour = int.Parse(parts[0]);
        int minute = int.Parse(parts[1]);

        Vector3 position = locationMap.ContainsKey(location) ?
            locationMap[location] : Vector3.zero;

        return new DailyActivity(hour, minute, duration, activity, position);
    }
}

public class AIScheduleGenerator : MonoBehaviour
{
    private static AIScheduleGenerator instance;
    public static AIScheduleGenerator Instance => instance;

    [Header("位置映射")]
    public Dictionary<string, Vector3> locationMap = new Dictionary<string, Vector3>();

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void GenerateScheduleForNPC(NPCBehavior npc, System.Action<List<DailyActivity>> onComplete)
    {
        StartCoroutine(GenerateScheduleCoroutine(npc, onComplete));
    }

    private System.Collections.IEnumerator GenerateScheduleCoroutine(NPCBehavior npc, System.Action<List<DailyActivity>> onComplete)
    {
        // 生成日程表
        // 构建提示词
        string prompt = BuildSchedulePrompt(npc);

        // 调用AI
        var npcData = new NPCData(npc);
        bool responseReceived = false;
        string aiResponse = "";

        yield return AzureOpenAIManager.Instance.GetNPCResponse(npcData, prompt, (response) =>
        {
            aiResponse = response;
            responseReceived = true;
        });

        while (!responseReceived)
        {
            yield return null;
        }

        // 解析AI响应
        try
        {
            // 检查响应是否完整
            if (!aiResponse.EndsWith("}") || !aiResponse.Contains("\"reasoning\""))
            {
                Debug.LogWarning($"[AISchedule] AI响应不完整，使用默认日程");
                onComplete?.Invoke(GetDefaultSchedule(npc));
                yield break;
            }

            var schedule = ParseScheduleResponse(aiResponse, npc.npcName);
            SaveSchedule(schedule);
            var activities = ConvertToActivities(schedule);
            onComplete?.Invoke(activities);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AISchedule] 解析日程失败: {e.Message}");
            onComplete?.Invoke(GetDefaultSchedule(npc));
        }
    }


    private string BuildSchedulePrompt(NPCBehavior npc)
    {
        // 获取NPCStatus组件
        //var status = npc.GetComponent<NPCStatus>();//不再需要NPCStatus组件
        string statusRule = "每工作1小时消耗20点体力同时获取1个资源。未工作满1小时不会获得任何资源。每休息1小时回复10点体力。前往集市可以和收购商谈判卖出资源获得金币,资源为0不需要去集市。由于谈判较花时间，可以选择获得资源后立即卖出，也可以选择积攒一阵子再卖以免耽误工作，根据你的性格决定。";// 可以明确提示状态值的扣除规则

        string statusInfo = $@"- 体力值：{npc.energy:F0}/{npc.maxEnergy:F0}
- 金钱：{npc.tips}金币";

        string prompt = $@"你需要为{npc.npcName}制定今天的日程安排。

角色信息：
- 姓名：{npc.npcName}
- 职业：{npc.occupation}
- 性格：{npc.personality}
{statusInfo}
- 当前时间：早上8:00

城镇中仅有以下地点（必须从中选择）：
- home：家（休息，晚上睡觉）
- lumberyard：伐木场（伐木工工作地点）
- market：集市（交易，固定花费2小时）
当前的规则是：{statusRule}
请生成一个从8:00到22:00的日程表，格式如下：
{{
  ""activities"": [
    {{
      ""startTime"": ""08:00"",
      ""duration"": 30,
      ""activity"": ""工作"",
      ""location"": ""lumberyard"",
      ""description"": ""在伐木场工作"",                                                                     
      ""priority"": 8
    }},
    // 更多活动...
  ],
  ""reasoning"": ""考虑到角色的职业和当前状态...""
}}

注意事项：
1. location字段必须是上述3个地点之一
2. 时间要连续，不能有空隙
3. 最后一个活动应该在家休息
4. 日程必须从8:00开始，到22:00结束，不要出现非整小时行程

重要：必须返回有效的JSON格式，确保：
- 使用英文逗号和冒号
- 字符串使用双引号
- 数组最后一个元素后不要有逗号
- 不要在JSON前后添加任何说明文字

只返回JSON数据：";
        //需要修改
        /* References:As {character}, engage in a dialogue with the
        objective of {objective}. Respond to the
        conversation using the given context or memories
        and limit your response to under 50 words. Please
        submit your response in JSON format.
        YOU are: {character}
        {event description}
        Initiates a conversation with {user’s
        character}.
        Here is your memory : {memory}
        Give your response in format {“response
        ”: “here is the response”}.....*/ 
        return prompt;
    }


    
    private AIGeneratedSchedule ParseScheduleResponse(string response, string npcName)
    {
        // 清理响应，提取JSON部分
        response = response.Trim();
        int startIndex = response.IndexOf('{');
        int endIndex = response.LastIndexOf('}');

        if (startIndex >= 0 && endIndex >= 0)
        {
            response = response.Substring(startIndex, endIndex - startIndex + 1);
        }

        var schedule = JsonUtility.FromJson<AIGeneratedSchedule>(response);
        schedule.npcName = npcName;
        schedule.date = DateTime.Now.ToString("yyyy-MM-dd");

        return schedule;
    }

    private void SaveSchedule(AIGeneratedSchedule schedule)
    {
        string projectPath = Path.GetDirectoryName(Application.dataPath);
        string folderPath = Path.Combine(projectPath, "NPCSchedules");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = $"{schedule.npcName}_{schedule.date}.json";
        string filePath = Path.Combine(folderPath, fileName);

        string json = JsonUtility.ToJson(schedule, true);
        File.WriteAllText(filePath, json);

        Debug.Log($"[AISchedule] 日程已保存到: {filePath}");
    }

    private List<DailyActivity> ConvertToActivities(AIGeneratedSchedule schedule)
    {
        var activities = new List<DailyActivity>();

        foreach (var item in schedule.activities)
        {
            activities.Add(item.ToDailyActivity(locationMap));
        }

        return activities;
    }

    private List<DailyActivity> GetDefaultSchedule(NPCBehavior npc)
    {
        // 返回默认日程作为备用
        return new List<DailyActivity>
        {
            new DailyActivity(8, 0, 30, "吃早餐", Vector3.zero),
            new DailyActivity(8, 30, 240, "工作", Vector3.zero),
            new DailyActivity(12, 30, 30, "吃午餐", Vector3.zero),
            new DailyActivity(13, 0, 240, "工作", Vector3.zero),
            new DailyActivity(17, 0, 30, "吃晚餐", Vector3.zero),
            new DailyActivity(17, 30, 270, "休息", Vector3.zero)
        };
    }
}