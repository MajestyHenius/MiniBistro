using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using static NPCBehavior;
using static UnityEngine.EventSystems.EventTrigger;
using UnityEngine.Tilemaps;

public class NPCBehavior : MonoBehaviour
{
    private RestaurantManager.Order currentOrder;
    // 定义活动决策的数据结构
    [System.Serializable]
    private class ActivityDecisionData
    {
        public string activity;
        public int duration;
        public string location;
        public string reason;
    }
    // 日程表与动态决策用：
    private int dynamicActivityEndMinutes = -1; // -1表示未设置
    private int dynamicActivityStartDay = -1; // -1表示未设置
    private bool isScheduleReady = true; //现在不需要ai日程表了，直接赋true
    //private bool isWaitingForSchedule = false; 
    // 定义消耗数值的数据结构
    [System.Serializable]
    public class ActivityEffect
    {
        public int healthChange = 0;
        public int energyChange = 0;
        public int moodChange = 0;
        public int moneyChange = 0;
        public int woodChange = 0;
        public string description = "";
    }
    public enum WaiterAction
    {
        GREET,      // 迎接顾客（包含后续的点菜）
        SERVE,      // 上菜服务
        IDLE        // 空闲/等待
        //CLEAN，   // 目前没有数值，清洁的意义不大
        //EXIT      // 愤怒之下不上班了
    }

    // 添加动画相关组件
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isFacingRight = false;

    // 动画方向常量（与玩家一致）
    private const int UP = 0;
    private const int DOWN = 1;
    private const int LEFT = 2;
    private const int RIGHT = 3;
    // AI回复
    [Header("AI回复设置")]
    public string defaultReply = "消息已收到";
    public float replyDisplayTime = 3f;
    public bool playerInRange { get; private set; } = false; // 改为属性，供PlayerInteraction访问

    //加上了机械对话，主动互动机械回复的版本，断网时显示
    [Header("餐厅服务相关")]
    private static bool hasCustomerAtEntrance = false;  // 全局标志：入口是否有顾客
    private static NPCBehavior greetingWaiter = null;  // 正在迎接的服务员
    public float greetingResponseDelay = 2f;  // 响应迎接的延迟时间
    private bool hasDecidedGreeting = false;  // 是否已经做过迎接决策
    // 服务员状态枚举
    public enum WaiterState
    {
        Idle, //在休息室等待
        Greeting, //迎接入口的顾客，需要有全局通知【顾客抵达】 
        TakingOrder, //在座位上等顾客点餐【可以对话，将点菜信息送到后厨】
        DeliveringOrder, //去后厨拿菜，需要有通知【几号桌订单完成】
        ServingFood, //送菜
        Cleaning, //顾客走后清理餐桌，需要有通知【顾客离去】，用桌子空闲标志表示
        MovingToDestination,
        Resting         // 休息
    }

    public string GetWaiterState()
    {
        switch (waiterState)
        {
            case WaiterState.Idle:
                return "正在员工休息室待命";
            case WaiterState.Greeting:
                return "正在迎接顾客";
            case WaiterState.TakingOrder:
                return "正在为顾客点菜";
            case WaiterState.DeliveringOrder:
                return "正在取菜";
            case WaiterState.ServingFood:
                return "正在上菜";
            case WaiterState.Cleaning:
                return "正在清理餐桌";
            case WaiterState.MovingToDestination:
                return "正在忙";

        }
        return "正在忙";
    }

    #region
    // 定义基础参数
    [System.Serializable]
    public class WaiterJsonData
    {
        public string name;
        public string name_eng;
        public int Energy;
        public int Mood;
        public string personalityType;
        public string personalityType_eng;
        public string story;
        public string story_eng;
    }

    #endregion


    [Header("NPC基本信息")]
    public string npcName = "小王";
    public string npcName_eng = "";
    private float currentMoveSpeed = 8f; //移动速度不在这里定义，而是在GameManager的基础速度
    public float GetCurrentMoveSpeed()
    {
        return currentMoveSpeed;
    }
    public string personality = "热心肠";
    public string personality_eng = "";
    public string occupation = "服务员";
    [SerializeField] public int health = 100;
    [SerializeField] public int energy = 50;
    [SerializeField] public int mood = 0;
    [SerializeField] public int tips = 50; //小费
    [SerializeField] public int wood = 0;
    [SerializeField] public string backgroundStory = "";
    [SerializeField] public string backgroundStory_eng = "";//英文翻译的背景故事
    [SerializeField] public int meat = 0;
    [SerializeField] public int vege = 0;
    [Header("最大值设置")]
    [SerializeField] public int maxHealth = 100;
    [SerializeField] public int maxEnergy = 100;
    [SerializeField] public int maxMood = 100;

    public int EffortPoint = 0;
    [Header("UI")]
    public GameObject valueCanvasPrefab;
    public NPCStatusUI statusUI;
    [SerializeField] public bool showStatusUI = false;

    private bool isUIInitialized = false;

    public string GetStatusSummary()
    {
        return $"体力: {energy:F0}/{maxEnergy:F0}| 心情: {mood:F0}/{maxMood:F0} | 金币: {tips}";
    }

    [Header("AI对话设置")]
    private bool useAI = true;  // 是否使用AI生成回复，越改越多了，这里不要改成false

    [Header("NPC交互设置")]
    public float npcInteractionRadius = 3f;
    public float npcGreetChance = 0.7f; // 打招呼的概率
    public float minDialogueInterval = 30f; // 两次对话的最小间隔时间
    private float lastDialogueTime = -100f;
    private NPCBehavior currentTalkingTo;
    private bool isInDialogue = false;
    private const float DIALOGUE_PAUSE_DURATION = 1.5f; // 对话时的短暂停顿，只有NPC vs NPC 使用了，NPC vs 顾客未使用
    private string lastDialogueContent = "";

    [Header("AI日程生成")]  //是否使用AI生成的日程
    [SerializeField] private bool useDynamicDecision = true;  // 是否使用动态决策
    private System.DateTime lastScheduleGeneration;
    private bool isGeneratingSchedule = false;
    private static int totalNPCCount = 0;
    private static int readyNPCCount = 0;
    private static bool hasStartedTime = false;
    private float previousTimeScale;
    public event System.Action<NPCBehavior> OnNPCScheduleReady;

    [Header("位置引用")]
    public Transform entrancePosition; //顾客进入的大门
    public Transform kitchenPosition; //厨房位置
    public Transform tablePosition; //餐桌位置
    public Transform restingPosition; //短暂休息位置
    public Transform cleanerPosition; //清洁、倒垃圾位置
    public float destinationThreshold = 1f;
    [Header("寻路避障")]
    
    [Header("交互设置")]
    public float interactionRadius = 2f;
    public string playerTag = "Player"; // 使用Player标签来识别玩家
    private const string NPCTag = "NPC"; // 标签常量,NPC检测NPC，在这里NPC就特指服务员，顾客用另外Tag，"Customer"表示
    public GameObject dialogueBubblePrefab;
    public Vector3 bubbleOffset = new Vector3(0, 200, 0);
    public float bubbleDisplayTime = 3f;
    // 记忆系统
    private List<string> memoryList = new List<string>(); //既用于实时决策，也用于保存成人类可见txt
    private MemoryManager memoManager;
    public List<string> memoryList_resouce = new List<string>();//与资源增减相关的记忆，单独拎出来留给api作为自己的输入
    private int maxResourceMemories = 10;
    // 记忆容量限制
    private const int MAX_MEMORY_COUNT = 200;
    private List<string> recentMemorySummary = new List<string>(); // 最近几天的摘要
    // 原始的NPC状态
    public WaiterState waiterState = WaiterState.Idle;

