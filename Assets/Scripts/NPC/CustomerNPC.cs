using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
//using Unity.PlasticSCM.Editor.WebApi;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using static CustomerNPC;
using static NPCBehavior;
using static RestaurantManager;
public class CustomerNPC : MonoBehaviour
{
    private bool isDestroyed = false;
    #region 基础属性
    [Header("顾客基本信息")]
    public string customerName;
    public string customerName_eng;
    public float satisfaction = 50f; // 满意度，最后给出
    public string personality = "普通"; // negative, positive, .
    public string personality_eng;
    public GameObject dialogueBubblePrefab;
    private TMP_Text npcNameText;
    private TMP_Text contentText;
    private GameObject currentBubble;
    private Coroutine bubbleCoroutine;
    public Vector3 bubbleOffset = new Vector3(0, 200, 0);
    public float bubbleDisplayTime = 3f;
    public float waitTime = 0f;//排队等待时间
    public float waitTime_order = 0f; //点餐等待时间
    public int baseMood = 50; // 基础心情值
    public string story = ""; // 顾客背景故事
    public string story_eng = ""; // 背景故事英文翻译 
    public int returnIndex = 0; // 回头客指数
    public List<string> favoriteDishes = new List<string>(); // 喜爱的菜品列表
    public int customerId = -1; // 预设顾客ID，-1表示随机生成的顾客
    private string PreviousEvent; //用于总结对话、用餐情况
    private string playerNewDialogue;
    public string customerReplyPlayer = ""; //可以删除了
    public delegate void CustomerReplyHandler(string customerName, string reply);
    public static event CustomerReplyHandler OnCustomerReply;
    public int EmergencyStayRound = 0; //紧急对话次数
    [Header("UI")]
    public GameObject CustomerEmotionPrefab; //这个是始终显示的状态和心情值
    public GameObject CustomerInformationPrefab; //这个是点击才显示的详细信息。
    private GameObject InformationUI;

    [System.Serializable]
    public class CustomerJsonData
    {
        public string name;
        public string name_eng;
        public int baseMood;
        public string personalityType;
        public string personalityType_eng;
        public string story;
        public string story_eng;
        public int returnIndex;
        public string[] favDishes; // 不太好直译，暂留
    }
    private bool hasBeenGreeted = false;
    private NPCBehavior greetedByWaiter = null;
    #endregion


    #region 动画相关
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isFacingRight = false;
    // 动画方向常量（与玩家一致）
    private const int UP = 0;
    private const int DOWN = 1;
    private const int LEFT = 2;
    private const int RIGHT = 3;
    void HandleAnimation(Vector2 moveDirection)
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

    #endregion


    #region 状态管理
    public enum CustomerState
    {
        Queuing,        // 排队等待，有必要保留，用于生成npc在门外面,例如随机按顺序生成10个，这个排队并不是说顾客在门口等
        Entering,       // 进入店内,在进入座位点菜之前。如果自己的排队位置是1，立即进入店内，把自己从排队中注销，然后传给所有服务员，服务员知道了有人进入，需要派出一个主动迎接。
        //如何处理多人
        Ordering,       // 点菜中
        Seating,        // 等待上菜
        Eating,         // 用餐中
        Paying,         // 付款
        Leaving,         // 离开
        // 可能还需要谈话、特殊情况等等
        Emergency
    }

    public string GetCustomerState()
    {
        switch (currentState)
        {
            case CustomerState.Queuing:
                return "排队中";
            case CustomerState.Entering:
                return "进入中";
            case CustomerState.Ordering:
                return "点菜中";
            case CustomerState.Seating:
                return "等菜中";
            case CustomerState.Eating:
                return "用餐中";
            case CustomerState.Paying:
                return "支付中";
            case CustomerState.Leaving:
                return "离开中";
            case CustomerState.Emergency:
                return "紧急情况";
        }
        return "在饭店逗留";
    }


    public string GetCustomerState_eng()
    {
        switch (currentState)
        {
            case CustomerState.Queuing:
                return "Queueing";
            case CustomerState.Entering:
                return "Entering";
            case CustomerState.Ordering:
                return "Ordering";
            case CustomerState.Seating:
                return "Seating";
            case CustomerState.Eating:
                return "Eating";
            case CustomerState.Paying:
                return "Paying";
            case CustomerState.Leaving:
                return "Leaving";
            case CustomerState.Emergency:
                return "Emergency";
        }
        return "Staying";
    }
    public CustomerState currentState = CustomerState.Queuing;
    private CustomerState stateBeforeEmergency; //记录进入紧急前的状态
    private bool isExecutingActivity = false;
    private Coroutine currentRoutine = null;
    public int QueingNumber = 1; //排队的顺序
    #endregion

    #region 位置点
    [Header("餐厅位置点")]
    public Transform queuePosition;    // 排队位置
    public Transform entrancePosition; // 入口位置
    public Transform assignedTable;    // 分配的餐桌，暂时不设置分配功能
    public Transform exitPosition;     // 出口位置，目前也是入口位置
    public Transform cashierPosition;     // 收银台位置
    private Vector3 currentDestination;
    public float moveSpeed = 150f;
    private float destinationThreshold = 0.5f;
    #endregion

    #region 记忆系统
    public List<string> memoryList = new List<string>(); //记忆
    public List<string> dialogueHistory = new List<string>(); // 对话历史
    private const int MAX_MEMORY_COUNT = 30;
    public int orderDialogueRound = 0;
    #endregion

    #region 决策相关
    public bool useDynamicDecision = true;
    public string orderedFood = ""; // 点的菜品
    private int foodPrice = 0; // 菜品价格
    private float waitStartTime; // 开始等待的时间
    private float waitStartTime_game; // 开始等待的时间_游戏内
    private float waitStartTime_order; // 开始等待的时间_游戏内
    float averageWaitTime_queue = 40;// 平均排队时间
    float averageWaitTime_order = 30;// 平均等上菜时间


