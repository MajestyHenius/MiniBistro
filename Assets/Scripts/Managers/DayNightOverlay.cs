
using UnityEngine;
using UnityEngine.UI;

public class DayNightOverlay : MonoBehaviour
{
    [Header("组件引用")]
    public TimeManager timeManager;  // 改为引用你的TimeManager
    public Image overlayImage;

    [Header("颜色设置")]
    public Color nightColor = new Color(0.1f, 0.1f, 0.3f, 0.7f);     // 夜晚颜色
    public Color dawnColor = new Color(1f, 0.6f, 0.4f, 0.3f);        // 黎明颜色
    public Color dayColor = new Color(1f, 1f, 1f, 0f);               // 白天颜色（透明）
    public Color duskColor = new Color(1f, 0.5f, 0.3f, 0.4f);        // 黄昏颜色

    void Start()
    {
        // 如果没有手动指定，自动查找
        if (timeManager == null)
        {
            timeManager = TimeManager.Instance;

            if (timeManager == null)
            {
                timeManager = FindObjectOfType<TimeManager>();
            }

            if (timeManager == null)
            {
                Debug.LogError("找不到 TimeManager！请确保场景中有TimeManager组件。");
                return;
            }
            else
            {
                Debug.Log("自动找到了 TimeManager");
            }
        }

        // 创建覆盖层
        if (overlayImage == null)
        {
            CreateOverlay();
        }
    }

    void CreateOverlay()
    {
        // 创建Canvas
        GameObject canvasObj = new GameObject("DayNightCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;  // 确保在最上层

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // 创建覆盖图像
        GameObject imageObj = new GameObject("Overlay");
        imageObj.transform.SetParent(canvasObj.transform, false);

        overlayImage = imageObj.AddComponent<Image>();
        overlayImage.color = dayColor;

        // 设置为全屏
        RectTransform rect = imageObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 确保不阻挡点击
        overlayImage.raycastTarget = false;
    }

    void Update()
    {
        if (timeManager == null || overlayImage == null) return;

        // 直接访问公共属性
        Color currentColor = GetColorForTime(timeManager.CurrentHour, timeManager.CurrentMinute);
        overlayImage.color = currentColor;
    }
    // 通过反射或者修改TimeManager来获取私有字段
    int GetHour()
    {
        // 从TimeManager的时间字符串中解析
        string timeStr = timeManager.GetCurrentTime();
        string[] parts = timeStr.Split(':');
        return int.Parse(parts[0]);
    }

    int GetMinute()
    {
        // 从TimeManager的时间字符串中解析
        string timeStr = timeManager.GetCurrentTime();
        string[] parts = timeStr.Split(':');
        return int.Parse(parts[1]);
    }

    Color GetColorForTime(int hour, int minute)
    {
        float time = hour + minute / 60f;

        if (time >= 22f || time < 4f)  // 22:00 - 04:00 夜晚
        {
            return nightColor;
        }
        else if (time >= 4f && time < 7f)  // 04:00 - 07:00 黎明
        {
            float t = (time - 4f) / 3f;
            return Color.Lerp(nightColor, dawnColor, t);
        }
        else if (time >= 7f && time < 10f)  // 07:00 - 10:00 早晨
        {
            float t = (time - 7f) / 3f;
            return Color.Lerp(dawnColor, dayColor, t);
        }
        else if (time >= 10f && time < 17f)  // 10:00 - 17:00 白天
        {
            return dayColor;
        }
        else if (time >= 17f && time < 20f)  // 17:00 - 20:00 黄昏
        {
            float t = (time - 17f) / 3f;
            return Color.Lerp(dayColor, duskColor, t);
        }
        else  // 20:00 - 22:00 入夜
        {
            float t = (time - 20f) / 2f;
            return Color.Lerp(duskColor, nightColor, t);
        }
    }
}