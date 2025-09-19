using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static GameModeManager;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    [Header("ʱ������")]
    public int startHour = 8;   // ��ʼʱ��(Сʱ)
    public int startMinute = 0; // ��ʼʱ��(����)
    [Tooltip("1��ʵ�� = ������Ϸ����")]
    public float timeScale = 0.5f; // Ĭ��: 1��ʵ�� = 2��Ϸ����

    [Header("�ٶ�����")]
    [Tooltip("ʱ������Ϊ1ʱ�Ļ����ƶ��ٶ�")]
    public float baseMoveSpeed = 8f; // timeScale=1ʱ�Ļ����ٶ�

    // ʱ�����Ÿı��¼�
    public UnityEvent<float> OnTimeScaleChanged;

    private float _totalMinutes;
    private int _currentHour;
    private int _currentMinute;
    private float _lastUpdateTime;
    private const float UPDATE_INTERVAL = 0.2f;
    private float _previousTimeScale;

    public int CurrentHour => _currentHour;
    public int CurrentMinute => _currentMinute;

    // ��ȡ��ǰӦ��ʹ�õ��ƶ��ٶ�

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

    [Header("UI����")]
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

        // ��ʼ��ʱ����һ���¼�
        //SetCustomTimeScale(0f);
        //timeScale = 0f; //����һ��ʼ������������npc������ճ̺��ƶ�
        OnTimeScaleChanged?.Invoke(timeScale);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateTimeDisplay();
        // �������غ�����֪ͨ���ж�������ٶ�
        OnTimeScaleChanged?.Invoke(timeScale);
    }

    void Update()
    {
        // ���ʱ�������Ƿ�ı�
        if (Mathf.Abs(timeScale - _previousTimeScale) > 0.01f)
        {
            _previousTimeScale = timeScale;
            OnTimeScaleChanged?.Invoke(timeScale);
        }

        // ����ʱ��
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

    // ����Ϸ����ת��Ϊ��ʵ����
    public float GameMinutesToRealSeconds(float gameMinutes)
    {
        return gameMinutes  / timeScale;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    // ����Զ���ʱ�����ŵķ���
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


    // ��ȡ��ǰʱ������
    public float GetCurrentTimeScale()
    {
        return timeScale;
    }

    public void NextDayResetTime()
    {

        // ���㵱ǰ����
        int currentDay = GetDayCount();
        
        // ������һ�����ʼ�ܷ�����
        float nextDayStartMinutes = (currentDay + 1) * 24 * 60;

        // ����Ŀ��ʱ�䣨Сʱ�ͷ��ӣ�
        _totalMinutes = nextDayStartMinutes + startHour * 60 + startMinute;
        timeScale = 1f;
        // ����ʱ����ʾ
        CalculateTime();
        UpdateTimeDisplay();

        Debug.Log($"�������� {currentDay + 1} ��� {startHour:00}:{startMinute:00}");
        _previousTimeScale = 1f;

        // ����ʱ�����ű仯�¼�
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
        // ʱ����������Ը���״̬����ʱ������
        switch (newState)
        {
            case GameState.Operating:
                // ȷ��ʱ����������
                if (GetCurrentTimeScale() == 0) ResumeTime();
                break;

            case GameState.Paused:
                // ��ͣʱ��
                PauseTime();
                break;
        }
    }
}