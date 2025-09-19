// ScheduleViewer.cs
using UnityEngine;
using System.IO;

public class ScheduleViewer : MonoBehaviour
{
    [Header("�ճ���ʾ")]
    [TextArea(10, 30)]
    public string currentScheduleJson;

    //private NPCStatus npcStatus;
    private NPCBehavior npcBehavior;

    void Start()
    {
        //npcStatus = GetComponent<NPCStatus>();
        npcBehavior = GetComponent<NPCBehavior>();
    }

    [ContextMenu("�鿴�����ճ�")]
    public void ViewTodaySchedule()
    {
        if (npcBehavior == null) return;

        string folderPath = Path.Combine(Application.persistentDataPath, "NPCSchedules");
        string fileName = $"{npcBehavior.npcName}_{System.DateTime.Now:yyyy-MM-dd}.json";
        string filePath = Path.Combine(folderPath, fileName);

        if (File.Exists(filePath))
        {
            currentScheduleJson = File.ReadAllText(filePath);
            Debug.Log($"[ScheduleViewer] �����ճ�: {filePath}");
        }
        else
        {
            currentScheduleJson = "�����ճ���δ����";
            Debug.LogWarning($"[ScheduleViewer] δ�ҵ��ճ��ļ�: {filePath}");
        }
    }

    [ContextMenu("�鿴��ǰ״̬")]
    public void ViewCurrentStatus()
    {
        /*if (npcStatus != null)
        {
            Debug.Log($"[{gameObject.name}] ��ǰ״̬: {npcStatus.GetStatusSummary()}");
        }*/
    }
}