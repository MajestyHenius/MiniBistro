using Cinemachine;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameModeManager : MonoBehaviour
{
    [Header("游戏模式设置")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera; // 改为虚拟相机
    [SerializeField] private CinemachineBrain cinemachineBrain; // 可选：用于获取Brain组件
    [SerializeField] GameObject playerObject;
    [SerializeField] Transform player;
    private PlayerController playerController;
    [SerializeField] private TimeManager timeManager;
    private int hasTriggeredNegotiation = 0; // 已经发生的谈判次数
    [SerializeField] public int negotiationGap = 2; //每x天发生一次进货讲价
    [Header("摄像机设置")]
    [SerializeField] private float normalCameraSize = 5f;
    [SerializeField] private float overviewCameraSize = 15f;
    [SerializeField] private float cameraTransitionDuration = 0.5f;
    [SerializeField] private Vector3 normalCameraOffset = new Vector3(0, 0, -10);
    [SerializeField] private Vector3 overviewCameraOffset = new Vector3(0, 5, -20);

    // 添加Cinemachine相关设置
    [Header("Cinemachine设置")]
    [SerializeField] private bool useOrthographic = true; // 是否使用正交投影
    [SerializeField] private float normalFieldOfView = 60f; // 透视投影时的正常FOV
    [SerializeField] private float overviewFieldOfView = 90f; // 透视投影时的全景FOV

    [Header("UI控制面板")]
    [SerializeField] private GameObject controlPanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button normalSpeedButton;
    [SerializeField] private Button fastSpeedButton;
    [SerializeField] private Button veryFastSpeedButton;
    [SerializeField] private TMP_Text currentSpeedText;
    [SerializeField] private Slider speedSlider;

    [SerializeField] private GameObject freeActivityUI; // "自由活动"的UI，切换阶段
    [SerializeField] private TMP_Text freeActivityText; // 显示文字的Text组件
    [SerializeField] private float uiDisplayDuration = 2f; // UI显示持续时间
    [SerializeField] private float uiFadeInDuration = 0.5f; // 淡入时间
    [SerializeField] private float uiFadeOutDuration = 0.5f; // 淡出时间
    [SerializeField] private DialogueUIManager dialogutUI; // 谈判对话相关的UI
    [Header("NPC管理")]
    [SerializeField] private List<NPCBehavior> allNPCs = new List<NPCBehavior>();
    [SerializeField] private GameObject loadingUI; // 显示加载中的UI
    [SerializeField] private TMP_Text loadingText; // 加载进度文字
    [SerializeField] private GameObject waiterSelectionUI; // 添加服务员选择UI引用

    



    private int totalNPCs = 0;
    private int readyNPCs = 0;
    private bool gameStarted = false;

    [Header("时间控制设置")]
    [SerializeField] private float normalTimeScale = 2f;
    [SerializeField] private float fastTimeScale = 10f;
    [SerializeField] private float veryFastTimeScale = 60f;

    [Header("菜单显示设置")]
    [SerializeField] private GameObject menuDisplayPanel; // 菜单显示面板
    [SerializeField] private GameObject menuItemPrefab; // 菜单项预制体
    [SerializeField] private Transform menuContentParent; // 菜单项父对象
    [SerializeField] private ScrollRect menuScrollView; // 滚动视图
    [SerializeField] private TMP_Text menuTitleText; // 菜单标题文本
    [SerializeField] private Button toggleMenuButton; // 切换菜单显示/隐藏的按钮
    [SerializeField] private TMP_Text toggleMenuButtonText; // 按钮文本组件
    private bool isMenuDisplayed = false; // 跟踪菜单显示状态
    // 重新管理游戏状态
    public enum GameState
    {
        Initializing,
        SelectingWaiters,
        DayStart,
        Operating,
        DayEnd,
        Paused,
        OverviewMode
    }
    private GameState _currentState = GameState.Initializing;

    public void SetGameState(GameState newState)
    {
        // 如果状态没有变化，直接返回
        if (_currentState == newState) return;

        GameState previousState = _currentState;
        _currentState = newState;

        Debug.Log($"游戏状态变化: {previousState} -> {newState}");

        switch (newState)
        {
            case GameState.Initializing:
                // 初始化状态：暂停时间，等待NPC准备
                TimeManager.Instance.PauseTime();
                if (loadingUI != null) loadingUI.SetActive(true);
                StartCoroutine(WaitForAllNPCsReady());
                break;

            case GameState.SelectingWaiters:
                // 服务员选择状态：显示选择UI
                if (waiterSelectionUI != null) waiterSelectionUI.SetActive(true);
                break;

            case GameState.DayStart:
                // 日开始状态：显示开始营业UI，恢复时间
                StartCoroutine(ShowFreeActivityUI("开始营业!"));
                TimeManager.Instance.SetTimeScale(normalTimeScale);
                if (waiterSelectionUI != null) waiterSelectionUI.SetActive(false);
                break;

            case GameState.Operating:
                // 正常营业状态：确保时间正常流动
                if (TimeManager.Instance.GetCurrentTimeScale() == 0)
                {
                    TimeManager.Instance.ResumeTime();
                }
                break;

            case GameState.DayEnd:
                // 日结束状态：暂停时间，显示营业结束UI，展示评价
                TimeManager.Instance.PauseTime();
                Debug.Log("通过EndBusinessSequence结束");
                StartCoroutine(EndBusinessSequence());
                break;

            case GameState.Paused:
                // 暂停状态：暂停时间，显示控制面板（如果适用）
                TimeManager.Instance.PauseTime();
                if (controlPanel != null && isOverviewMode) controlPanel.SetActive(true);
                break;

            case GameState.OverviewMode:
                // 俯瞰模式：可能已经通过ToggleGameMode处理，这里确保状态正确
                if (!isOverviewMode) ToggleGameMode();
                break;
        }

        // 触发状态变化事件，通知其他管理器
        OnGameStateChanged?.Invoke(previousState, newState);
    }


    public delegate void GameStateChangeHandler(GameState previousState, GameState newState);
    public static event GameStateChangeHandler OnGameStateChanged;

    private void HandleGameStateChange(GameState previousState, GameState newState)
    {
        // 通知其他管理器状态变化
        if (RestaurantManager.Instance != null)
        {
            RestaurantManager.Instance.OnGameStateChanged(previousState, newState);
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnGameStateChanged(previousState, newState);
        }

        // 可以根据状态变化执行特定逻辑
        switch (newState)
        {
            case GameState.DayStart:
                // 新的一天开始，重置餐厅状态
                RestaurantManager.Instance.ResetDailyReviews();
                RestaurantManager.Instance.ResetCustomerQueue();
                break;

            case GameState.DayEnd:
                // 一天结束，清理顾客
                RestaurantManager.ClearAllCustomers();
                break;
        }
    }
    private bool isOverviewMode = false;
    private Coroutine cameraTransitionCoroutine;
    private float previousTimeScale;

    // 保存原始的虚拟相机设置
    private Transform originalFollow;
    private Transform originalLookAt;
    private Vector3 originalBodyOffset;

    public static GameModeManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        // 注册状态变化监听器
        OnGameStateChanged += HandleGameStateChange;


    }

    void Start()
    {


        if (allNPCs.Count == 0)
        {
            allNPCs = FindObjectsOfType<NPCBehavior>().ToList();
        }

        totalNPCs = allNPCs.Count;

        // 隐藏UI
        if (freeActivityUI != null)
            freeActivityUI.SetActive(false);
        if (loadingUI != null)
            loadingUI.SetActive(false);
        StartCoroutine(InitializeGame());
        //dialogutUI.HideImmediate();
        // 自动获取虚拟相机
        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            virtualCamera.Follow = originalFollow;
            virtualCamera.LookAt = originalLookAt;
            //视角赋初值
            cameraTransitionCoroutine = StartCoroutine(TransitionVirtualCamera(
                useOrthographic ? normalCameraSize : normalFieldOfView,
                normalCameraOffset, 
                false
            ));
            if (virtualCamera == null)
            {
                Debug.LogError("未找到CinemachineVirtualCamera！请在Inspector中指定或在场景中添加一个。");
                return;
            }
        }
        // 获取CinemachineBrain（可选）
        if (cinemachineBrain == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cinemachineBrain = mainCam.GetComponent<CinemachineBrain>();
            }
        }

        if (playerObject != null)
        {
            playerController = playerObject.GetComponent<PlayerController>();
            player = playerObject.transform;
        }
        else
        {
            playerController = FindObjectOfType<PlayerController>();
            if (playerController != null)
            {
                playerObject = playerController.gameObject;
                player = playerObject.transform;
            }
        }

        // 保存原始的虚拟相机设置
        if (virtualCamera != null)
        {
            originalFollow = virtualCamera.Follow;
            originalLookAt = virtualCamera.LookAt;

            // 获取Transposer组件的偏移值
            var transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                originalBodyOffset = transposer.m_FollowOffset;
            }
        }

        // 获取TimeManager引用
        if (timeManager == null)
            timeManager = TimeManager.Instance;

        // 初始化UI
        InitializeUI();

        // 确保控制面板初始状态为隐藏
        if (controlPanel != null)
            controlPanel.SetActive(false);
    }

    void Update()
    {
        if (!gameStarted || timeManager == null)
            return;
        int day = timeManager.GetDayCount();
        int hour = timeManager.CurrentHour;
        int minute = timeManager.CurrentMinute;
        // 读取时间用于场景切换
        if (hour == 23 &&minute ==0)
        {
            Debug.Log("到了打烊时间");
            string freeActivityTXT = "今日营业结束!";
            
            ShowFreeActivityUI(freeActivityTXT);
            //TriggerDialogueScene();
        }

        // 检测ESC键
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleGameMode();
        }

        // 全景模式下的快捷键
        if (isOverviewMode)
        {
            // 空格键暂停/继续
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }

            // 数字键快速切换速度
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SetNormalSpeed();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SetFastSpeed();
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetVeryFastSpeed();
            }
        }
    }

    private void InitializeUI()
    {
        // 绑定按钮事件
        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);

        if (normalSpeedButton != null)
            normalSpeedButton.onClick.AddListener(SetNormalSpeed);

        if (fastSpeedButton != null)
            fastSpeedButton.onClick.AddListener(SetFastSpeed);

        if (veryFastSpeedButton != null)
            veryFastSpeedButton.onClick.AddListener(SetVeryFastSpeed);

        // 初始化滑块
        if (speedSlider != null)
        {
            speedSlider.minValue = 0;
            speedSlider.maxValue = veryFastTimeScale;
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }


        // 菜单显示按钮
        if (toggleMenuButton != null)
        {
            toggleMenuButton.onClick.AddListener(ToggleMenuDisplay);
        }

        // 确保初始状态正确
        if (toggleMenuButton != null)
        {
            toggleMenuButton.gameObject.SetActive(false); // 默认隐藏
        }

        if (menuDisplayPanel != null)
        {
            menuDisplayPanel.SetActive(false); // 默认隐藏
            isMenuDisplayed = false;
        }



    }

    #region NPC日程等待+游戏阶段提示
    private IEnumerator InitializeGame()
    {
        isBusinessEnded = false;

        // 确保时间完全暂停
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
            Debug.Log("游戏时间已暂停，等待所有NPC生成日程表...");
        }

        // 显示加载UI（可选）
        if (loadingUI != null)
        {
            loadingUI.SetActive(true);
            UpdateLoadingUI();
        }

        // ===== 服务员选择流程 =====
        // 显示服务员选择UI
        if (waiterSelectionUI != null)
        {
            waiterSelectionUI.SetActive(true);

            // 获取选择脚本并确保时间保持暂停
            WaiterSelectionUI selectionScript = waiterSelectionUI.GetComponent<WaiterSelectionUI>();
            if (selectionScript != null)
            {
                // 确保选择UI知道时间应该暂停
                selectionScript.EnsureTimePaused();

                // 等待选择完成
                yield return new WaitUntil(() => !waiterSelectionUI.activeSelf);
            }
            else
            {
                // 如果没有找到脚本，直接等待UI关闭
                yield return new WaitUntil(() => !waiterSelectionUI.activeSelf);
            }
        }
        // ===== 服务员选择流程结束 =====

        // 再次确认时间暂停，防止在等待期间被修改
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
        }

        // 等待所有NPC准备完成
        yield return StartCoroutine(WaitForAllNPCsReady());

        // 隐藏加载UI
        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        // 显示"自由活动"UI
        string freeActivityTXT = "开始营业!";
        yield return StartCoroutine(ShowFreeActivityUI(freeActivityTXT));

        // 恢复时间
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(normalTimeScale);
            Debug.Log("所有NPC准备完成，游戏开始！");
        }

        gameStarted = true;
        StartCoroutine(CheckCustomerStatusRoutine());
        yield return null;
    }
    private IEnumerator EndBusinessSequence()
    {
        // 强制退出ESC模式
        ExitOverviewMode();
        UpdateSpeedDisplay();
        // 显示"营业结束"UI
        yield return StartCoroutine(ShowFreeActivityUI("营业结束!"));

        // 展示所有评价
        yield return StartCoroutine(RestaurantManager.Instance.ShowAllReviews());

        // 重置餐厅状态
        RestaurantManager.Instance.ResetDay();
        // 进入下一天
        TimeManager.Instance.NextDayResetTime();

        // 重新初始化游戏
        StartCoroutine(InitializeGame());
    }
    private IEnumerator CheckCustomerStatusRoutine()
    {
        while (true)
        {
            // 如果已经是营业结束状态，不再检查
            if (isBusinessEnded)
            {
                yield break;
            }

            // 检查时间是否超过22点且所有顾客都已离开
            if (timeManager != null &&
                (timeManager.CurrentHour >= 22 || AreAllCustomersGone()))
            {
                // 添加额外检查，确保不是在营业结束过程中
                if (!isBusinessEnded)
                {
                    Debug.Log("通过EndBusiness结束");
                    StartCoroutine(EndBusiness());
                    yield break; // 结束这个协程
                }
            }

            yield return new WaitForSeconds(0.5f); // 每0.5秒检查一次
        }
    }
    //营业结束：
    public bool isBusinessEnded = false;
    [SerializeField] private GameObject businessEndUI; // 类似于开始经营的UI

    // 检查是否所有顾客都已离开的方法
    private bool AreAllCustomersGone()
    {
        return RestaurantManager.ActiveCustomerCount == 0 &&
               RestaurantManager.QueueCustomerCount == 0;
    }

    // 结束营业的协程方法
    private IEnumerator EndBusiness()
    {
        // 确保只执行一次
        if (isBusinessEnded) yield break;
        isBusinessEnded = true;

        Debug.Log("开始结束营业流程");

        // 暂停时间
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
            Debug.Log("营业结束，游戏时间已暂停");
        }

        // 等待一小段时间确保所有操作完成
        yield return new WaitForSeconds(0.1f);

        // 应该先注销所有顾客
        RestaurantManager.ClearAllCustomers();

        // 显示"营业结束"UI
        string endBusinessTXT = "营业结束!";
        yield return StartCoroutine(ShowFreeActivityUI(endBusinessTXT));

        // 等待UI动画完成
        yield return new WaitForSeconds(1f);
        if (allNPCs.Count == 0)
        {
            Debug.LogWarning("服务员已被清空");
            allNPCs = FindObjectsOfType<NPCBehavior>().ToList();
        }
           
        // 保存所有NPC的记忆
        foreach (var npc in allNPCs)
        {
            if (npc != null)
            {
                npc.TriggerDailyReflection();// 将今天进行反思
                //npc.SaveMemory(); // 将全部记忆保存到json
            }
            else
                Debug.LogWarning("NPC是空的");
        }
        // 显示所有评价
        yield return StartCoroutine(RestaurantManager.Instance.ShowAllReviews());

        // 等待评价显示完成
        while (RestaurantManager.Instance.IsShowingReviews)
        {
            yield return null;
        }
        
        if (timeManager != null)
        {
            // 将日期加1
            timeManager.NextDayResetTime();
            // 重置时间为开始营业的时间
            Debug.Log($"已进入第{timeManager.GetDayCount()}天，时间重置为开始时间");
        }

        // 这里可以添加其他结束营业的逻辑，如统计当日收入等
        Debug.Log("当日营业已结束，所有顾客已离开");

        // 重置状态并开始新的一天
        isBusinessEnded = false;
        StartCoroutine(InitializeGame());
    }

    // 检查顾客状态的协程


    private IEnumerator WaitForAllNPCsReady()
    {
        readyNPCs = 0;

        // 检查每个NPC的准备状态
        while (readyNPCs < totalNPCs)
        {
            readyNPCs = 0;

            foreach (var npc in allNPCs)
            {
                if (npc != null && npc.IsScheduleReady())
                {
                    readyNPCs++;
                }
            }

            // 更新加载进度
            UpdateLoadingUI();

            // 每0.5秒检查一次
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"NPC准备进度: {readyNPCs}/{totalNPCs}");
            //string WatingTXT = "Awaiting AI Schedules...";
            //yield return StartCoroutine(ShowFreeActivityUI(WatingTXT));
        }

        //Debug.Log("所有NPC日程表生成完成！");
    }

    private void UpdateLoadingUI()
    {
        if (loadingText != null)
        {
            loadingText.text = $"正在初始化NPC日程表... ({readyNPCs}/{totalNPCs})";
        }
    }

    private IEnumerator ShowFreeActivityUI(string showTXT)
    {
        if (freeActivityUI == null || freeActivityText == null)
            yield break;
        freeActivityText.text = showTXT;
        // 设置初始透明度
        CanvasGroup canvasGroup = freeActivityUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = freeActivityUI.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        freeActivityUI.SetActive(true);

        // 淡入
        float elapsedTime = 0f;
        while (elapsedTime < uiFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / uiFadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 保持显示
        yield return new WaitForSeconds(uiDisplayDuration);

        // 淡出
        elapsedTime = 0f;
        while (elapsedTime < uiFadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / uiFadeOutDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        freeActivityUI.SetActive(false);
    }


    #endregion

    #region 相机控制与ESC面板
    public void ToggleGameMode()
    {
        isOverviewMode = !isOverviewMode;

        if (isOverviewMode)
        {
            EnterOverviewMode();
        }
        else
        {
            ExitOverviewMode();
        }

        // 确保UI正确更新
        UpdateSpeedDisplay();
    }

    private void EnterOverviewMode()
    {
        // 保存当前时间缩放
        if (timeManager != null)
        {
            previousTimeScale = timeManager.GetCurrentTimeScale();
        }

        // 禁用玩家控制
        if (playerController != null)
        {
            playerController.enabled = false;

            // 停止玩家动画
            Animator animator = playerController.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
        }

        // 切换到俯瞰视角
        if (cameraTransitionCoroutine != null)
        {
            StopCoroutine(cameraTransitionCoroutine);
        }

        // 设置虚拟相机为全景模式
        if (virtualCamera != null)
        {
            // 取消跟随目标，使相机固定在世界中心
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;

            cameraTransitionCoroutine = StartCoroutine(TransitionVirtualCamera(
                useOrthographic ? overviewCameraSize : overviewFieldOfView,
                overviewCameraOffset,
                true
            ));
        }

        // 显示控制面板
        if (controlPanel != null)
        {
            controlPanel.SetActive(true);
            UpdateSpeedDisplay(); // 确保UI更新
        }

        if (toggleMenuButton != null)
        {
            toggleMenuButton.gameObject.SetActive(true);
            UpdateMenuButtonText();
        }

        if (menuDisplayPanel != null)
        {
            menuDisplayPanel.SetActive(false); // 默认隐藏菜单
            isMenuDisplayed = false;
        }

        Debug.Log("进入全景模式");
    }

    private void ExitOverviewMode()
    {
        // 启用玩家控制
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // 恢复之前的时间缩放
        if (timeManager != null)
        {
            // 只有当之前的时间缩放不为0时才恢复
            if (previousTimeScale > 0)
            {
                timeManager.SetCustomTimeScale(previousTimeScale);
            }
            else
            {
                // 如果之前是暂停状态，恢复为正常速度
                timeManager.SetCustomTimeScale(normalTimeScale);
            }
        }

        // 恢复虚拟相机设置
        if (virtualCamera != null)
        {
            // 恢复跟随目标
            virtualCamera.Follow = originalFollow;
            virtualCamera.LookAt = originalLookAt;

            cameraTransitionCoroutine = StartCoroutine(TransitionVirtualCamera(
                useOrthographic ? normalCameraSize : normalFieldOfView,
                normalCameraOffset,
                false
            ));
        }

        // 隐藏控制面板
        if (controlPanel != null)
        {
            controlPanel.SetActive(false);
        }
        if (toggleMenuButton != null)
        {
            toggleMenuButton.gameObject.SetActive(false);
        }

        if (menuDisplayPanel != null)
        {
            menuDisplayPanel.SetActive(false);
            isMenuDisplayed = false;
        }
        Debug.Log("退出全景模式");
    }


    // 切换菜单显示/隐藏
    public void ToggleMenuDisplay()
    {
        isMenuDisplayed = !isMenuDisplayed;

        if (menuDisplayPanel != null)
        {
            menuDisplayPanel.SetActive(isMenuDisplayed);

            // 如果显示菜单，则填充菜单项
            if (isMenuDisplayed)
            {
                PopulateMenuItems();
            }
        }

        UpdateMenuButtonText();
    }

    // 更新按钮文本
    private void UpdateMenuButtonText()
    {
        if (toggleMenuButtonText != null)
        {
            toggleMenuButtonText.text = isMenuDisplayed ? "隐藏菜单" : "显示菜单";
        }
    }



    private IEnumerator TransitionVirtualCamera(float targetSizeOrFOV, Vector3 targetOffset, bool isOverview)
    {
        if (virtualCamera == null) yield break;

        // 获取当前值
        float startValue = useOrthographic ? virtualCamera.m_Lens.OrthographicSize : virtualCamera.m_Lens.FieldOfView;
        Vector3 startPosition = virtualCamera.transform.position;

        // 获取Transposer组件
        var transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        Vector3 startOffset = transposer != null ? transposer.m_FollowOffset : Vector3.zero;

        // 计算目标位置
        Vector3 targetPosition = CalculateCameraPosition(targetOffset, isOverview);

        float elapsedTime = 0;

        while (elapsedTime < cameraTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / cameraTransitionDuration;
            t = Mathf.SmoothStep(0, 1, t);

            // 平滑过渡相机属性
            if (useOrthographic)
            {
                virtualCamera.m_Lens.OrthographicSize = Mathf.Lerp(startValue, targetSizeOrFOV, t);
            }
            else
            {
                virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(startValue, targetSizeOrFOV, t);
            }

            // 如果是全景模式，直接设置相机位置
            if (isOverview)
            {
                virtualCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            }
            else if (transposer != null)
            {
                // 如果是正常模式，通过Transposer的偏移来控制
                transposer.m_FollowOffset = Vector3.Lerp(startOffset, targetOffset, t);
            }

            yield return null;
        }

        // 设置最终值
        if (useOrthographic)
        {
            virtualCamera.m_Lens.OrthographicSize = targetSizeOrFOV;
        }
        else
        {
            virtualCamera.m_Lens.FieldOfView = targetSizeOrFOV;
        }

        if (isOverview)
        {
            virtualCamera.transform.position = targetPosition;
        }
        else if (transposer != null)
        {
            transposer.m_FollowOffset = targetOffset;
        }
    }

    private Vector3 CalculateCameraPosition(Vector3 offset, bool isOverview)
    {
        if (isOverview)
        {
            // 全景模式：相机位于世界中心上方
            return new Vector3(-4.5f, -5.5f, 0) + offset; //画面中心
        }
        else
        {
            // 正常模式：相机跟随玩家（通过Cinemachine的Follow机制）
            return virtualCamera.transform.position; // 保持当前位置，让Cinemachine处理跟随
        }
    }
    public bool IsOverviewMode()
    {
        return isOverviewMode;
    }

    private void PopulateMenuItems()
    {
        // 清除现有菜单项
        foreach (Transform child in menuContentParent)
        {
            Destroy(child.gameObject);
        }

        // 获取菜单数据
        if (RestaurantManager.Instance == null || RestaurantManager.menuItems == null)
        {
            Debug.LogWarning("无法获取菜单数据");
            return;
        }

        // 设置菜单标题
        if (menuTitleText != null)
        {
            menuTitleText.text = $"餐厅菜单 (共{RestaurantManager.menuItems.Length}道菜)";
        }

        // 创建菜单项
        foreach (RestaurantManager.MenuItem item in RestaurantManager.menuItems)
        {
            GameObject menuItemObj = Instantiate(menuItemPrefab, menuContentParent);
            MenuItemUI menuItemUI = menuItemObj.GetComponent<MenuItemUI>();

            if (menuItemUI != null)
            {
                menuItemUI.Setup(item);
            }
        }
    }

    #endregion 相机控制
    #region 时间流速控制
    // 时间控制方法保持不变
    private void TogglePause()
    {
        if (TimeManager.Instance.GetCurrentTimeScale() > 0)
        {
            TimeManager.Instance.PauseTime();
            if (pauseButton != null) pauseButton.GetComponentInChildren<TMP_Text>().text = "继续";
        }
        else
        {
            TimeManager.Instance.ResumeTime();
            if (pauseButton != null) pauseButton.GetComponentInChildren<TMP_Text>().text = "暂停";
        }
        UpdateSpeedDisplay();
    }

    private void SetNormalSpeed()
    {
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(normalTimeScale);
            UpdateSpeedDisplay();
        }
    }

    private void SetFastSpeed()
    {
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(fastTimeScale);
            UpdateSpeedDisplay();
        }
    }

    private void SetVeryFastSpeed()
    {
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(veryFastTimeScale);
            UpdateSpeedDisplay();
        }
    }

    private void OnSpeedSliderChanged(float value)
    {
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(value);
            UpdateSpeedDisplay();
        }
    }

    private void UpdateSpeedDisplay()
    {
        if (timeManager != null && currentSpeedText != null)
        {
            float currentScale = timeManager.GetCurrentTimeScale();
            currentSpeedText.text = $"流速: x{currentScale:F1}";

            // 更新滑块位置
            if (speedSlider != null)
            {
                speedSlider.SetValueWithoutNotify(currentScale);
            }

            // 更新暂停按钮文本
            if (pauseButton != null)
            {
                pauseButton.GetComponentInChildren<TMP_Text>().text = currentScale > 0 ? "暂停" : "还原";
            }
        }
    }


    #endregion


    #region 进货讲价
    private IEnumerator ShowDialogueSequence()
    {

        // 1. 暂停时间
        timeManager.SetCustomTimeScale(0f);
        string freeActivityTXT = "Negotiation Period!";
        ShowFreeActivityUI(freeActivityTXT);
        yield return new WaitForSeconds(0.1f);
        // 2. 显示对话UI
        DialogueUIManager.Instance.Show();
        //yield return null;
        yield return new WaitForSeconds(0.1f);
        // 3. 显示第一句对话
        bool continued = false;
        DialogueUIManager.Instance.ShowDialogue(
            "Merchant",
            "What do you need",
            () => continued = true
        );
        yield return new WaitUntil(() => continued);
        yield return new WaitForSeconds(0.1f);
        // 4. 等待玩家输入
        string playerInput = "";
        bool inputReceived = false;
        DialogueUIManager.Instance.ShowPlayerInput(
            "请输入回复...",
            (input) => {
                playerInput = input;
                inputReceived = true;
            }
        );
        yield return new WaitUntil(() => inputReceived);
        yield return new WaitForSeconds(0.1f);
        // 5. 显示更多对话...

        // 6. 结束时隐藏UI并恢复时间
        DialogueUIManager.Instance.Hide();
        timeManager.ResumeNormalTime();
    }

    private void TriggerDialogueScene()
    {
        // 设置对话数据
        DialogueSceneManager.NextNPCName = "Merchant";
        DialogueSceneManager.NextDialogue = "What do you need?";
        DialogueSceneManager.ReturnSceneName = SceneManager.GetActiveScene().name;

        // 标记已显示
        //hasShownDay3Dialogue = true;

        // 加载对话场景
        //SceneManager.LoadScene("NegotiationScene");
    }


    #endregion
}