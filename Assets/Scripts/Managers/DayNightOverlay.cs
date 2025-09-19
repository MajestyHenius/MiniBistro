
using UnityEngine;
using UnityEngine.UI;

public class DayNightOverlay : MonoBehaviour
{
    [Header("�������")]
    public TimeManager timeManager;  // ��Ϊ�������TimeManager
    public Image overlayImage;

    [Header("��ɫ����")]
    public Color nightColor = new Color(0.1f, 0.1f, 0.3f, 0.7f);     // ҹ����ɫ
    public Color dawnColor = new Color(1f, 0.6f, 0.4f, 0.3f);        // ������ɫ
    public Color dayColor = new Color(1f, 1f, 1f, 0f);               // ������ɫ��͸����
    public Color duskColor = new Color(1f, 0.5f, 0.3f, 0.4f);        // �ƻ���ɫ

    void Start()
    {
        // ���û���ֶ�ָ�����Զ�����
        if (timeManager == null)
        {
            timeManager = TimeManager.Instance;

            if (timeManager == null)
            {
                timeManager = FindObjectOfType<TimeManager>();
            }

            if (timeManager == null)
            {
                Debug.LogError("�Ҳ��� TimeManager����ȷ����������TimeManager�����");
                return;
            }
            else
            {
                Debug.Log("�Զ��ҵ��� TimeManager");
            }
        }

        // �������ǲ�
        if (overlayImage == null)
        {
            CreateOverlay();
        }
    }

    void CreateOverlay()
    {
        // ����Canvas
        GameObject canvasObj = new GameObject("DayNightCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;  // ȷ�������ϲ�

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // ��������ͼ��
        GameObject imageObj = new GameObject("Overlay");
        imageObj.transform.SetParent(canvasObj.transform, false);

        overlayImage = imageObj.AddComponent<Image>();
        overlayImage.color = dayColor;

        // ����Ϊȫ��
        RectTransform rect = imageObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // ȷ�����赲���
        overlayImage.raycastTarget = false;
    }

    void Update()
    {
        if (timeManager == null || overlayImage == null) return;

        // ֱ�ӷ��ʹ�������
        Color currentColor = GetColorForTime(timeManager.CurrentHour, timeManager.CurrentMinute);
        overlayImage.color = currentColor;
    }
    // ͨ����������޸�TimeManager����ȡ˽���ֶ�
    int GetHour()
    {
        // ��TimeManager��ʱ���ַ����н���
        string timeStr = timeManager.GetCurrentTime();
        string[] parts = timeStr.Split(':');
        return int.Parse(parts[0]);
    }

    int GetMinute()
    {
        // ��TimeManager��ʱ���ַ����н���
        string timeStr = timeManager.GetCurrentTime();
        string[] parts = timeStr.Split(':');
        return int.Parse(parts[1]);
    }

    Color GetColorForTime(int hour, int minute)
    {
        float time = hour + minute / 60f;

        if (time >= 22f || time < 4f)  // 22:00 - 04:00 ҹ��
        {
            return nightColor;
        }
        else if (time >= 4f && time < 7f)  // 04:00 - 07:00 ����
        {
            float t = (time - 4f) / 3f;
            return Color.Lerp(nightColor, dawnColor, t);
        }
        else if (time >= 7f && time < 10f)  // 07:00 - 10:00 �糿
        {
            float t = (time - 7f) / 3f;
            return Color.Lerp(dawnColor, dayColor, t);
        }
        else if (time >= 10f && time < 17f)  // 10:00 - 17:00 ����
        {
            return dayColor;
        }
        else if (time >= 17f && time < 20f)  // 17:00 - 20:00 �ƻ�
        {
            float t = (time - 17f) / 3f;
            return Color.Lerp(dayColor, duskColor, t);
        }
        else  // 20:00 - 22:00 ��ҹ
        {
            float t = (time - 20f) / 2f;
            return Color.Lerp(duskColor, nightColor, t);
        }
    }
}