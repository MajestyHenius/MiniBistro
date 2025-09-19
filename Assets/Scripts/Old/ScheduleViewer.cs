// ScheduleViewer.cs
using UnityEngine;
using System.IO;

public class ScheduleViewer : MonoBehaviour
{
    [Header("日程显示")]
    [TextArea(10, 30)]
    public string currentScheduleJson;

    //private NPCStatus npcStatus;
    private NPCBehavior npcBehavior;

    void Start()
    {
        //npcStatus = GetComponent<NPCStatus>();
        npcBehavior = GetComponent<NPCBehavior>();
    }

    [ContextMenu("查看今日日程")]
    public void ViewTodaySchedule()
    {
        if (npcBehavior == null) return;

        string folderPath = Path.Combine(Application.persistentDataPath, "NPCSchedules");
        string fileName = $"{npcBehavior.npcName}_{System.DateTime.Now:yyyy-MM-dd}.json";
        string filePath = Path.Combine(folderPath, fileName);

        if (File.Exists(filePath))
        {
            currentScheduleJson = File.ReadAllText(filePath);
            Debug.Log($"[ScheduleViewer] 加载日程: {filePath}");
        }
        else
        {
            currentScheduleJson = "今日日程尚未生成";
            Debug.LogWarning($"[ScheduleViewer] 未找到日程文件: {filePath}");
        }
    }

    [ContextMenu("查看当前状态")]
    public void ViewCurrentStatus()
    {
        /*if (npcStatus != null)
        {
            Debug.Log($"[{gameObject.name}] 当前状态: {npcStatus.GetStatusSummary()}");
        }*/
    }
}