using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static GameModeManager;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    [Header("时间设置")]
    public int startHour = 8;   // 起始时间(小时)
    public int startMinute = 0; // 起始时间(分钟)
    [Tooltip("1现实秒 = 多少游戏分钟")]
    public float timeScale = 0.5f; // 默认: 1现实秒 = 2游戏分钟

    [Header("速度设置")]
    [Tooltip("时间缩放为1时的基础移动速度")]
    public float baseMoveSpeed = 8f; // timeScale=1时的基础速度

    // 时间缩放改变事件
    public UnityEvent<float> OnTimeScaleChanged;

    private float _totalMinutes;
    private int _currentHour;
    private int _currentMinute;
    private float _lastUpdateTime;
    private const float UPDATE_INTERVAL = 0.2f;
    private float _previousTimeScale;

    public int CurrentHour => _currentHour;
    public int CurrentMinute => _currentMinute;

    // 获取当前应该使用的移动速度

    public int GetTotalMinutes()
    {
        return Mathf.FloorToInt(_totalMinutes);
    }
    public int GetDayMinutes()
    {
        return Mathf.FloorToInt(_totalMinutes %(24*60));
    }
    public int GetDayCount()
    {
        return Mathf.FloorToInt(_totalMinutes / (24 * 60));
    }

    public void GetFullTimeInfo(out int day, out int hour, out int minute)
    {
        day = GetDayCount();
        hour = CurrentHour;
        minute = CurrentMinute;
    }

    public float GetScaledMoveSpeed()
    {
        return baseMoveSpeed * timeScale;
    }

    [Header("UI引用")]
    public TMP_Text timeText;
    public TMP_Text dayText;
    public GameObject timePanel;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        _totalMinutes = startHour * 60 + startMinute;
        _previousTimeScale = timeScale;
        CalculateTime();
        UpdateTimeDisplay();

        if (timePanel != null) timePanel.SetActive(true);

        // 初始化时触发一次事件
        //SetCustomTimeScale(0f);
        //timeScale = 0f; //可以一开始不动，当所有npc都完成日程后移动
        OnTimeScaleChanged?.Invoke(timeScale);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateTimeDisplay();
        // 场景加载后重新通知所有对象更新速度
        OnTimeScaleChanged?.Invoke(timeScale);
    }

    void Update()
    {
        // 检查时间缩放是否改变
        if (Mathf.Abs(timeScale - _previousTimeScale) > 0.01f)
        {
            _previousTimeScale = timeScale;
            OnTimeScaleChanged?.Invoke(timeScale);
        }

        // 更新时间
        _totalMinutes += Time.deltaTime * timeScale;

        if (Time.time - _lastUpdateTime > UPDATE_INTERVAL)
        {
            CalculateTime();
            UpdateTimeDisplay();
            _lastUpdateTime = Time.time;
        }

    }

    private void CalculateTime()
    {
        _currentHour = Mathf.FloorToInt(_totalMinutes / 60) % 24;
        _currentMinute = Mathf.FloorToInt(_totalMinutes % 60);
    }

    private void UpdateTimeDisplay()
    {
        if (timeText != null)
        {
            timeText.text = $"{_currentHour:00}:{_currentMinute:00}";
        }

        if (dayText != null)
        {
            dayText.text = $"{GetDayCount()}";
        }
    }

    public string GetCurrentTime()
    {
        return $"{_currentHour:00}:{_currentMinute:00}";
    }

    // 将游戏分钟转换为现实秒数
    public float GameMinutesToRealSeconds(float gameMinutes)
    {
        return gameMinutes  / timeScale;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    // 添加自定义时间缩放的方法
    public void SetCustomTimeScale(float scale)
    {
        _previousTimeScale = timeScale;
        timeScale = scale;
        //Debug.Log($"timeScale:{timeScale}");
        //Debug.Log($"previoustimeScale:{_previousTimeScale}");
        OnTimeScaleChanged?.Invoke(timeScale);
    }

    public void ResumeNormalTime()
    {
        timeScale = _previousTimeScale;
        OnTimeScaleChanged?.Invoke(timeScale);
        //Debug.Log($"timeScale:{timeScale}");
    }


    // 获取当前时间缩放
    public float GetCurrentTimeScale()
    {
        return timeScale;
    }

    public void NextDayResetTime()
    {

        // 计算当前天数
        int currentDay = GetDayCount();
        
        // 计算下一天的起始总分钟数
        float nextDayStartMinutes = (currentDay + 1) * 24 * 60;

        // 加上目标时间（小时和分钟）
        _totalMinutes = nextDayStartMinutes + startHour * 60 + startMinute;
        timeScale = 1f;
        // 更新时间显示
        CalculateTime();
        UpdateTimeDisplay();

        Debug.Log($"已跳到第 {currentDay + 1} 天的 {startHour:00}:{startMinute:00}");
        _previousTimeScale = 1f;

        // 触发时间缩放变化事件
        OnTimeScaleChanged?.Invoke(timeScale);
    }




    public void PauseTime()
    {
        SetCustomTimeScale(0f);
    }

    public void ResumeTime()
    {
        SetCustomTimeScale(_previousTimeScale);
    }

    public void SetTimeScale(float scale, bool rememberPrevious = true)
    {
        if (rememberPrevious) _previousTimeScale = timeScale;
        SetCustomTimeScale(scale);
    }
    public void OnGameStateChanged(GameState previousState, GameState newState)
    {
        // 时间管理器可以根据状态调整时间流逝
        switch (newState)
        {
            case GameState.Operating:
                // 确保时间正常流动
                if (GetCurrentTimeScale() == 0) ResumeTime();
                break;

            case GameState.Paused:
                // 暂停时间
                PauseTime();
                break;
        }
    }
}