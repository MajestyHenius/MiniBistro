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
    public string reasoning; // AI�ľ�������
}

[System.Serializable]
public class ScheduleItem
{
    public string startTime; // "08:00"
    public int duration; // ����
    public string activity;
    public string location;
    public string description; // ��ϸ����
    //public float priority; // 1-10 ���ȼ�

    // ת��ΪDailyActivity
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

    [Header("λ��ӳ��")]
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
        // �����ճ̱�
        // ������ʾ��
        string prompt = BuildSchedulePrompt(npc);

        // ����AI
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

        // ����AI��Ӧ
        try
        {
            // �����Ӧ�Ƿ�����
            if (!aiResponse.EndsWith("}") || !aiResponse.Contains("\"reasoning\""))
            {
                Debug.LogWarning($"[AISchedule] AI��Ӧ��������ʹ��Ĭ���ճ�");
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
            Debug.LogError($"[AISchedule] �����ճ�ʧ��: {e.Message}");
            onComplete?.Invoke(GetDefaultSchedule(npc));
        }
    }


    private string BuildSchedulePrompt(NPCBehavior npc)
    {
        // ��ȡNPCStatus���
        //var status = npc.GetComponent<NPCStatus>();//������ҪNPCStatus���
        string statusRule = "ÿ����1Сʱ����20������ͬʱ��ȡ1����Դ��δ������1Сʱ�������κ���Դ��ÿ��Ϣ1Сʱ�ظ�10��������ǰ�����п��Ժ��չ���̸��������Դ��ý��,��ԴΪ0����Ҫȥ���С�����̸�нϻ�ʱ�䣬����ѡ������Դ������������Ҳ����ѡ�����һ�����������ⵢ��������������Ը������";// ������ȷ��ʾ״ֵ̬�Ŀ۳�����

        string statusInfo = $@"- ����ֵ��{npc.energy:F0}/{npc.maxEnergy:F0}
- ��Ǯ��{npc.tips}���";

        string prompt = $@"����ҪΪ{npc.npcName}�ƶ�������ճ̰��š�

��ɫ��Ϣ��
- ������{npc.npcName}
- ְҵ��{npc.occupation}
- �Ը�{npc.personality}
{statusInfo}
- ��ǰʱ�䣺����8:00

�����н������µص㣨�������ѡ�񣩣�
- home���ң���Ϣ������˯����
- lumberyard����ľ������ľ�������ص㣩
- market�����У����ף��̶�����2Сʱ��
��ǰ�Ĺ����ǣ�{statusRule}
������һ����8:00��22:00���ճ̱���ʽ���£�
{{
  ""activities"": [
    {{
      ""startTime"": ""08:00"",
      ""duration"": 30,
      ""activity"": ""����"",
      ""location"": ""lumberyard"",
      ""description"": ""�ڷ�ľ������"",                                                                     
      ""priority"": 8
    }},
    // ����...
  ],
  ""reasoning"": ""���ǵ���ɫ��ְҵ�͵�ǰ״̬...""
}}

ע�����
1. location�ֶα���������3���ص�֮һ
2. ʱ��Ҫ�����������п�϶
3. ���һ���Ӧ���ڼ���Ϣ
4. �ճ̱����8:00��ʼ����22:00��������Ҫ���ַ���Сʱ�г�

��Ҫ�����뷵����Ч��JSON��ʽ��ȷ����
- ʹ��Ӣ�Ķ��ź�ð��
- �ַ���ʹ��˫����
- �������һ��Ԫ�غ�Ҫ�ж���
- ��Ҫ��JSONǰ������κ�˵������

ֻ����JSON���ݣ�";
        //��Ҫ�޸�
        /* References:As {character}, engage in a dialogue with the
        objective of {objective}. Respond to the
        conversation using the given context or memories
        and limit your response to under 50 words. Please
        submit your response in JSON format.
        YOU are: {character}
        {event description}
        Initiates a conversation with {user��s
        character}.
        Here is your memory : {memory}
        Give your response in format {��response
        ��: ��here is the response��}.....*/ 
        return prompt;
    }


    
    private AIGeneratedSchedule ParseScheduleResponse(string response, string npcName)
    {
        // ������Ӧ����ȡJSON����
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

        Debug.Log($"[AISchedule] �ճ��ѱ��浽: {filePath}");
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
        // ����Ĭ���ճ���Ϊ����
        return new List<DailyActivity>
        {
            new DailyActivity(8, 0, 30, "�����", Vector3.zero),
            new DailyActivity(8, 30, 240, "����", Vector3.zero),
            new DailyActivity(12, 30, 30, "�����", Vector3.zero),
            new DailyActivity(13, 0, 240, "����", Vector3.zero),
            new DailyActivity(17, 0, 30, "�����", Vector3.zero),
            new DailyActivity(17, 30, 270, "��Ϣ", Vector3.zero)
        };
    }
}