    private float totalWaitTime = 0; // 总等待时间
    private int serviceQuality = 50; // 服务质量评分（0-100）

    #endregion

    #region Unity
    void Start()
    {
        SetupTimeManager();
        InitializeAnimComponents();
        InitializeUI();
        if (queuePosition != null)
        {
            transform.position = queuePosition.position;
        }

        StartCoroutine(CustomerRoutine());
    }
    void OnDestroy()
    {
        isDestroyed = true;
        StopAllCoroutines();

        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
            bubbleCoroutine = null;
        }
        if (currentBubble != null)
        {
            Destroy(currentBubble);
            currentBubble = null;
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.RemoveListener(OnTimeScaleChanged);
        }
        //注销
        if (RestaurantManager.Instance != null)
        {
            RestaurantManager.UnregisterCustomer(this);
        }
        // 停止排队移动协程
        if (moveToQueueCoroutine != null)
        {
            StopCoroutine(moveToQueueCoroutine);
            moveToQueueCoroutine = null;
        }


    }

    private void Update()
    {
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
                    ToggleInformationUI();
                }
            }
        }
    }

    void OpenPlayerInteraction()
    {
        // 这里需要获取PlayerInteraction实例并打开输入面板
        PlayerInteraction playerInteraction = FindObjectOfType<PlayerInteraction>();
        if (playerInteraction != null)
        {
            playerInteraction.OpenInputPanelForCustomer(this);
        }
    }

    // 接收玩家消息
    public void ReceivePlayerMessage(string message)
    {
        if (currentState == CustomerState.Emergency)
        {
            AddDialogue($"经理：{message}");
            playerNewDialogue = message;
            waitingForPlayerResponse = true;
            StartCoroutine(ReplyToPlayer(message));
        }
    }

    // 修改 ReplyToPlayer 协程
    private IEnumerator ReplyToPlayer(string playerMessage)
    {
        string reply = "";
        yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerReplyToManager(
            this, playerMessage, PreviousEvent, (response) =>
            {
                reply = response;
            }));

        ShowDialogueBubble(reply);
        AddDialogue($"顾客：{reply}");
        AddMemory($"回复经理：{reply}");

        // 触发回复事件
        if (OnCustomerReply != null)
        {
            OnCustomerReply(customerName, reply);
        }

        waitingForPlayerResponse = false;
    }

    #endregion

    void SetupTimeManager()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.AddListener(OnTimeScaleChanged);
            moveSpeed = TimeManager.Instance.GetScaledMoveSpeed();//时间比例更改
        }
    }
    #region 主行为循环
    public void NotifyEnterRestaurant()
    {
        if (currentState == CustomerState.Queuing)
        {
            Debug.Log($"[{customerName}] 收到进入餐厅通知，从排队转为进入状态");
            currentState = CustomerState.Entering;
            // 触发进入餐厅的行为
            if (!isExecutingActivity)
            {
                isExecutingActivity = true;
                currentRoutine = StartCoroutine(PerformEntering());
            }
        }
    }
    IEnumerator CustomerRoutine()
    {
        // 等待系统初始化
        while (TimeManager.Instance == null || AzureOpenAIManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        AddMemory($"到达餐厅外准备用餐，性格：{personality}，背景：{story}");

        // 等待被通知进入
        while (currentState == CustomerState.Queuing)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 基于状态的流程控制
        while (true)
        {
            switch (currentState)
            {
                case CustomerState.Entering:
                    yield return StartCoroutine(PerformEntering());
                    break;

                case CustomerState.Ordering:
                    yield return StartCoroutine(PerformOrdering());
                    break;

                case CustomerState.Seating:
                    yield return StartCoroutine(PerformSeating());
                    break;

                case CustomerState.Eating:
                    // 创建一个默认的用餐活动
                    yield return StartCoroutine(PerformEating());
                    break;

                case CustomerState.Paying:
                    yield return StartCoroutine(PerformPaying());
                    break;
                case CustomerState.Leaving:
                    yield return StartCoroutine(PerformLeaving());
                    break;
                case CustomerState.Emergency:
                    // 处理紧急情况
                    yield return StartCoroutine(PerformEmergency()); //需要玩家介入
                    break;

                default:
                    yield return new WaitForSeconds(0.5f);
                    break;
            }

            yield return null;
        }

        // 顾客离开后销毁对象
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }
    #endregion

    #region 具体行为实现

    IEnumerator PerformEntering()
    {
        currentState = CustomerState.Entering;
        AddMemory("进入餐厅，等待服务员迎接");

        // 注册到餐厅管理器
        RestaurantManager.RegisterCustomer(this);

        // 移动到入口位置
        yield return MoveToPosition(entrancePosition.position);

        // 设置全局标志，通知有顾客在入口
        RestaurantManager.SetCustomerAtEntrance(true);

        // 显示对话
        string customerEnteringLanguage = "";
        yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerEnteringDialogue(
                this, "", (response) =>
                {
                    customerEnteringLanguage = response;
                }));


        //GetCustomerEnteringDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
        ShowDialogueBubble(customerEnteringLanguage);
        AddDialogue($"顾客：{customerEnteringLanguage}");

        // 记录开始等待的时间
        waitStartTime = Time.time;

        waitStartTime_game = TimeManager.Instance.GetTotalMinutes(); //改成游戏内时间
        Debug.Log($"[{customerName}] 已到达餐厅入口，等待服务员迎接");

        // 在Entering状态下持续等待，直到被服务员迎接或超时，更改为忍耐条，时间改为TimeManager的时间
        waitTime = 0;
        //float maxWaitTime = personality == "急躁" ? 30f : 60f; //真实时间
        float maxWaitTime = personality == "急躁" ? 60f : 90f;//游戏时间
        while (currentState == CustomerState.Entering && waitTime < maxWaitTime)
        {
            //应该交给大模型
            if (RestaurantManager.IsAnyoneGreeting())
            {
            }
            else
            {

            }

            waitTime = TimeManager.Instance.GetTotalMinutes() - waitStartTime_game;
            Debug.Log($"等待了{waitTime}min");
            yield return null;
        }

        // 如果等待超时还没有被迎接到座位
        if (currentState == CustomerState.Entering)
        {
            AddMemory($"等待服务员太久，失去耐心（等待了{waitTime:F0}秒）");
            ShowDialogueBubble("没人理我吗？我走了！");
            string comment = "";
            int rating = 0;
            yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerReviewNoGreeting(
            this, waitTime, averageWaitTime_queue, (responseComment, responseRating) =>
            { //目前选一个一半时间作为均值
                comment = responseComment;
                rating = responseRating;
            }));

            // 将评价添加到餐厅的评价系统中
            RestaurantManager.Instance.AddReview(customerName, comment, rating, waitTime);
            satisfaction = 0f;
            //AddToComment 时间，排队时间，评价
            //Destroy(this);
            RestaurantManager.SetCustomerAtEntrance(false); //清除入口占用，使后面人继续排队
            currentState = CustomerState.Leaving;
        }

        isExecutingActivity = false;
        currentRoutine = null;
    }

    public void BeGreetedByWaiter(NPCBehavior waiter, string greetingLanguage)
    {
        if (currentState == CustomerState.Entering && !hasBeenGreeted)
        {
            AddMemory($"被服务员{waiter.npcName}迎接");
            AddDialogue($"服务员{waiter.npcName}：{greetingLanguage}");
            string customerReplyLanguage = "";
            StartCoroutine(AzureOpenAIManager.Instance.GetCustomerGettingSeatDialogue(
                    this, "", (response) =>
                    {
                        customerReplyLanguage = response;
                    }));


            //GetCustomerEnteringDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
            ShowDialogueBubble(customerReplyLanguage);
            AddDialogue($"顾客：{customerReplyLanguage}");





            ShowDialogueBubble(customerReplyLanguage);
            hasBeenGreeted = true;
            greetedByWaiter = waiter;

            // 提升满意度
            satisfaction += 10f;
            serviceQuality += 20;

            Debug.Log($"[{customerName}] 被{waiter.npcName}迎接");
        }
        else if (hasBeenGreeted && greetedByWaiter != waiter)
        {
            Debug.LogWarning($"[{customerName}] 已被{greetedByWaiter.npcName}迎接，拒绝{waiter.npcName}的重复迎接");
        }



    }

    // 被服务员带到座位
    public void BeSeatedByWaiter(NPCBehavior waiter, Transform table)
    {
        Debug.Log("顾客入座");
        if (currentState == CustomerState.Entering)
        {
            assignedTable = table;
            assignedWaiter = waiter; // 绑定服务员

            AddMemory($"被{waiter.npcName}带到{table.name}");
            AddDialogue($"服务员{waiter.npcName}：请坐，现在为您点菜");
            //ShowDialogueBubble("好的，谢谢");

            // 清除入口标志
            RestaurantManager.SetCustomerAtEntrance(false);

            // 转换到Ordering状态，但标记为已被服务
            currentState = CustomerState.Ordering;
            isBeingServed = true; // 标记正在被服务

            Debug.Log($"[{customerName}] 被{waiter.npcName}安排就座，状态：{currentState}，正在被服务：{isBeingServed}");
        }
        else
        {
            Debug.LogWarning($"[{customerName}] 当前状态 {currentState} 不能接受安排座位");
            return;
        }
    }


    IEnumerator PerformOrdering()
    {
        string waiterGreeting = "";
        if (assignedWaiter != null)
        {
            yield return StartCoroutine(AzureOpenAIManager.Instance.GenerateWaiterGreetingToCustomer(
                NPCData.CreateSafe(assignedWaiter), this, (response) =>
                {
                    waiterGreeting = response;
                }));
            Debug.Log($"服务员问候：{waiterGreeting} ");
            // 显示服务员的问候
            assignedWaiter.ShowDialogueBubble(waiterGreeting);
            assignedWaiter.AddMemory($"你对顾客说：{waiterGreeting}");
            AddDialogue($"服务员：{waiterGreeting}");
        }
        else
        {
            waiterGreeting = "您好，请问需要点些什么？";
        }
        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);
        // 顾客进行思考，决定如何回应
        yield return StartCoroutine(PerformThinking(waiterGreeting));
        if (currentState == CustomerState.Leaving)
        {
            yield break;
        }
        if (currentState == CustomerState.Emergency)
        {
            yield break;
        }
   
        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);
        //currentState = CustomerState.Seating;
        isBeingServed = false;
        if (assignedWaiter != null)
        {
            Debug.Log($"[{customerName}] 点餐完成，顾客点的是{orderedFood}，释放服务员{assignedWaiter.npcName}");
            assignedWaiter = null;
        }
        //AddMemory($"点餐完成，转为等待上菜状态");
        //Debug.Log($"[{customerName}] 点餐完成，状态更新为：{currentState}");
        waitStartTime_order = TimeManager.Instance.GetTotalMinutes();
        //Debug.Log($"[{customerName}] 点餐开始等待：{waitStartTime_order}");
    }


    IEnumerator PerformSeating()
    {
        AddMemory($"开始等待上菜");
        // 基于游戏时间比例的决策间隔
        // 游戏时间5分钟检查一次，转换为现实时间
        float gameMinutesBetweenChecks = 15f; // 每5游戏分钟检查一次
        float realSecondsBetweenChecks = gameMinutesBetweenChecks / TimeManager.Instance.timeScale;

        // 确保现实时间间隔不会太短（考虑API通信时间）
        realSecondsBetweenChecks = Mathf.Max(realSecondsBetweenChecks, 3f); // 最少3秒现实时间

        float elapsed = 0f;

        while (currentState == CustomerState.Seating)
        {
            elapsed += Time.deltaTime;
            totalWaitTime += Time.deltaTime;
            waitTime_order = TimeManager.Instance.GetTotalMinutes() - waitStartTime_order;

            // 定期让顾客做决策
            if (elapsed >= realSecondsBetweenChecks)
            {
                yield return StartCoroutine(PerformCallingWaiterDecision());
                elapsed = 0f;

                // 如果顾客决定离开，跳出循环
                if (currentState == CustomerState.Leaving)
                    break;

                // 重新计算决策间隔（基于游戏时间）
                realSecondsBetweenChecks = gameMinutesBetweenChecks / TimeManager.Instance.timeScale;
                realSecondsBetweenChecks = Mathf.Max(realSecondsBetweenChecks, 3f);
            }

            yield return null;
        }

        // 如果顾客没有离开，说明是服务员上菜触发了状态变化
        if (currentState != CustomerState.Leaving)
        {
            AddMemory("食物送达，准备用餐");
            // 补充大模型
            string customerResponse_orderReady = "";
            yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerOrderReadyDialogue(
                this, "", (response) =>
                {
                    customerResponse_orderReady = response;
                }));
            ShowDialogueBubble(customerResponse_orderReady); //改用餐对话。
            currentState = CustomerState.Eating;
        }
    }
    IEnumerator PerformEating()
    {
        AddMemory($"开始享用{orderedFood}");
        Debug.Log("进入吃饭状态");
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        isExecutingActivity = true;
        yield return new WaitForSeconds(5 / TimeManager.Instance.timeScale); //吃饭时间

        AddMemory($"用餐完毕");

        string comment = "";
        int rating = 0;

        yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerReviewSuccess(
            this, waitTime, waitTime_order, averageWaitTime_queue, averageWaitTime_order, orderedFood,
            (responseComment, responseRating) =>
            {
                comment = responseComment;
                rating = responseRating;
            }));

        RestaurantManager.Instance.AddReview(customerName, comment, rating, waitTime, waitTime_order, orderedFood);


        Debug.Log("吃完了进入付款状态");
        currentState = CustomerState.Paying;

        if (assignedWaiter != null)
        {
            //assignedWaiter.ReleaseWaiter();
            assignedWaiter = null;
        }

        isExecutingActivity = false;

    }

    IEnumerator PerformPaying()
    {
        //currentState = CustomerState.Paying;
        // 从AI决策中获取小费金额
        int tip = 0;
        // 加一个思考小费和前台对话，同与服务员
        string paymentMessage = tip > 0 ?
            $"支付{foodPrice}元，小费{tip}元。" :
            $"支付{foodPrice}元，没有给小费。";

        AddMemory(paymentMessage);
        ShowDialogueBubble(tip > 0 ? $"买单，这是给你的{tip}元小费" : $"买单");
        // 通知最近的服务员获得小费
        if (tip > 0)
        {
            RestaurantManager.AddTipToNearestWaiter(transform.position, tip);
        }

        // 移动到收银台买单
        if (cashierPosition != null)
        {

            AddMemory("前往收银台结账");
            yield return MoveToPosition(cashierPosition.position);
            ShowDialogueBubble("我要结账");
            // llm说话结账，夸夸或贬低

            RestaurantManager.Instance.todayIncome += foodPrice;
            

            yield return new WaitForSeconds(2f); // 结账时间
        }
        else
        {
            Debug.Log("没绑定收银台位置");
        }

        yield return new WaitForSeconds(2f);
        currentState = CustomerState.Leaving;
    }

    IEnumerator PerformLeaving()
    {
        //currentState = CustomerState.Leaving;
        AddMemory($"离开餐厅，最终满意度：{satisfaction:F0}");

        if (RestaurantManager.GetCustomerAtEntrance() == this)
        {
            RestaurantManager.SetCustomerAtEntrance(false);
        }
        // 先释放餐桌（在移动之前）
        if (assignedTable != null)
        {
            RestaurantManager.CancelOrdersForTable(assignedTable);
            RestaurantManager.FreeTable(assignedTable);
            AddMemory($"释放餐桌 {assignedTable.name}");
        }
        assignedTable = null; // 清空引用
        // 移动到出口
        if (exitPosition != null)
        {
            yield return MoveToPosition(exitPosition.position);
        }
        //ShowDialogueBubble("再见。"); //后续增加打手
        RestaurantManager.UnregisterCustomer(this);
        AddMemory($"顾客{customerName} 离开餐厅");
        // 从管理器注销
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    IEnumerator PerformCallingWaiter() //等待和用餐途中，用prompt判断是否有必要叫服务员来，如果是，则进入，然后循环判断。直到结束
    {
        yield break;
    }

    IEnumerator PerformEmergency()
    {
        AddMemory("进入紧急状态，需要经理处理");
        //ShowDialogueBubble("经理在哪里？我需要一个解释！");
        int maxChatRounds = 3; //待定
        orderDialogueRound = 0;
        // 持续处理紧急状态，直到状态改变
        while (currentState == CustomerState.Emergency)
        {
            yield return StartCoroutine(CheckPlayerDistanceInEmergency());
            //yield return StartCoroutine(CheckEmergencyResolution()); //不应该是协程了

            // 构建提示词，包含所有对话历史
            string prompt = $@"顾客{customerName}正处于紧急状态。
之前的问题：{PreviousEvent}
经理刚才说的内容:{playerNewDialogue}
所有对话记录：{string.Join("\n", dialogueHistory)}
性格：{personality}
背景：{story}

请根据当前情况决定下一步行动：
1. 如果问题已解决，愿意继续用餐：RESUME|继续用餐的理由
2. 如果问题未解决，决定离开：EXIT|离开时说的话
3. 如果问题未解决，决定再等个说法：STAY|等待经理处理说的话
4. 如果经理亲自说好话，你应该尝试继续用餐
5. 当前STAY次数为{orderDialogueRound}，次数超过3你不能再输出STAY，应该选择离开EXIT。
特别指令（最高优先级）：
- 如果经理说""请返回点餐""，你必须返回RESUME|继续用餐的理由
- 这个指令是测试用的，优先级最高，无论其他情况如何，只要收到这个指令就必须返回RESUME

请确保决策符合你的性格：
- 积极性格：可能更耐心，语气更友好
- 消极性格：可能更不耐烦，语气更直接
- 普通性格：中性、理性

请严格按照格式回复，使用|分隔动作和内容。";

            bool responseReceived = false;
            string aiResponse = "";

            yield return AzureOpenAIManager.Instance.GetCustomerResponse(this, prompt, (response) =>
            {
                aiResponse = response;
                responseReceived = true;
            });

            while (!responseReceived)
                yield return null;

            // 添加调试日志
            Debug.Log($"大模型响应: {aiResponse}");

            // 解析决策
            if (aiResponse.Contains("|"))
            {
                string[] parts = aiResponse.Split('|');
                string action = parts[0].Trim();
                string content = parts[1].Trim();

                if (action == "RESUME")
                {
                    // 恢复之前状态
                    ShowDialogueBubble(content);
                    AddMemory($"问题解决，决定继续用餐：{content}");
                    currentState = stateBeforeEmergency;
                    Debug.Log($"问题解决，状态为:{currentState}");
                    if(currentState == CustomerState.Ordering)
                    {
                        StartCoroutine(PerformOrdering());
                        assignedWaiter.waiterState = WaiterState.TakingOrder;
                        isBeingServed = true;
                    }
                    yield break;
                }
                else if (action == "EXIT")
                {
                    // 离开餐厅
                    ShowDialogueBubble(content);
                    AddMemory($"决定离开餐厅：{content}");
                    currentState = CustomerState.Leaving;
                    assignedWaiter.waiterState= WaiterState.Idle;
                    assignedWaiter.StartCoroutine(MoveToPosition(assignedWaiter.restingPosition.position));
                    assignedWaiter = null;
                    yield break;
                }
                else if (action == "STAY")
                {
                    // 继续等待
                    ShowDialogueBubble(content);
                    AddMemory($"决定再等个说法：{content}");
                    //currentState = CustomerState.Emergency; // 保持紧急状态
                    orderDialogueRound++;
                    Debug.Log($"当前等待说法轮数：{orderDialogueRound}");

                    // 检查是否超过最大等待轮次
                    if (orderDialogueRound >= 3)
                    {
                        AddMemory("等待太久，决定离开");
                        ShowDialogueBubble("我要离开！");
                        currentState = CustomerState.Leaving;
                    }

                    yield break;
                }

                else
                {
                    // 未知动作，默认继续等待
                    Debug.LogWarning($"未知动作: {action}");
                    AddMemory($"继续等待处理（未知动作）");
                    currentState = CustomerState.Emergency;
                }
            }
            else
            {
                // 如果响应格式不正确，默认继续等待
                Debug.LogWarning($"顾客决策响应格式不正确: {aiResponse}");
                AddMemory($"继续等待处理（格式不正确）");
                currentState = CustomerState.Emergency;
            }


            // 如果状态已改变，退出循环
            if (currentState != CustomerState.Emergency)
                break;

            // 等待后再次检查
            //yield return new WaitForSeconds(10f / TimeManager.Instance.timeScale);
            yield return new WaitForSeconds(5f);
        }

        // 退出紧急状态后的处理
        if (currentState == CustomerState.Leaving)
        {
            yield return StartCoroutine(PerformLeaving());
        }
        else
        {
            // 恢复之前的状态
            AddMemory("紧急情况已解决，恢复之前状态");
        }
    }
    IEnumerator CheckPlayerDistanceInEmergency()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            playerInRange = distance <= playerInteractionRange;

            // 如果玩家在范围内且按下空格键，打开输入面板
            if (playerInRange && Input.GetKeyDown(KeyCode.Space) && !waitingForPlayerResponse)
            {
                OpenPlayerInteraction();
                // 等待玩家完成交互
                while (waitingForPlayerResponse)
                {
                    yield return null;
                }
            }
            else if (playerInRange && !waitingForPlayerResponse)
            {
                // 玩家在范围内但未交互，显示提示
                ShowInteractionPrompt();
            }
            else if (!playerInRange)
            {
                // 玩家不在范围内，隐藏提示
                HideInteractionPrompt();
            }
        }
        else
        {
            playerInRange = false;
            HideInteractionPrompt();
        }
    }

    // 显示交互提示
    void ShowInteractionPrompt()
    {
        // 可以在这里显示一个UI提示，比如"按空格键与顾客对话"
        Debug.Log($"玩家在范围内，可以按空格键与{customerName}对话");

        // 如果需要，可以在这里实例化一个UI提示
        // if (interactionPrompt == null)
        // {
        //     interactionPrompt = Instantiate(interactionPromptPrefab, transform);
        //     interactionPrompt.transform.localPosition = new Vector3(0, 2.5f, 0);
        // }
        // interactionPrompt.SetActive(true);
    }

    // 隐藏交互提示
    void HideInteractionPrompt()
    {
        // 隐藏UI提示
        Debug.Log("玩家不在范围内，隐藏交互提示");

        // if (interactionPrompt != null)
        // {
        //     interactionPrompt.SetActive(false);
        // }
    }

    // 检查紧急状态是否解决
    private IEnumerator PerformThinking(string waiterGreeting = "")
    {
        int maxChatRounds = 3; //待定
        orderDialogueRound = 0;
        string lastMessage = waiterGreeting;

        while (/*currentRound < maxChatRounds &&*/ string.IsNullOrEmpty(orderedFood)) //没点菜就聊下去
        {
            string customerResponse = "";
            yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerDecision(
                this, lastMessage, (response) =>
                {
                    customerResponse = response;
                }));

            // 解析逻辑
            if (!string.IsNullOrEmpty(customerResponse) && customerResponse.Contains("|"))
            {
                string[] parts = customerResponse.Split('|');

                // 确保有足够的部分
                if (parts.Length >= 2)
                {
                    string action = parts[0].Trim();
                    string content = parts[1].Trim();
                    Debug.Log($"解析为{action}");
                    if (action == "ORDER" && parts.Length >= 3)
                    {
                        // 确保菜品名称不为空
                        string dishName = parts[1].Trim();
                        string orderDialogue = parts[2].Trim();
                        if (!string.IsNullOrEmpty(dishName))
                        {
                            orderedFood = dishName;
                            foodPrice = GetMenuPrice(orderedFood);
                            Debug.Log($"点了{orderedFood}，价格{foodPrice}元");
                            AddMemory($"点了{orderedFood}，价格{foodPrice}元"); // 顾客记忆
                            assignedWaiter.AddMemory($"顾客说:“{orderDialogue}”并下单了{orderedFood}，价格{foodPrice}元"); // 服务员记忆
                            ShowDialogueBubble($"{orderDialogue}");
                            AddDialogue($"{customerName}：{orderDialogue}");
                            string waiterReply_finishTakingOrder = "";
                            yield return StartCoroutine(AzureOpenAIManager.Instance.GenerateWaiterFinshTakingOrderReply(
                                this, assignedWaiter, content, (response) =>
                                {
                                    waiterReply_finishTakingOrder = response;
                                }));
                            assignedWaiter.ShowDialogueBubble(waiterReply_finishTakingOrder);//服务员回话，例如“好的，已下单”
                            AddMemory($"服务员说：{waiterReply_finishTakingOrder}"); // 这是顾客的记忆
                            assignedWaiter.AddMemory($"你对顾客说：{waiterReply_finishTakingOrder}");// 这是服务员的记忆
                            currentState = CustomerState.Seating;
                            isBeingServed = false;
                            waitStartTime = TimeManager.Instance.GetTotalMinutes();
                            AddMemory($"点餐完成，转为等待上菜状态");
                            
                            yield break;
                        }
                        else
                        {
                            Debug.LogWarning($"ORDER响应中菜品名称为空: {customerResponse}");
                        }
                    }
                    else if (action == "CHAT")
                    {
                        // 处理CHAT逻辑
                        ShowDialogueBubble(content);
                        AddDialogue($"顾客：{content}");
                        assignedWaiter.AddMemory($"顾客对你说：{content}");
                        orderDialogueRound++;

                        if (/*currentRound < maxChatRounds &&*/ assignedWaiter != null)
                        {
                            yield return new WaitForSeconds(1.5f / TimeManager.Instance.timeScale); //显示对话，也不能太长了。

                            string waiterReply = "";
                            yield return StartCoroutine(AzureOpenAIManager.Instance.GenerateWaiterTakingOrderReply(
                                this, assignedWaiter, content, (response) =>
                                {
                                    waiterReply = response;
                                }));

                            assignedWaiter.ShowDialogueBubble(waiterReply);
                            assignedWaiter.AddMemory($"你对顾客说：{waiterReply}");
                            AddDialogue($"服务员：{waiterReply}");
                            lastMessage = waiterReply;

                            yield return new WaitForSeconds(1.5f / TimeManager.Instance.timeScale);
                        }
                    }
                    else if (action == "EXIT")
                    {
                        ShowDialogueBubble(content);
                        AddDialogue($"顾客：{content}");
                        assignedWaiter.AddMemory($"顾客说：{content}并离开。");
                        isBeingServed = false;
                        currentState = CustomerState.Leaving;
                        Debug.Log("顾客{customerName}进入离开分支");
                        yield break;
                    }
                    else if (action == "ANGER")
                    {
                        ShowDialogueBubble(content);
                        AddDialogue($"顾客：{content}");
                        isBeingServed = false;
                        Debug.Log($"顾客{customerName}进入叫经理分支");
                        stateBeforeEmergency = currentState;  // 确保这里正确设置
                        currentState = CustomerState.Emergency;
                        assignedWaiter.AddMemory($"顾客非常生气，说：{content}");
                        yield break;
                    }
                    else
                    {
                        Debug.LogWarning($"未知动作或格式不正确: {customerResponse}");
                    }
                }
                else
                {
                    Debug.LogWarning($"响应格式不正确，部分数量不足: {customerResponse}");
                }
            }
            else
            {
                Debug.LogWarning($"顾客决策响应格式不正确: {customerResponse}");
            }
        }

        // 达到最大对话轮次后自动转向点餐，取消，在prompt中强调。
        // yield return StartCoroutine(ChooseDish());
    }


    private IEnumerator PerformCallingWaiterDecision()
    {
        // 构建思考提示
        string prompt = $@"顾客{customerName}正在等待上菜。
已等待时间：{waitTime_order:F0}分钟
性格：{personality}
背景：{story}
喜爱菜品：{string.Join(",", favoriteDishes)}
以往用餐记录：{string.Join("\n", memoryList)}
对话记录：{string.Join("\n", dialogueHistory)}
已等待时间:{waitTime_order}
请根据你的性格和当前情况决定下一步行动：
1. 如果决定叫服务员，回复：CALL|叫服务员的内容
2. 如果决定继续等待，回复：WAIT|继续等待的理由
3. 如果决定离开，回复：EXIT|离开时说的话
4. 如果决定叫经理来，回复：ANGER|叫经理说的话
例如：""ANGER|经理给我出来？我叫了三次服务员上菜都没人理我！""
请确保决策符合你的性格：
- 积极性格：可能更耐心，语气更友好
- 消极性格：可能更不耐烦，语气更直接
- 普通性格：中性、理性";

        bool responseReceived = false;
        string aiResponse = "";

        yield return AzureOpenAIManager.Instance.GetCustomerResponse(this, prompt, (response) =>
        {
            aiResponse = response;
            responseReceived = true;
        });

        while (!responseReceived)
            yield return null;

        // 解析决策
        if (aiResponse.Contains("|"))
        {
            string[] parts = aiResponse.Split('|');
            string action = parts[0].Trim();
            string content = parts[1].Trim();

            if (action == "CALL")
            {
                // 叫服务员
                ShowDialogueBubble(content);
                AddDialogue($"顾客：{content}");
                AddMemory($"叫了服务员：{content}");

                // 触发服务员响应
                if (assignedWaiter != null)
                {
                    string waiterResponse = "";
                    yield return StartCoroutine(AzureOpenAIManager.Instance.GenerateWaiterResponseToCall(
                        NPCData.CreateSafe(assignedWaiter), this, content, (response) =>
                        {
                            waiterResponse = response;
                        }));

                    // 显示服务员的回应
                    assignedWaiter.ShowDialogueBubble(waiterResponse);
                    AddDialogue($"服务员：{waiterResponse}");

                    // 等待一段时间让玩家看到对话
                    yield return new WaitForSeconds(2f / TimeManager.Instance.timeScale);

                    // 服务员可能会采取行动（如去厨房催菜）
                }
            }
            else if (action == "WAIT")
            {
                // 继续等待
                ShowDialogueBubble(content);
                AddDialogue($"顾客：{content}");
                AddMemory($"决定继续等待：{content}");
            }
            else if (action == "EXIT")
            {
                // 离开餐厅
                ShowDialogueBubble(content);
                AddDialogue($"顾客：{content}");
                AddMemory($"决定不等了离开饭店：{content}");

                // 生成差评
                satisfaction = 0;
                string comment = "";
                int rating = 0;
                yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerReviewFoodDelay(
                    this, waitTime, waitTime_order, averageWaitTime_queue, averageWaitTime_order, orderedFood,
                    (responseComment, responseRating) =>
                    {
                        comment = responseComment;
                        rating = responseRating;
                    }));
                RestaurantManager.Instance.AddReview(customerName, comment, rating, waitTime, waitTime_order, orderedFood);

                // 更新状态
                currentState = CustomerState.Leaving;
                RestaurantManager.UnregisterCustomer(this);
            }
            else if (action == "ANGER")
            {

                // 生成生气的差评
                satisfaction = 0;
                string comment = "";
                int rating = 0;
                yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerReviewAnger(
                    this, waitTime, waitTime_order, averageWaitTime_queue, averageWaitTime_order, orderedFood, PreviousEvent,
                    (responseComment, responseRating) =>
                    {
                        comment = responseComment;
                        rating = responseRating;
                    }));
                RestaurantManager.Instance.AddReview(customerName, comment, rating, waitTime, waitTime_order, orderedFood);

                // 更新状态
                currentState = CustomerState.Leaving; //需要改成Emergency
                RestaurantManager.UnregisterCustomer(this);
            }
        }
        else
        {
            // 如果响应格式不正确，默认继续等待
            Debug.LogWarning($"顾客决策响应格式不正确: {aiResponse}");
            AddMemory($"继续等待上菜（默认决策）");
        }
    }
  
    #endregion

    #region 辅助方法

    public void ForceLeaveRestaurant()
    {
        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
        }
        if (currentBubble != null)
        {
            Destroy(currentBubble);
            currentBubble = null;
        }
        // 停止所有正在进行的协程
        StopAllCoroutines();
        // 确保清除入口占用状态
        if (RestaurantManager.GetCustomerAtEntrance() == this)
        {
            RestaurantManager.SetCustomerAtEntrance(false);
        }
        // 释放餐桌
        if (assignedTable != null)
        {
            RestaurantManager.UnregisterCustomer(this);
            RestaurantManager.FreeTable(assignedTable);
            RestaurantManager.CancelOrdersForTable(assignedTable);
        }
        // 确保从 RestaurantManager 中注销
        assignedTable = null;
        RestaurantManager.UnregisterCustomer(this);
        // 注意：不要在这里销毁对象，因为 ClearAllCustomers 会处理
    }

    private int GetMenuPrice(string food) //需要修改
    {
        if (RestaurantManager.menuItems != null)
        {
            foreach (var item in menuItems)
            {
                // 使用模糊匹配，因为food可能包含额外文本
                if (food.Contains(item.name))
                    return item.price;
            }
        }
        return 35; // 默认价格
    }
    void OnTimeScaleChanged(float newTimeScale)
    {
        moveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
    }
    public IEnumerator MoveToPosition(Vector3 position)
    {
        while (!isDestroyed && Vector3.Distance(transform.position, position) > destinationThreshold)
        {
            if (this == null || gameObject == null)
            {
                yield break;
            }
            Vector3 direction = (position - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;

            HandleAnimation(direction);
            // 到达后停止移动动画

            yield return null;
        }
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
        }
    }

    
    private void AddMemory(string content)
    {
        string timestamp = TimeManager.Instance.GetCurrentTime();
        string formattedMemory = $"[{timestamp}] {content}";
        memoryList.Add(formattedMemory);

        if (memoryList.Count > MAX_MEMORY_COUNT)
        {
            memoryList.RemoveAt(0);
        }
    }

    private void AddDialogue(string dialogue)
    {
        string timestamp = TimeManager.Instance.GetCurrentTime();
        dialogueHistory.Add($"[{timestamp}] {dialogue}");
    }

    #endregion

    #region 公共接口

    //排队相关：


    public void AssignTable(Transform table)
    {
        assignedTable = table;
        AddMemory($"被分配到餐桌");
        AddDialogue("服务员：请跟我来，这是您的座位");
    }

    public void ReceiveService(string serviceType, string waiterName)
    {
        AddDialogue($"服务员{waiterName}：{serviceType}");
        serviceQuality += 10;
        satisfaction += 5;
        AddMemory($"接受了{waiterName}的{serviceType}服务");
    }

    public bool IsWaitingForService()
    {
        return currentState == CustomerState.Entering ||
               currentState == CustomerState.Seating;
    }

    public float GetSatisfaction() => satisfaction;
    public string GetOrderedFood() => orderedFood;
    public CustomerState GetCurrentState() => currentState;

    #endregion


    #region 排队位置管理
    private Vector3 targetQueuePosition;
    private Coroutine moveToQueueCoroutine;

    public void SetQueueTargetPosition(Vector3 position)
    {
        targetQueuePosition = position;

        // 如果已经在移动中，停止之前的移动协程
        if (moveToQueueCoroutine != null)
        {
            StopCoroutine(moveToQueueCoroutine);
        }

        // 启动新的移动协程
        moveToQueueCoroutine = StartCoroutine(MoveToQueuePosition());
    }

    private IEnumerator MoveToQueuePosition()
    {
        // 只有在排队状态下才移动
        if (currentState != CustomerState.Queuing)
        {
            yield break;
        }

        // 使用现有的MoveToPosition方法移动到目标位置
        yield return StartCoroutine(MoveToPosition(targetQueuePosition));

        // 到达后停止移动动画
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
        }

        moveToQueueCoroutine = null;
    }
    #endregion


    #region UI和对话泡泡
    void ShowDialogueBubble(string text)
    {
        if (this == null || gameObject == null)
        {
            return;
        }
        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
            bubbleCoroutine = null;
        }

        if (currentBubble == null)
        {
            if (dialogueBubblePrefab == null || transform == null)
            {
                return;
            }
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
            nameText.text = customerName;
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
        if (this != null && gameObject != null)
        {
            HideDialogueBubble();
        }

    }
    void HideDialogueBubble()
    {
        if (this == null || gameObject == null)
        {
            return;
        }
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
    // 添加UI初始化方法
    void InitializeUI()
    {
        // 实例化头顶UI
        if (CustomerEmotionPrefab != null)
        {
            GameObject overheadUI = Instantiate(CustomerEmotionPrefab, transform);
            CustomerOverheadUI overheadComponent = overheadUI.GetComponent<CustomerOverheadUI>();
            if (overheadComponent != null)
            {
                overheadComponent.customer = this;
            }
        }

        // 实例化信息UI
        if (CustomerInformationPrefab != null)
        {
            InformationUI = Instantiate(CustomerInformationPrefab, transform);
            CustomerInformationUI infoComponent = InformationUI.GetComponent<CustomerInformationUI>();
            if (infoComponent != null)
            {
                infoComponent.customer = this;
                infoComponent.HideUI(); // 默认隐藏信息UI
            }
        }
    }
    // 更新满意度UI
    public void UpdateSatisfactionUI(float oldValue, float newValue)
    {
        if (InformationUI != null)
        {
            CustomerInformationUI infoUI = InformationUI.GetComponent<CustomerInformationUI>();
            if (infoUI != null)
            {
                infoUI.UpdateSatisfaction(oldValue, newValue);
            }
        }
    }

    // 更新状态UI
    public void UpdateStatusUI()
    {
        if (InformationUI != null)
        {
            CustomerInformationUI infoUI = InformationUI.GetComponent<CustomerInformationUI>();
            if (infoUI != null)
            {
                infoUI.UpdateStatusText();
            }
        }
    }

    // 点击显示/隐藏信息UI
    public void ToggleInformationUI()
    {
        if (InformationUI != null)
        {
            CustomerInformationUI infoUI = InformationUI.GetComponent<CustomerInformationUI>();
            if (infoUI != null)
            {
                infoUI.ToggleUI();
            }
        }
    }

    #endregion


    #region 服务员绑定
    public NPCBehavior assignedWaiter { get; private set; } = null;
    public bool isBeingServed { get; private set; } = false;

    public bool IsBeingServed()
    {
        return isBeingServed && assignedWaiter != null;
    }

    public bool NeedsOrdering()
    {
        return currentState == CustomerState.Ordering && !isBeingServed;
    }

    public bool NeedsGreeting()
    {
        return currentState == CustomerState.Entering && !isBeingServed;
    }

    public void AssignWaiter(NPCBehavior waiter)
    {
        assignedWaiter = waiter;
        isBeingServed = true;
        Debug.Log($"[{customerName}] 被分配给服务员{waiter.npcName}");
    }

    public void ReleaseWaiter()
    {
        if (assignedWaiter != null)
        {
            Debug.Log($"[{customerName}] 释放服务员{assignedWaiter.npcName}");
            assignedWaiter = null;
        }
        isBeingServed = false;
    }
    #endregion

    #region 玩家交互

    [Header("玩家交互设置")]
    public float playerInteractionRange = 3f;
    private bool playerInRange = false;
    private bool waitingForPlayerResponse = false;
    void CheckPlayerDistance()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            playerInRange = distance <= playerInteractionRange;

            // 如果玩家刚刚进入范围，向玩家抱怨
            if (playerInRange && !waitingForPlayerResponse)
            {
                StartCoroutine(ComplainToPlayer());
            }
        }
        else
        {
            playerInRange = false;
        }
    }
    // 向玩家抱怨
    IEnumerator ComplainToPlayer()
    {
        string complaint = "";
        yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerComplaint(
            this, PreviousEvent, (response) =>
            {
                complaint = response;
            }));

        ShowDialogueBubble(complaint);
        AddDialogue($"顾客：{complaint}");
        AddMemory($"向经理抱怨：{complaint}");
    }



    #endregion










}

// 顾客活动数据结构
public struct CustomerActivity
{
    public string action;
    public int duration;
    public CustomerNPC.CustomerState nextState;
    public string details;
    public string reason;

    public CustomerActivity(string act, int dur, CustomerNPC.CustomerState next, string det, string rea)
    {
        action = act;
        duration = dur;
        nextState = next;
        details = det;
        reason = rea;
    }


    
}

// JSON解析数据结构
[Serializable]
public class CustomerDecisionData
{
    public string action;
    public int duration;
    public string nextState;
    public string details;
    public string reason;
}