    private Transform currentDestination;
    private Coroutine currentRoutine;

    // 对话相关
    private GameObject currentBubble;
    private Coroutine bubbleCoroutine;
    //private bool playerInRange = false;
    private GameObject currentPlayer;
    // ai生成日程
    private List<DailyActivity> dailySchedule;
    private DailyActivity currentActivity;
    private bool isExecutingActivity = false;

    #region****************************************Unity相关****************************************
    private void Awake()
    {
    }
    void Start()
    {
        //InitializeStatusUI();
        //CreateStatusUI();
        memoManager = GetComponent<MemoryManager>();
        //TimeManager.Instance.SetCustomTimeScale(0f);  // 最开始暂停
        SetupTimeManager();
        SetupDialogueBubble();
        InitializeAnimComponents();
        lastDialogueTime = Time.time - minDialogueInterval; // 确保初始可对话
        currentActivity = new DailyActivity();
        StartCoroutine(DelayedInitialize());
    }

    void Update()
    {
        // 检测鼠标左键点击
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("检测到鼠标点击了");
            // 从鼠标位置发射射线
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);

            // 检测射线是否击中了这个NPC
            if (hit.collider != null)
            {
                Debug.Log("检测到鼠标点击到了物体: " + hit.collider.gameObject.name);
                if (hit.collider.gameObject == gameObject)
                {
                    Debug.Log("检测到鼠标点击到了NPC上");
                    HandleClick();
                }
            }
        }
        // 检测玩家
        CheckPlayerInteraction();
        CheckNPCInteraction();
        
    }
    void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.RemoveListener(OnTimeScaleChanged);
        }
    }
    #endregion

    #region****************************************动画相关****************************************
    void InitializeAnimComponents()
    {
        // 初始化动画组件
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = isFacingRight;
        }

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetInteger("Direction", DOWN);//默认向下
        }
    }
    public void HandleAnimation(Vector2 moveDirection)
    {
        bool isMoving = moveDirection.magnitude > 0.1f;

        if (animator != null)
        {
            animator.SetBool("IsWalking", isMoving);

            // 只在有移动输入时更新方向
            if (isMoving)
            {
                int direction = GetDirection(moveDirection);
                animator.SetInteger("Direction", direction);
            }
        }
        // 处理水平翻转
        if (spriteRenderer != null && moveDirection.x != 0)
        {
            bool shouldFaceRight = moveDirection.x > 0;
            if (shouldFaceRight != isFacingRight)
            {
                isFacingRight = shouldFaceRight;
                spriteRenderer.flipX = isFacingRight;
            }
        }
    }

    int GetDirection(Vector2 moveDirection)
    {
        if (moveDirection.magnitude < 0.1f)
        {
            return animator != null ? animator.GetInteger("Direction") : DOWN;
        }

        if (Mathf.Abs(moveDirection.y) > Mathf.Abs(moveDirection.x))
        {
            return moveDirection.y > 0 ? UP : DOWN;
        }
        else
        {
            return moveDirection.x > 0 ? RIGHT : LEFT;
        }
    }
    #endregion




    #region****************************************玩家交互相关****************************************

    void SetupTimeManager()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.AddListener(OnTimeScaleChanged);
            currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();//时间比例更改
        }
    }



    void CheckPlayerInteraction()
    {
        // 获取范围内的所有碰撞体
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRadius);

        bool playerFound = false;
        GameObject foundPlayer = null;

        // 查找是否有玩家
        foreach (var collider in colliders)
        {
            if (collider.CompareTag(playerTag))
            {
                playerFound = true;
                foundPlayer = collider.gameObject;
                break;
            }
        }

        // 处理玩家进入/离开
        if (playerFound && !playerInRange)
        {
            playerInRange = true;
            currentPlayer = foundPlayer;
            OnPlayerEnterRange();
        }
        else if (!playerFound && playerInRange)
        {
            playerInRange = false;
            currentPlayer = null;
            OnPlayerExitRange();
        }
    }

    void OnPlayerEnterRange()
    {
        // 根据当前状态选择对话,改成了其他函数的调用
        StartCoroutine(GreetPlayer());

    }

    void OnPlayerExitRange()
    {
        playerInRange = false;  // 确保设置这个
        HideDialogueBubble();
    }



    public void ShowDialogueBubble(string text)
    {
        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
        }

        if (currentBubble == null)
        {
            currentBubble = Instantiate(dialogueBubblePrefab, transform);
            currentBubble.transform.localPosition = bubbleOffset;
        }

        currentBubble.SetActive(true);

        // 查找Content文本组件的正确路径
        Transform panel = currentBubble.transform.Find("Panel");
        if (panel != null)
        {
            Transform contentTransform = panel.Find("Content");
            if (contentTransform != null)
            {
                TMP_Text tmpText = contentTransform.GetComponent<TMP_Text>();
                if (tmpText != null)
                {
                    //tmpText.font = Resources.Load<TMP_FontAsset>("Fonts/msyh SDF");
                    tmpText.text = text;

                    if (tmpText.font == null)
                    {
                        Debug.LogWarning("字体加载失败，将使用默认字体");
                    }
                }
                else
                {
                    Debug.LogError("Content对象上找不到TMP_Text组件");
                }
            }
            else
            {
                Debug.LogError("Panel下找不到Content对象");
            }
            Transform nameLayer = panel.Find("Header");
            Transform NPCNameLayer = nameLayer.Find("NPCName");
            TMP_Text nameText = NPCNameLayer.GetComponent<TMP_Text>();
            //nameText.font = Resources.Load<TMP_FontAsset>("Fonts/msyh SDF");
            nameText.text = npcName;
        }
        else
        {
            Debug.LogError("找不到Panel对象");
        }

        bubbleCoroutine = StartCoroutine(HideBubbleAfterDelay());
    }

    IEnumerator HideBubbleAfterDelay()
    {
        yield return new WaitForSeconds(bubbleDisplayTime);
        HideDialogueBubble();
    }

    void SetupDialogueBubble()
    {

    }
    void HideDialogueBubble()
    {
        if (currentBubble != null)
        {
            currentBubble.SetActive(false);
        }

        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
            bubbleCoroutine = null;
        }
    }


    void OnTimeScaleChanged(float newTimeScale)
    {
        currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
    }

    // 添加接收消息的方法
    public void ReceivePlayerMessage(string message)
    {
        Debug.Log($"[{npcName}] 收到玩家消息: {message}");
        AddMemory($"老板对你说：{message}");
        if(message.Contains("快去")|| message.Contains("扣") || message.Contains("偷懒") || message.Contains("禁止") || message.Contains("不许"))
        {
            statusUI.UpdateMood(mood, mood - 10);
            mood -= 10;
        }
        // 显示回复
        string reply = "";
        this.GetAIResponse(message, (reply) =>
        {
            Debug.Log($"收到AI回复: {reply}");
            ShowDialogueBubble(reply);
            AddMemory($"你回复老板说：{reply}");
        });
        //string reply = GenerateReply(message); //这是默认的
        //ShowDialogueBubble(reply);

        // 确保对话显示足够长的时间
        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
        }
        bubbleCoroutine = StartCoroutine(ShowBubbleForDuration(replyDisplayTime));
    }

    // 生成回复的方法（可以接入AI）
    string GenerateReply(string playerMessage)
    {
        // 将来这里可以调用AI API
        // 现在先返回默认回复
        return defaultReply;
    }

    // 添加一个新的协程方法来控制显示时间
    IEnumerator ShowBubbleForDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideDialogueBubble();
    }


    public void GetAIResponse(string playerMessage, System.Action<string> onResponse)
    {
        if (!useAI)
        {
            onResponse?.Invoke(GenerateReply(playerMessage));
            return;
        }

        try
        {
            NPCData npcData = NPCData.CreateSafe(this); // 使用安全的工厂方法
            StartCoroutine(AzureOpenAIManager.Instance.GetNPCResponse(npcData, playerMessage, onResponse));
        }
        catch (Exception e)
        {
            Debug.LogError($"获取AI回复时出错: {e.Message}");
            onResponse?.Invoke("抱歉，我现在无法回应");
        }
    }


    // 主动向老板问候的协程
    IEnumerator GreetPlayer()
    {
        // 构建情况描述
        string situation = "";

        switch (waiterState)
        {
            case WaiterState.ServingFood:
                situation = "你正在给顾客上菜，老板走了过来";
                break;
            case WaiterState.TakingOrder:
                situation = "你正在为顾客点菜，老板经过";
                break;
            case WaiterState.Cleaning:
                situation = "你正在清理餐桌，老板走到旁边";
                break;
            case WaiterState.Idle:
                situation = "你刚好有空，老板走了过来";
                break;
            case WaiterState.Greeting:
                situation = "你正在迎接新顾客，老板也在附近";
                break;
            default:
                situation = "老板在你附近";
                break;
        }

        // 决定是否问候
        bool greetingComplete = false;
        string greeting = "";

        if (useDynamicDecision)
        {
            yield return AzureOpenAIManager.Instance.GenerateWaiterToPlayerGreeting(
                this,
                situation,
                (response) =>
                {
                    greeting = response;
                    greetingComplete = true;
                }
            );

            while (!greetingComplete)
            {
                yield return null;
            }
        }
        else
        {
            // 不使用AI时的默认行为
            if (waiterState == WaiterState.Idle)
            {
                greeting = "老板好！";
            }
            else
            {
                greeting = "..."; // 忙碌时不说话
            }
        }

        // 显示对话（如果决定说话）
        if (greeting != "...")
        {
            ShowDialogueBubble(greeting);
            AddMemory($"向老板问候：{greeting}");
        }
        else
        {
            // 可以添加一个点头动画或其他非语言表示
            Debug.Log($"[{npcName}] 太忙了，没有和老板打招呼");
        }

    }

    private string GetLastPlayerCommand()
    {
        // 添加空值检查
        if (memoryList == null || memoryList.Count == 0)
        {
            return null;
        }

        // 查找最近5条记忆中的玩家指令
        var recentPlayerMessages = memoryList
            .Where(m => m.Contains("老板对你说"))
            .TakeLast(5)
            .ToList();

        if (recentPlayerMessages.Count > 0)
        {
            // 返回最近的一条玩家指令
            string lastMessage = recentPlayerMessages.Last();
            int startIndex = lastMessage.IndexOf("老板对你说") + "老板对你说".Length;
            return lastMessage.Substring(startIndex).Trim();
        }

        return null;
    }

    private DailyActivity? ParsePlayerCommand(string command)
    {
        // 添加空值检查
        if (string.IsNullOrEmpty(command))
        {
            return null;
        }

        int currentHour = TimeManager.Instance.CurrentHour;
        int currentMinute = TimeManager.Instance.CurrentMinute;

        if (command.Contains("快去entrance") || command.Contains("去门口") || command.Contains("顾客"))
        {
            //waiterState = WaiterState.Greeting;
            
            return new DailyActivity(currentHour, currentMinute, 10, "执行老板指令:迎接顾客", entrancePosition);
        }
        else if (command.Contains("去干活") || command.Contains("别休息") || command.Contains("还不去干活") || command.Contains("干活"))
        {
            // 根据当前情况选择最合适的工作
            if (RestaurantManager.IsCustomerAtEntrance())
            {
                //waiterState = WaiterState.Greeting;
                return new DailyActivity(currentHour, currentMinute, 10, "执行老板指令:迎接顾客", entrancePosition);
            }
            else
            {
                //waiterState = WaiterState.Cleaning;
                return new DailyActivity(currentHour, currentMinute, 10, "执行老板指令:清理餐桌", tablePosition);
            }
        }
        else if (command.Contains("上菜") || command.Contains("取菜"))
        {
            //waiterState = WaiterState.DeliveringOrder;
            return new DailyActivity(currentHour, currentMinute, 5, "执行老板指令:上菜", kitchenPosition);
        }
        else if (command.Contains("去休息") || command.Contains("休息"))
        {
            //waiterState = WaiterState.Idle;
            return new DailyActivity(currentHour, currentMinute, 15, "执行老板指令:休息", restingPosition);
        }

        return null; // 返回可空类型
    }

    #endregion



    #region****************************************npc互相交互相关****************************************

    public string GetCurrentLocationName()
    {
        try
        {
            // 使用列表避免字典的null key问题
            var locations = new System.Collections.Generic.List<(Transform transform, string name)>();

            // 只添加非空的位置
            if (entrancePosition != null) locations.Add((entrancePosition, "饭店门口"));
            if (kitchenPosition != null) locations.Add((kitchenPosition, "饭店后厨"));
            if (tablePosition != null) locations.Add((tablePosition, "餐桌"));
            if (restingPosition != null) locations.Add((restingPosition, "员工休息室"));
            if (cleanerPosition != null) locations.Add((cleanerPosition, "清洁间"));

            // 如果没有任何有效位置
            if (locations.Count == 0)
            {
                return "员工休息室"; // 返回默认位置
            }

            // 查找最近的位置
            float minDistance = float.MaxValue;
            string closestLocation = "路上";

            foreach (var (transform, name) in locations)
            {
                float distance = Vector3.Distance(transform.position, this.transform.position);
                if (distance < minDistance && distance < 5f)
                {
                    minDistance = distance;
                    closestLocation = name;
                }
            }

            return closestLocation;
        }
        catch (Exception e)
        {
            Debug.LogError($"[{npcName}] GetCurrentLocationName异常: {e.Message}");
            return "员工休息室"; // 出错时返回默认值
        }
    }
    void CheckNPCInteraction()
    {
        // 添加调试日志
        //Debug.Log($"[{npcName}] 检测NPC交互 - 状态: {currentState}, 执行活动: {isExecutingActivity}");
        // 如果正在对话、执行活动或时间间隔不够，则跳过
        if (isInDialogue || isExecutingActivity ||
            Time.time - lastDialogueTime < minDialogueInterval)
        {
            return;
        }

        // 获取范围内的NPC
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, npcInteractionRadius);
        List<NPCBehavior> nearbyNPCs = new List<NPCBehavior>();

        foreach (var collider in colliders)
        {
            // 确保只检测NPC标签的对象
            if (collider.CompareTag("NPC") && collider.gameObject != gameObject)
            {
                NPCBehavior npc = collider.GetComponent<NPCBehavior>();
                if (npc != null && !npc.isInDialogue &&
                    !npc.isExecutingActivity && // 确保对方没有执行活动
                    Time.time - npc.lastDialogueTime > npc.minDialogueInterval)
                {
                    nearbyNPCs.Add(npc);
                    Debug.Log($"[{npcName}] 发现附近NPC: {npc.npcName}");
                }
            }
        }

        // 随机选择是否发起对话，弃用
        //if (nearbyNPCs.Count > 0 && UnityEngine.Random.value < npcGreetChance)
        {
            NPCBehavior targetNPC = GetClosestNPC(nearbyNPCs);
            if (targetNPC != null)
            {
                //Debug.Log($"[{npcName}] 决定与 {targetNPC.npcName} 对话");
                StartCoroutine(InitiateDialogue(targetNPC));
            }
        }
    }

    private NPCBehavior GetClosestNPC(List<NPCBehavior> npcs)
    {
        NPCBehavior closest = null;
        float minDistance = float.MaxValue;

        foreach (NPCBehavior npc in npcs)
        {
            float distance = Vector2.Distance(transform.position, npc.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = npc;
            }
        }
        return closest;
    }

    // 添加对话协程
    private IEnumerator InitiateDialogue(NPCBehavior otherNPC)
    {
        // 标记双方为对话中
        isInDialogue = true;
        otherNPC.isInDialogue = true;
        currentTalkingTo = otherNPC;

        // 保存原始移动状态（不影响实际移动）
        Vector3 originalPosition = transform.position;
        Vector3 otherOriginalPosition = otherNPC.transform.position;

        // 生成问候语
        yield return StartCoroutine(GenerateDialogue(otherNPC, true));
        
        // 短暂停顿
        yield return new WaitForSeconds(DIALOGUE_PAUSE_DURATION);
        // 生成回应
        yield return otherNPC.StartCoroutine(otherNPC.GenerateDialogue(this, false));
        // 恢复状态
        isInDialogue = false;
        otherNPC.isInDialogue = false;
        currentTalkingTo = null;
        lastDialogueTime = Time.time;
        otherNPC.lastDialogueTime = Time.time;
    }

    // 添加对话生成方法
    public IEnumerator GenerateDialogue(NPCBehavior otherNPC, bool isInitiator)
    {
        string safeNpcName = string.IsNullOrEmpty(npcName) ? "NPC" : npcName;
        string safePersonality = string.IsNullOrEmpty(personality) ? "" : personality;

        if (otherNPC == null)
        {
            Debug.LogError($"[{safeNpcName}] 对话对象不存在，终止对话");
            yield break;
        }

        string safeOtherNpcName = string.IsNullOrEmpty(otherNPC.npcName) ? "其他NPC" : otherNPC.npcName;
        string safeLastDialogue = currentTalkingTo != null ?
            (string.IsNullOrEmpty(currentTalkingTo.lastDialogueContent) ? "你好" : currentTalkingTo.lastDialogueContent)
            : "你好";

        // 使用 NPCData,下一步增加记忆，金币、健康值等的影响。
        // 地点读取还有一点问题
        //还需要添加时间
        string context;
        if (isInitiator)
        {
            //NPCData initiatorData = new NPCData(this);
            //NPCData targetData = new NPCData(otherNPC);
            NPCData initiatorData = NPCData.CreateSafe(this);
            NPCData targetData = NPCData.CreateSafe(otherNPC);
            //暂时不加地点
            context = $"{initiatorData.npcName}（{initiatorData.personality}，{initiatorData.occupation}）" +
                      $"在{initiatorData.currentLocation}遇到了{targetData.npcName}（{targetData.personality}，{targetData.occupation}）。" +
                      $"当前体力值是{initiatorData.energy}/{initiatorData.maxEnergy}，心情值是{initiatorData.mood}/{initiatorData.maxMood}" +
                      $"你今天收到了{initiatorData.money}小费。" +
            "请根据你的性格和当前状态，简短打个招呼（1-2句话）,如果体力值、心情值非常高或者非常低，要体现在对话中。";
        }
        else
        {
            NPCData responderData = new NPCData(this);
            //暂时不加地点
            context = $"{otherNPC.npcName}对你说：\"{safeLastDialogue}\"。\n" +
                      $"你当前在{responderData.currentLocation}，状态是{responderData.currentState}。" +
                      $"当前体力值是{responderData.energy}/{responderData.maxEnergy}，心情值是{responderData.mood}/{responderData.maxMood}" +
                      $"你今天收到了{responderData.money}小费。" +
                      $"请根据你的性格（{responderData.personality}）和当前状态简短回复（1-2句话），如果体力值、心情值非常高或者非常低，要体现在对话中。";
        }

        //Debug.Log($"[{safeNpcName}] 对话上下文: {context}");

        NPCData npcData = new NPCData(this);
        string dialogueContent = "";

        // 创建包装器来捕获协程结果
        CoroutineWithData coroutineData = new CoroutineWithData(
            this,
            AzureOpenAIManager.Instance.GetNPCResponse(
                npcData,
                context,
                (response) => { dialogueContent = response; }
            )
        );
        
            
        // 等待API协程完成
        yield return coroutineData.coroutine;

        // 检查是否获得有效响应
        if (string.IsNullOrEmpty(dialogueContent))
        {
            Debug.LogWarning("API返回空响应，使用默认回复");
            dialogueContent = isInitiator ? "你好！" : "我听到了！";
        }

        // 记录对话
        lastDialogueContent = dialogueContent;

        // 显示对话气泡
        ShowDialogueBubble(dialogueContent);

        // 等待对话显示时间
        yield return new WaitForSeconds(bubbleDisplayTime);
        if (isInitiator)
        {
            AddMemory($"向{otherNPC.npcName}打招呼说：{dialogueContent}");
        }
        else
        {
            AddMemory($"{otherNPC.npcName}对你说：{safeLastDialogue}");
            AddMemory($"向{otherNPC.npcName}回复说：{dialogueContent}");
        }


        // 隐藏气泡
        HideDialogueBubble();

        // 记录对话日志
        if (DialogueLogger.Instance != null)
        {
            DialogueLogger.Instance.LogDialogue(
                npcName,
                otherNPC.npcName,
                dialogueContent,
                transform.position
            );
        }
        else
        {
            Debug.LogWarning("DialogueLogger实例缺失");
        }
    }

    // 添加获取活动描述的方法





    private class CoroutineWithData
    {
        public Coroutine coroutine;
        public object result;
        private IEnumerator target;

        public CoroutineWithData(MonoBehaviour owner, IEnumerator target)
        {
            this.target = target;
            this.coroutine = owner.StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            while (target.MoveNext())
            {
                result = target.Current;
                yield return result;
            }
        }
    }





    #endregion


    #region****************************************智能体行为相关****************************************

    #region 日程表

    public bool IsScheduleReady() //其实已经不用了
    {
        return isScheduleReady;
    }

    void InitializeDailySchedule()
    {
        // 初始化一个默认的日程，改成饭馆
        dailySchedule = new List<DailyActivity>
        {
            new DailyActivity(16, 0, 30, "准备餐厅", entrancePosition),
            new DailyActivity(16, 30, 360, "营业阶段，服务客人", restingPosition),
            new DailyActivity(22, 30, 30, "清理准备打烊", cleanerPosition)
        };
    }
    // 服务员具体行为应包括：看见进来新客人，前去领座，点菜，交给后厨，从后厨上菜
    IEnumerator DelayedInitialize()
    {
        // 等待一帧，确保所有组件都已初始化
        yield return null;

        // 等待TimeManager初始化
        while (TimeManager.Instance == null)
        {
            Debug.Log($"[{npcName}] 等待 TimeManager 初始化...");
            yield return new WaitForSeconds(0.1f);
        }

        // 等待AzureOpenAIManager初始化（如果使用AI）
        if (useDynamicDecision)
        {
            while (AzureOpenAIManager.Instance == null)
            {
                Debug.Log($"[{npcName}] 等待 AzureOpenAIManager 初始化...");
                yield return new WaitForSeconds(0.1f);
            }
        }

        InitializeStatusUI();
        isScheduleReady = true;
        InitializeDailySchedule();
        string memoryTemp = $"制定了今天的计划：{GetScheduleSummary()}";//get方法也可以改为直接在npc体中调取
        AddMemory(memoryTemp);
        if (useDynamicDecision)
        {
            //StartCoroutine(HourlyRoutine());
            StartCoroutine(RealtimeDecision());
        }
        else
        {
            //StartCoroutine(DailyRoutine());
        }

    }
   

    public struct DailyActivity
    {
        public int startHour;
        public int startMinute;
        public int durationMinutes;
        public string activity;
        public Transform destination;
        public Vector3 position; // 添加位置字段，用于AI生成的日程
        public DailyActivity(int hour, int minute, int duration, string act, Transform dest)
        {
            startHour = hour;
            startMinute = minute;
            durationMinutes = duration;
            activity = act;
            destination = dest;
            position = dest != null ? dest.position : Vector3.zero;
        }

        // 新增构造函数，支持Vector3位置，用于随机事件、寻找其他Agent
        public DailyActivity(int hour, int minute, int duration, string act, Vector3 pos)
        {
            startHour = hour;
            startMinute = minute;
            durationMinutes = duration;
            activity = act;
            destination = null;
            position = pos;
        }
    }

    private string GetScheduleSummary()
    {
        // 添加空值检查
        if (dailySchedule == null || dailySchedule.Count == 0)
        {
            Debug.Log($"[{npcName}] 日程表为空，返回默认日程参考");
            return "暂无预定日程（自由决策模式）";
        }

        StringBuilder summary = new StringBuilder();
        foreach (var activity in dailySchedule)
        {
            summary.AppendLine($"{activity.startHour:D2}:{activity.startMinute:D2} - {activity.activity}");
        }
        return summary.ToString();
    }

    // 通用活动执行方法
    IEnumerator PerformGenericActivity(DailyActivity activity)
    {
        waiterState = WaiterState.Idle;

        while (IsStillInActivityTime(activity))
        {
            yield return new WaitForSeconds(0.5f);
        }
    }

    #endregion

    #region 实时决策
    [System.Serializable]
    public class WaiterDecision
    {
        public WaiterAction action;
        public string dialogue; // 可能需要说的话
        public string reason;
    }
    private Coroutine decisionRoutine;
    private bool isMoving = false;
    public IEnumerator RealtimeDecision()
    {
        while (true)
        {
            // 只有当空闲时才做决策
            if (waiterState == WaiterState.Idle)
            {
                Debug.Log($"[{npcName}] 准备获取新指令");

                WaiterDecision decision = new WaiterDecision();
                bool decisionDecided = false;

                yield return GetWaiterDecision((d) =>
                {
                    decision = d;
                    decisionDecided = true;
                });

                while (!decisionDecided)
                {
                    yield return null;
                }

                Debug.Log($"[{npcName}] 决定执行: {decision.action}, 理由: {decision.reason}");

                // 根据决策执行相应行动
                switch (decision.action)
                {
                    case WaiterAction.GREET:
                        yield return StartCoroutine(PerformGreetingAndOrderAction(decision.dialogue));
                        break;

                    case WaiterAction.SERVE:
                        yield return StartCoroutine(PerformServingAction());
                        break;

                    case WaiterAction.IDLE:
                        // 空闲状态，移动到休息点并等待一段时间
                        yield return StartCoroutine(MoveToDestination(restingPosition));
                        yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 10f));
                        break;
                }
            }

            yield return null;
        }
    }

    private IEnumerator GetWaiterDecision(System.Action<WaiterDecision> onDecisionDecided)
    {
        int currentHour = TimeManager.Instance.CurrentHour;
        int currentMinute = TimeManager.Instance.CurrentMinute;
        string currentTime = $"{currentHour:D2}:{currentMinute:D2}";

        // 检查环境状态 - 确保检查最新的订单状态
        CheckForNewOrders(); // 添加这行，确保订单队列是最新的
        var myReadyOrder = RestaurantManager.GetReadyOrderForWaiter(this);
        bool hasOrderToPickup = myReadyOrder != null || orderQueue.Count > 0;
        bool hasCustomerAtEntrance = RestaurantManager.IsCustomerAtEntrance();
        bool isAnyoneGreeting = RestaurantManager.IsAnyoneGreeting();
        bool canGreet = RestaurantManager.TryReserveGreeting(this);

        // 构建环境情况描述
        string environmentInfo = "";

        if (hasCustomerAtEntrance)
        {
            if (canGreet)
            {
                environmentInfo += "- 门口有顾客等待，你可以去迎接\n";
            }
            else
            {
                environmentInfo += "- 门口有顾客等待，但已有其他服务员在迎接\n";
            }
        }

        if (hasOrderToPickup)
        {
            environmentInfo += $"- 有{orderQueue.Count}个订单需要上菜\n";
            environmentInfo += "- 上菜任务有最高优先级！\n";
        }

        // 服务员状态信息
        string statusInfo = $"体力:{energy}/{maxEnergy}, 小费:{tips}";
        string currentMemory = string.Join("\n", memoryList.TakeLast(3));

        string prompt = $@"作为{npcName}（{occupation}），基于以下情况决定下一步行动：
时间：{currentTime}
状态：{statusInfo}
环境情况：
{environmentInfo}
最近记忆：
{currentMemory}

请基于你的性格（{personality}）选择最合适的行动：
1. 上菜任务有最高优先级！如果有订单需要上菜，你必须选择SERVE！
2. 如果有顾客在门口且无人迎接，可以考虑选择GREET
3. 如果没有紧急任务，可以选择IDLE休息

可选行动：
- GREET: 迎接顾客（包含后续的点菜）
- SERVE: 上菜服务
- IDLE: 空闲/休息

请返回JSON格式：
{{
  ""action"": ""行动名称"",
  ""dialogue"": ""如果需要说的话"",
  ""reason"": ""选择此行动的理由""
}}";

        // 调用AI决策
        bool responseReceived = false;
        string aiResponse = "";

        NPCData npcData = NPCData.CreateSafe(this);
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
            var decision = ParseWaiterDecision(aiResponse);

            // 验证决策合理性 - 强制优先级
            if (hasOrderToPickup && decision.action != WaiterAction.SERVE)
            {
                Debug.LogWarning($"[{npcName}] 有订单但AI未选择上菜，强制改为上菜");
                decision.action = WaiterAction.SERVE;
                decision.reason = "有订单需要立即上菜";
            }
            // 如果AI想迎接但无法预订，改为空闲
            else if (decision.action == WaiterAction.GREET && !canGreet)
            {
                Debug.LogWarning($"[{npcName}] 想迎接但无法预订，改为空闲");
                decision.action = WaiterAction.IDLE;
                decision.reason = "无法迎接顾客，改为空闲";
            }

            onDecisionDecided?.Invoke(decision);
        }
        catch (Exception e)
        {
            Debug.LogError($"解析决策失败: {e.Message}");
            // 默认决策 - 空闲
            onDecisionDecided?.Invoke(new WaiterDecision
            {
                action = WaiterAction.IDLE,
                reason = "解析决策失败，默认空闲"
            });
        }
    }

    private WaiterDecision ParseWaiterDecision(string jsonResponse)
    {
        //Debug.Log($"AI决策响应: {jsonResponse}");
        try
        {
            // 提取JSON内容
            int startIdx = jsonResponse.IndexOf('{');
            int endIdx = jsonResponse.LastIndexOf('}');
            if (startIdx >= 0 && endIdx >= 0)
            {
                jsonResponse = jsonResponse.Substring(startIdx, endIdx - startIdx + 1);
            }

            // 使用Unity的JsonUtility解析
            return JsonUtility.FromJson<WaiterDecision>(jsonResponse);
        }
        catch (Exception e)
        {
            Debug.LogError($"解析决策异常: {e.Message}");
            throw;
        }
    }

    // 迎接并点菜的行动
    private IEnumerator PerformGreetingAndOrderAction(string dialogue)
    {
        Debug.Log($"[{npcName}] 开始执行迎接和点菜行动");

        if (RestaurantManager.greetingWaiter != this)
        {
            Debug.Log($"[{npcName}] 迎接任务已被其他服务员接管");
            yield break;
        }

        // 获取在入口等待的顾客
        CustomerNPC waitingCustomer = RestaurantManager.GetCustomerAtEntrance();

        if (waitingCustomer == null)
        {
            Debug.LogWarning($"[{npcName}] 没有找到等待的顾客");
            RestaurantManager.SetCustomerAtEntrance(false);
            yield break;
        }

        // 移动到入口位置
        yield return StartCoroutine(MoveToDestination(entrancePosition));
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // 如果有对话内容，显示对话
        if (!string.IsNullOrEmpty(dialogue))
        {
            ShowDialogueBubble(dialogue);
        }
        else
        {
            ShowDialogueBubble("欢迎光临！请跟我来");
        }

        AddMemory($"迎接顾客：{dialogue}");

        // 通知顾客被迎接
        waitingCustomer.BeGreetedByWaiter(this, dialogue);

        yield return new WaitForSeconds(0.5f / TimeManager.Instance.timeScale);

        // 分配餐桌
        Transform assignedTable = RestaurantManager.AssignTable();
        if (assignedTable == null)
        {
            ShowDialogueBubble("抱歉，目前没有空桌，请稍等");
            AddMemory("没有空桌可分配");

            // 只在自己是当前迎接者时清除全局状态
            if (RestaurantManager.greetingWaiter == this)
            {
                RestaurantManager.SetCustomerAtEntrance(false);
                RestaurantManager.SetGreetingWaiter(null);
            }

            yield break;
        }

        // 引导顾客到餐桌
        ShowDialogueBubble("请跟我来，这边请");
        AddMemory($"带领顾客前往{assignedTable.name}");

        // 服务员先移动到餐桌
        yield return StartCoroutine(MoveToDestination(assignedTable));

        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // 让顾客跟随到餐桌
        yield return waitingCustomer.MoveToPosition(assignedTable.position);

        // 现在才让顾客进入Ordering状态
        waitingCustomer.BeSeatedByWaiter(this, assignedTable);

        // 确保顾客的assignedTable设置了
        if (waitingCustomer.assignedTable == null)
        {
            waitingCustomer.assignedTable = assignedTable;
        }

        AddMemory($"成功将顾客安排到{assignedTable.name}");

        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);

        // 执行点菜
        yield return StartCoroutine(PerformTakingOrder(assignedTable, waitingCustomer));
        AddMemory("完成点单，返回休息区");

        // 返回休息区
        yield return StartCoroutine(MoveToDestination(restingPosition));

        // 只在自己是当前迎接者时清除全局状态
        if (RestaurantManager.greetingWaiter == this)
        {
            RestaurantManager.SetCustomerAtEntrance(false);
            RestaurantManager.SetGreetingWaiter(null);
        }

        Debug.Log($"[{npcName}] 迎接和点菜行动完成");
    }

    // 点菜行动
    private IEnumerator PerformTakingOrder(Transform table, CustomerNPC customer)
    {
        if (customer == null || customer.gameObject == null)
        {
            Debug.LogWarning($"[{npcName}] 顾客已不存在，终止操作");
            yield break;
        }

        Debug.Log($"[{npcName}] 开始点菜服务");
        AddMemory($"为顾客点菜");

        // 等待顾客点菜
        yield return new WaitUntil(() => !string.IsNullOrEmpty(customer.GetOrderedFood()));

        string orderedDish = customer.GetOrderedFood();

        yield return new WaitUntil(() => !customer.isBeingServed);

        RestaurantManager.AddOrder(customer, this, table, orderedDish);
        Debug.Log($"AddOrder执行完毕");
        AddMemory($"完成为顾客点菜，订单已确认");

        // 等待顾客状态更新
        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);

        if (customer != null)
        {
            customer.ReleaseWaiter();
            Debug.Log($"[{npcName}] 释放顾客 {customer.customerName}");
        }

        Debug.Log($"[{npcName}] 点菜服务完成");
    }

    // 上菜行动
    private IEnumerator PerformServingAction()
    {
        Debug.Log($"[{npcName}] 开始执行上菜行动");

        // 确保订单队列是最新的
        CheckForNewOrders();

        if (orderQueue.Count == 0)
        {
            Debug.LogWarning($"[{npcName}] 没有找到任何待处理的订单，返回休息区");
            // 移动回休息区
            yield return StartCoroutine(MoveToDestination(restingPosition));
            yield break;
        }

        RestaurantManager.Order currentOrder = orderQueue.Peek();
        if (currentOrder.customer == null || currentOrder.customer.gameObject == null)
        {
            Debug.Log($"[{npcName}] 订单 #{currentOrder.orderId} 的顾客已离开，取消上菜");
            orderQueue.Dequeue();
            RestaurantManager.PickupOrder(currentOrder.orderId);
            yield break;
        }

        CustomerNPC assignedCustomer = currentOrder.customer;
        if (currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Leaving ||
            currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Paying ||
            currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Eating)
        {
            Debug.Log($"[{npcName}] 顾客已离开或正在用餐，取消上菜订单 #{currentOrder.orderId}");
            orderQueue.Dequeue();
            RestaurantManager.PickupOrder(currentOrder.orderId);
            yield break;
        }

        Debug.Log($"[{npcName}] 开始上菜服务 - 订单#{currentOrder.orderId}");
        AddMemory($"前往厨房取餐：{currentOrder.dishName}");

        // 先去厨房取餐
        yield return StartCoroutine(MoveToDestination(kitchenPosition));
        yield return new WaitForSeconds(2f / TimeManager.Instance.timeScale);

        // 通知餐厅管理器订单已被取走
        RestaurantManager.PickupOrder(currentOrder.orderId);
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // 再到对应餐桌
        yield return StartCoroutine(MoveToDestination(currentOrder.table));

        ShowDialogueBubble($"您的{currentOrder.dishName}来了，请慢用");
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // 通知顾客开始用餐
        if (assignedCustomer != null)
        {
            assignedCustomer.currentState = CustomerNPC.CustomerState.Eating;
            AddMemory($"为{assignedCustomer.customerName}送上{currentOrder.dishName}");
        }

        // 从队列中移除已处理的订单
        orderQueue.Dequeue();

        Debug.Log($"[{npcName}] 上菜行动完成");
    }
    // 上菜
    IEnumerator PerformServingFood(DailyActivity activity)
    {
        CheckForNewOrders();
        if (orderQueue.Count == 0)
        {
            // 尝试从RestaurantManager获取订单
            var readyOrders = RestaurantManager.GetReadyOrders();
            var myOrders = readyOrders.Where(o => o.waiter == this).ToList();

            if (myOrders.Count > 0)
            {
                Debug.Log($"[{npcName}] 从RestaurantManager找到{myOrders.Count}个订单，加入队列");
                foreach (var order in myOrders)
                {
                    orderQueue.Enqueue(order);
                }
            }
            else
            {
                Debug.LogWarning($"[{npcName}] 没有找到任何待处理的订单，返回休息区");
                waiterState = WaiterState.Idle;

                // 移动回休息区
                yield return StartCoroutine(MoveToDestination(restingPosition));
                yield break;
            }
        }
        RestaurantManager.Order currentOrder = orderQueue.Peek();
        if (currentOrder.customer == null || currentOrder.customer.gameObject == null)
        {
            Debug.Log($"[{npcName}] 订单 #{currentOrder.orderId} 的顾客已离开，取消上菜");
            orderQueue.Dequeue();

            // 通知RestaurantManager清理这个订单
            RestaurantManager.PickupOrder(currentOrder.orderId);

            waiterState = WaiterState.Idle;
            yield break;
        }


        CustomerNPC assignedCustomer = currentOrder.customer;
        if (currentOrder.customer == null ||
    currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Leaving ||
    currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Paying||
    currentOrder.customer.GetCurrentState() ==CustomerNPC.CustomerState.Eating)
        {
            Debug.Log($"[{npcName}] 顾客已离开，取消上菜订单 #{currentOrder.orderId}");
            orderQueue.Dequeue();
            RestaurantManager.PickupOrder(currentOrder.orderId);
            yield break;
        }
        Debug.Log($"[{npcName}] 开始上菜服务 - 订单#{currentOrder.orderId}");
        AddMemory($"前往厨房取餐：{currentOrder.dishName}");

        // 先去厨房取餐
        yield return StartCoroutine(MoveToDestination(kitchenPosition));
        yield return new WaitForSeconds(2f / TimeManager.Instance.timeScale);

        // 通知餐厅管理器订单已被取走
        RestaurantManager.PickupOrder(currentOrder.orderId);
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;
        // 再到对应餐桌
        yield return StartCoroutine(MoveToDestination(currentOrder.table));

        ShowDialogueBubble($"您的{currentOrder.dishName}来了，请慢用");
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;
        // 通知顾客开始用餐
        if (assignedCustomer != null)
        {
            assignedCustomer.currentState = CustomerNPC.CustomerState.Eating;
            AddMemory($"为{assignedCustomer.customerName}送上{currentOrder.dishName}");
        }



        //waiterState = WaiterState.Idle;
        //isExecutingActivity = false;
    }

    void CheckForNewOrders()
    {
        var readyOrders = RestaurantManager.GetReadyOrders();
        var myOrders = readyOrders.Where(o => o.waiter == this).ToList();

        foreach (var order in myOrders)
        {
            // 避免重复添加
            if (!orderQueue.Any(o => o.orderId == order.orderId))
            {
                orderQueue.Enqueue(order);
                Debug.Log($"[{npcName}] 添加新订单到队列: #{order.orderId}");
            }
        }
    }


    // 接收订单完成通知
    private Queue<RestaurantManager.Order> orderQueue = new Queue<RestaurantManager.Order>();
    public void NotifyOrderReady(RestaurantManager.Order order)
    {
        // 将订单加入队列
        orderQueue.Enqueue(order);
        AddMemory($"收到通知：订单#{order.orderId}({order.dishName})已完成，加入队列");

        // 不再启动 ProcessOrderQueue，而是依赖决策系统
        // 决策系统会在下一次决策时检测到订单并选择上菜
    }


    // 订单队列处理方法
    private IEnumerator ProcessOrderQueue()
    {
        while (orderQueue.Count > 0)
        {
            RestaurantManager.Order nextOrder = orderQueue.Peek();

            DailyActivity servingActivity = new DailyActivity(
                TimeManager.Instance.CurrentHour,
                TimeManager.Instance.CurrentMinute,
                5, // 5分钟上菜时间
                $"上菜：{nextOrder.dishName}",
                nextOrder.table
            );

            currentActivity = servingActivity;
            isExecutingActivity = true;

            // 执行上菜活动
            yield return StartCoroutine(PerformServingFood(servingActivity));

            // 完成后从队列中移除
            if (orderQueue.Count > 0 && orderQueue.Peek().orderId == nextOrder.orderId)
            {
                orderQueue.Dequeue();
            }
        }
    }

    // 清洁
    IEnumerator PerformCleaning(DailyActivity activity)
    {
        Debug.Log($"[{npcName}] 开始清洁工作");
        AddMemory($"清理餐桌");

        yield return new WaitForSeconds(5f / TimeManager.Instance.timeScale);

        waiterState = WaiterState.Idle;
    }

    IEnumerator MoveToDestination(Transform destination)
    {
        waiterState = WaiterState.MovingToDestination;
        currentDestination = destination;

        while (Vector2.Distance(transform.position, destination.position) > destinationThreshold)
        {
            // 使用MoveTowards计算新位置
            transform.position = Vector2.MoveTowards(transform.position, destination.position, currentMoveSpeed * Time.deltaTime);
            HandleAnimation((destination.position - transform.position).normalized);
            yield return null;
        }

        transform.position = destination.position;
        waiterState = WaiterState.Idle;
        if (animator != null)
            animator.SetBool("IsWalking", false);
    }



    bool IsStillInActivityTime(DailyActivity activity)
    {
        // 使用 GetDayMinutes 获取当天内的分钟数
        int currentDayMinutes = TimeManager.Instance.GetDayMinutes();
        int currentDay = TimeManager.Instance.GetDayCount();

        // 如果是动态决策模式
        if (useDynamicDecision && dynamicActivityEndMinutes >= 0)
        {
            // 检查是否跨天
            if (dynamicActivityStartDay >= 0 && currentDay > dynamicActivityStartDay)
            {
                // 已经到了第二天，需要调整计算
                int minutesSinceStart = (currentDay - dynamicActivityStartDay) * 24 * 60 + currentDayMinutes;
                //int activityStartMinutes = dynamicActivityEndMinutes - currentActivity.durationMinutes;

                // 如果活动应该已经结束
                if (minutesSinceStart >= currentActivity.durationMinutes)
                {
                    return false;
                }
            }
            else if (currentDay == dynamicActivityStartDay)
            {
                // 同一天内，直接比较
                return currentDayMinutes < (dynamicActivityEndMinutes % (24 * 60));
            }

            return true;
        }

        // 原有的固定日程检查逻辑
        int activityStartMinutes = activity.startHour * 60 + activity.startMinute;
        int activityEndMinutes = activityStartMinutes + activity.durationMinutes;

        if (activityEndMinutes > 24 * 60)
        {
            // 活动跨天
            activityEndMinutes = activityEndMinutes % (24 * 60);
            return currentDayMinutes >= activityStartMinutes || currentDayMinutes < activityEndMinutes;
        }
        else
        {
            return currentDayMinutes >= activityStartMinutes && currentDayMinutes < activityEndMinutes;
        }
    }


    IEnumerator PerformResting(DailyActivity activity) // 暂时把resting和idle合并了，idle中需要再补充
    {
        // 检查是否应该执行
        if (!isExecutingActivity)
        {
            yield break;
        }
        int startDay = TimeManager.Instance.GetDayCount();
        int startDayMinutes = TimeManager.Instance.GetDayMinutes();
        int startTotalMinutes = TimeManager.Instance.GetTotalMinutes();

        float hours = activity.durationMinutes / 60f;
        Debug.Log($"[{npcName}] ===== 开始休息 =====");
        Debug.Log($"[{npcName}] 初始状态 - 体力: {energy}/{maxEnergy}, 木材: {wood}");
        //int checkCount = 0;
        yield return StartCoroutine(MoveToDestination(restingPosition));

        // 记录上一个小时，用于检测小时变化
        int lastHour = TimeManager.Instance.CurrentHour;
        int lastTime = TimeManager.Instance.GetTotalMinutes();
        int hoursRested = 0;
        int debugCounter = 0;
        while (IsStillInActivityTime(activity) && isExecutingActivity)
        {
            waiterState = WaiterState.Idle;
            int currentHour = TimeManager.Instance.CurrentHour;
            int currentTime = TimeManager.Instance.GetTotalMinutes();
            if (currentTime - lastTime>=10) //每一个十分钟休息回复10点，还得修改
            {
                lastTime = currentTime;

                if (energy < maxEnergy)
                {
                    int energyBefore = energy;
                    energy += 10;
                    statusUI.UpdateEnergy(energyBefore, energy);

                    Debug.Log($"[{npcName}] 完成第{hoursRested}个休息小时 - 体力: {energy}/{maxEnergy}");

                }
                else
                {
                    Debug.LogWarning($"[{npcName}] 体力已满");
                    // 可以选择是否要提前结束工作
                    // break;
                }


            }
            debugCounter++;
            if (debugCounter % 20 == 0)
            {
                int currentTotalMinutes = TimeManager.Instance.GetTotalMinutes();
                int elapsedMinutes = currentTotalMinutes - startTotalMinutes;

                //Debug.Log($"[{npcName}] 休息中... 已休息{elapsedMinutes}分钟");
                //Debug.Log($"  isExecutingActivity: {isExecutingActivity}");
                //Debug.Log($"  currentRoutine: {currentRoutine}");
            }

            yield return new WaitForSeconds(0.5f / TimeManager.Instance.timeScale);
        }

        int endTotalMinutes = TimeManager.Instance.GetTotalMinutes();
        int actualDuration = endTotalMinutes - startTotalMinutes;

        //Debug.Log($"[{npcName}] ===== 休息结束 =====");
        //Debug.Log($"[{npcName}] 实际持续: {actualDuration}分钟，完成{hoursRested}个休息小时");
        //Debug.Log($"[{npcName}] 最终状态 - 体力: {energy}/{maxEnergy}, 木材: {wood}");
        string memoryTemp2 = $"[{npcName}]休息了{actualDuration}分钟，体力: {energy}/{maxEnergy}";//get方法也可以改为直接在npc体中调取
        AddMemory(memoryTemp2);


        waiterState = WaiterState.Idle;
    }

    #endregion


    #region 记忆相关
    public void AddMemory(string memoryContent) //通用，无论是制定日程表、记录移动工作、对话，都在外面写好memoryContent，然后加进来。
    {
        // 获取当前游戏时间
        string timestamp = $"{TimeManager.Instance.CurrentHour:D2}:{TimeManager.Instance.CurrentMinute:D2}";
        // 格式化记忆条目
        string formattedMemory = $"[{timestamp}] {memoryContent}";
        // 添加到记忆列表
        memoryList.Add(formattedMemory);
        // 检查是否超出容量限制
        if (memoryList.Count > MAX_MEMORY_COUNT)
        {
            // 移除最早的记忆（保持列表大小）
            memoryList.RemoveAt(0);
        }
        //Debug.Log($"{npcName}的新记忆: {formattedMemory}");
    }


    public void AddMemory_resource(string memoryContent)  // 跟小费、收入有关的资源记忆，聚焦于餐厅则弃用
    {
        // 获取当前游戏时间
        string timestamp = $"{TimeManager.Instance.CurrentHour:D2}:{TimeManager.Instance.CurrentMinute:D2}";
        // 格式化记忆条目
        string formattedMemory = $"[{timestamp}] {memoryContent}";
        // 添加到记忆列表
        memoryList_resouce.Add(formattedMemory);
        // 检查是否超出容量限制
        if (memoryList_resouce.Count > maxResourceMemories)
        {
            // 移除最早的记忆（保持列表大小）
            memoryList.RemoveAt(0);
        }
        //Debug.Log($"{npcName}的新记忆: {formattedMemory}");
    }
    public List<string> GetAllMemories() // 读取记忆，用于MemoryManager保存成txt
    {
        return new List<string>(memoryList);
    }
    public void ClearDailyMemories() // 清空当日记忆。但是可以留一个关于价格、资源数值的摘要
    {
        memoryList.Clear();
        Debug.Log($"{npcName}的记忆已清空，准备新的一天");
    }


    public IEnumerator GenerateDailyReflection(System.Action<string> onResponse)
    {
        if (memoryList.Count == 0)
        {
            onResponse?.Invoke("今天没什么特别的事情发生。");
            yield break;
        }

        // 构建系统提示
        string systemPrompt = $@"你是一个餐厅服务员{npcName}，请根据今天的工作记忆进行反思总结。
性格：{personality}
背景故事：{backgroundStory}
反思要求：
1. 用第一人称思考，总结今天的经历、感受和学习点
2. 简洁明了，1-2句话
3. 体现个人情感和成长，符合你的性格特点
4. 基于具体事件进行思考
5. 使用自然的口语化表达";

        // 将记忆列表转换为字符串
        string memoryContext = string.Join("\n",memoryList);
        // 构建用户提示
        string userPrompt = $"这是你今天的工作记录：\n{memoryContext}\n\n请基于这些经历进行反思：";

        yield return AzureOpenAIManager.Instance.SendDialogueRequest(systemPrompt, userPrompt, 150, 0.8f, onResponse);
    }


    public void TriggerDailyReflection()
    {
        if (memoryList.Count > 0)
        {
            string reflection="";
            StartCoroutine(GenerateDailyReflection(reflection =>
            {
                string reflectionMemory = $"今日反思: {reflection}";
                AddMemory(reflectionMemory);
                SaveMemory(); //反思是一天结束的时候，既保存记忆，也保存反思。

            }));
            
            Debug.Log($"{npcName}的生成本日反思: {reflection}");
            
        }
        else
        {
            Debug.Log($"记忆为空");
        }
    }
    public void SaveMemory()
    {
        // 获取或创建MemoryManager实例
        MemoryManager memoryManager = FindObjectOfType<MemoryManager>();
        if (memoryManager != null)
        {
            memoryManager.SaveMemoryToFile(this);
        }
        else
        {
            Debug.LogError("找不到MemoryManager实例");
        }
    }



    #endregion

    #endregion

    #region****************************************NPC_UI相关****************************************

    private void InitializeStatusUI()
    {
        if (valueCanvasPrefab == null)
        {
            Debug.LogError($"NPC {npcName} 的状态UI预制体未设置!");
            return;
        }

        // 生成UI实例
        GameObject uiInstance = Instantiate(valueCanvasPrefab, transform.position, Quaternion.identity);

        // 获取NPCStatusUI组件
        statusUI = uiInstance.GetComponent<NPCStatusUI>();

        if (statusUI == null)
        {
            Debug.LogError("生成的UI预制体上没有NPCStatusUI组件!");
            Destroy(uiInstance);
            return;
        }

        // 设置UI的NPC引用
        statusUI.npc = this;

        // 初始化UI（更新文本等）
        statusUI.InitializeUI();

        // 确保UI初始时是隐藏的
        statusUI.HideUI();
    }

    private void HandleClick()
    {
        Debug.Log($"点击了NPC: {npcName}");

        if (statusUI != null)
        {
            statusUI.ToggleUI();
            Debug.Log($"UI状态: {(statusUI.IsUIVisible ? "显示" : "隐藏")}");
        }
        else
        {
            Debug.LogError("statusUI为空！");
        }
    }
    #endregion





}
