using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using static CustomerNPC;
using static GameModeManager;
using static NPCBehavior;
public class RestaurantManager : MonoBehaviour
{
    [System.Serializable]
    public class HistoricalData
    {
        public int day;
        public float averageRating;
        public int income;
    }
    [Header("Game Settings")] //根据setting设置难度
    public GameSettings gameSettings; // 将GlobalGameSettings资源拖拽到这里


    [System.Serializable] //菜品类
    public class MenuItem
    {
        public string name;
        public int price;
        public string attribute;
        public int popularity;
    }

    [System.Serializable]
    #region 评价管理
    public class CustomerReview
    {
        public string customerName;
        public string comment;
        public int rating;
        public float waitTime;
        public float orderWaitTime;
        public string orderTook;

        public CustomerReview(string name, string comment, int rating, float waitTime = 0, float orderWaitTime = 0, string orderTook = null)
        {
            this.customerName = name;
            this.comment = comment;
            this.rating = rating;
            this.waitTime = waitTime;
            this.orderWaitTime = orderWaitTime;
            this.orderTook = orderTook;
        }
    }


    public int todayIncome = 0; // 当日收入

    [Header("单条的评价窗口")]
    [Header("评价显示设置")]
    [SerializeField] private float reviewDisplayDuration = 100f; // 单条评价显示时间
    [SerializeField] private float reviewSpacing = 0.1f; // 评价之间的间隔时间（改为正数
    [SerializeField] private GameObject reviewUIPrefab; // 评价UI预制体
    [SerializeField] private Transform reviewsContainer; // 评价容器（ScrollView的Content）
    [SerializeField] private GameObject reviewsPanel; // 评价总面板
    [SerializeField] private GameObject continueButton; // 添加对继续按钮的引用，避免使用Find



    private List<CustomerReview> dailyReviews = new List<CustomerReview>(); // 当日所有评价
    // 添加折线图预制件
    public GameObject chartUIPrefab;

    // 添加历史数据列表
    public List<HistoricalData> historicalDataList = new List<HistoricalData>();


    private bool isBusinessEnded = false;
    public void AddReview(string customerName, string comment, int rating, float waitTime = 0, float orderWaitTime = 0, string orderTook = null)
    {
        CustomerReview review = new CustomerReview(customerName, comment, rating, waitTime, orderWaitTime, orderTook);
        dailyReviews.Add(review);
        Debug.Log($"[评价] {customerName}: {comment} (评分: {rating}/10)");
    }
    public IEnumerator ShowAllReviews()
    {
        IsShowingReviews = true;
        // 显示评价面板
        if (reviewsPanel != null)
        {
            reviewsPanel.SetActive(true);
        }

        // 清空现有评价UI
        foreach (Transform child in reviewsContainer)
        {
            Destroy(child.gameObject);
        }

        // 计算平均评分
        float averageRating = CalculateAverageRating();

        // 保存到历史数据
        HistoricalData todayData = new HistoricalData
        {
            day = historicalDataList.Count + 1, // 假设天数从1开始
            averageRating = averageRating,
            income = todayIncome
        };
        historicalDataList.Add(todayData);

        // 如果没有评价，显示无评价提示和总结
        if (dailyReviews.Count == 0)
        {
            GameObject noReviews = Instantiate(reviewUIPrefab, reviewsContainer);
            // 设置无评价提示文本
            SetupNoReviewText(noReviews);

            // 显示总结信息 - 这里使用不同的变量名
            GameObject noReviewSummary = Instantiate(reviewUIPrefab, reviewsContainer);
            SetupSummaryText(noReviewSummary, averageRating, todayIncome);
            yield return StartCoroutine(FadeInReview(noReviewSummary));
            yield return new WaitForSecondsRealtime(0.3f);

            // 等待玩家点击继续
            yield return WaitForPlayerContinue();

            // 隐藏评价面板
            if (reviewsPanel != null)
            {
                reviewsPanel.SetActive(false);
            }
            IsShowingReviews = false;
            yield break;
        }

        // 创建所有评价UI对象但不立即显示
        List<GameObject> reviewUIList = new List<GameObject>();
        foreach (CustomerReview review in dailyReviews)
        {
            GameObject reviewUI = Instantiate(reviewUIPrefab, reviewsContainer);
            SetupReviewText(reviewUI, review);

            // 初始设置为透明
            CanvasGroup canvasGroup = reviewUI.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = reviewUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            reviewUIList.Add(reviewUI);
        }

        // 逐一淡入显示每个评价
        foreach (GameObject reviewUI in reviewUIList)
        {
            yield return StartCoroutine(FadeInReview(reviewUI));
            yield return new WaitForSecondsRealtime(0.3f); // 短暂间隔
        }

        // 显示总结信息 - 这里使用summaryUI
        GameObject summaryUI = Instantiate(reviewUIPrefab, reviewsContainer);
        SetupSummaryText(summaryUI, averageRating, todayIncome);
        yield return StartCoroutine(FadeInReview(summaryUI));
        yield return new WaitForSecondsRealtime(0.3f);

        // 如果有历史数据，显示折线图
        if (historicalDataList.Count > 1)
        {
            GameObject chartUI = Instantiate(chartUIPrefab, reviewsContainer);
            SetupChart(chartUI, historicalDataList);
            yield return StartCoroutine(FadeInReview(chartUI));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        // 等待玩家点击继续
        yield return WaitForPlayerContinue();

        // 只有在玩家点击继续后才隐藏评价面板
        if (reviewsPanel != null)
        {
            reviewsPanel.SetActive(false);
        }

        IsShowingReviews = false;
    }

    // 计算平均评分的方法
    private float CalculateAverageRating()
    {
        if (dailyReviews.Count == 0) return 0;

        int totalRating = 0;
        foreach (CustomerReview review in dailyReviews)
        {
            totalRating += review.rating;
        }
        return (float)totalRating / dailyReviews.Count;
    }
    // 设置总结文本的方法
    private void SetupSummaryText(GameObject summaryUI, float averageRating, int income)
    {
        TMP_Text[] texts = summaryUI.GetComponentsInChildren<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text.name.Contains("Comment"))
            {
                text.text = $"今日平均评分: {averageRating:F1}/10, 收入: {income}元";
            }
            else if (text.name.Contains("Rating"))
            {
                text.text = ""; // 清空评分文本
            }
            else if (text.name.Contains("Name"))
            {
                text.text = "每日总结";
            }
            else if (text.name.Contains("Dish"))
            {
                text.text = ""; // 清空菜品文本
            }
            else if (text.name.Contains("WaitTime"))
            {
                text.text = ""; // 清空等待时间文本
            }
        }
    }
    // 提取设置无评价文本的方法
    private void SetupNoReviewText(GameObject noReviews)
    {
        TMP_Text[] texts = noReviews.GetComponentsInChildren<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text.name.Contains("Comment"))
            {
                text.text = "今日暂无顾客评价";
            }
            else if (text.name.Contains("Rating"))
            {
                text.text = "";
            }
            else if (text.name.Contains("Name"))
            {
                text.text = "系统提示";
            }
            
        }
    }
    private void SetupChart(GameObject chartUI, List<HistoricalData> historicalData)
    {
        LineChart lineChart = chartUI.GetComponent<LineChart>();
        if (lineChart == null) return;

        // 准备数据
        List<float> ratings = new List<float>();
        List<int> incomes = new List<int>();
        List<int> days = new List<int>();

        foreach (HistoricalData data in historicalData)
        {
            ratings.Add(data.averageRating);
            incomes.Add(data.income);
            days.Add(data.day);
        }

        // 设置折线图数据
        lineChart.SetData(days, ratings, incomes);
    }
    // 提取设置评价文本的方法
    private void SetupReviewText(GameObject reviewUI, CustomerReview review)
    {
        TMP_Text[] texts = reviewUI.GetComponentsInChildren<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text.name.Contains("Comment"))
            {
                text.text = review.comment;
            }
            else if (text.name.Contains("Rating"))
            {
                text.text = $"评分: {review.rating}/10";

                // 根据评分设置颜色
                if (review.rating >= 8) text.color = Color.green;
                else if (review.rating >= 5) text.color = Color.yellow;
                else text.color = Color.red;
            }
            else if (text.name.Contains("Name"))
            {
                text.text = review.customerName;
            }
            else if (text.name.Contains("Dish"))
            {
                text.text = $"菜品：{review.orderTook}";
            }
            else if (text.name.Contains("WaitTime") && review.waitTime > 0)
            {
                text.text = $"等待: {review.waitTime:F1}分钟";
            }
        }
    }

    // 淡入效果协程
    private IEnumerator FadeInReview(GameObject reviewUI)
    {
        CanvasGroup canvasGroup = reviewUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = reviewUI.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0;
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // 使用 unscaledDeltaTime 因为时间可能被暂停
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 1;
    }
    // 等待玩家点击继续的方法
    private IEnumerator WaitForPlayerContinue()
    {
        // 使用序列化字段引用继续按钮，避免使用Find
        if (continueButton != null)
        {
            continueButton.SetActive(true);

            // 等待按钮点击
            bool clicked = false;
            UnityEngine.UI.Button button = continueButton.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                // 使用Lambda表达式时，需要先移除所有监听器，避免重复添加
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => clicked = true);
            }

            yield return new WaitUntil(() => clicked);
            continueButton.SetActive(false);
        }
        else
        {
            // 如果没有按钮，等待几秒后自动继续
            yield return new WaitForSecondsRealtime(3f);
        }
    }

    // 添加一个重置评价的方法，在每天开始时调用
    public void ResetDailyReviews()
    {
        dailyReviews.Clear();
    }
    public void ResetDay()
    {
        // 重置每日数据
        ResetDailyReviews();
        usedCustomerIds.Clear();
        todayIncome = 0;

        // 重置订单系统
        pendingOrders.Clear();
        preparingOrders.Clear();
        readyOrders.Clear();
        nextOrderId = 1;

        // 隐藏UI
        if (reviewsPanel != null && reviewsPanel.activeSelf)
        {
            reviewsPanel.SetActive(false);
        }

        // 清理临时对象
        CleanupTemporaryObjects();
    }

    public bool IsShowingReviews { get; private set; }

 

    #endregion


    #region 顾客生成和顾客数据库
    [Header("顾客生成配置")]
    public GameObject customerPrefab; // 顾客预制体，后面改成列表，生成长相不同的顾客
    public int maxQueueSize = 6; // 最大排队人数
    public float customerSpawnInterval = 10f; // 顾客生成间隔（秒）
    public Transform spawnPosition; // 顾客生成位置
    public bool testNegatives = false; //是否测试负面顾客
    [Header("预设顾客管理")]
    private List<CustomerJsonData> customerDatabase = new List<CustomerJsonData>(); // 顾客json
    private List<int> usedCustomerIds = new List<int>(); // 当日已使用的顾客ID
    [SerializeField] private List<AnimatorOverrideController> npcAppearances = new List<AnimatorOverrideController>(); //外观animator
    void LoadCustomerDatabase() //从Json读取
    {
        string jsonPath = "";
        if (testNegatives)
        {
            jsonPath = Path.Combine(Application.streamingAssetsPath, "Customers/negativecustomers.json");
        }
        else
        {
            jsonPath = Path.Combine(Application.streamingAssetsPath, "Customers/customers.json");
        }
            
        //Debug.Log($"路径是：{jsonPath}");
        if (File.Exists(jsonPath))
        {
            string jsonString = File.ReadAllText(jsonPath);
            // 读取Json数组
            CustomerJsonData[] customersArray = JsonHelper.FromJson<CustomerJsonData>(jsonString); ;
            customerDatabase.AddRange(customersArray);
            Debug.Log($"[RestaurantManager] 成功加载 {customerDatabase.Count} 个预设顾客");
        }
        else
        {
            Debug.LogError($"[RestaurantManager] 找不到顾客数据文件: {jsonPath}");
        }
    }

    // 获取一个未使用的顾客数据
    CustomerJsonData GetRandomUnusedCustomerData()
    {
        // 获取可用的顾客ID列表
        List<int> availableIds = new List<int>();
        for (int i = 0; i < customerDatabase.Count; i++)
        {
            if (!usedCustomerIds.Contains(i))
            {
                availableIds.Add(i);
            }
        }

        if (availableIds.Count == 0)
        {
            Debug.LogWarning("[RestaurantManager] 所有预设顾客都已使用");
            return null;
        }

        // 随机选择一个
        int randomIndex = availableIds[UnityEngine.Random.Range(0, availableIds.Count)];
        usedCustomerIds.Add(randomIndex);

        return customerDatabase[randomIndex];
    }
    void SpawnNewCustomer()
    {
        if (customerPrefab == null || spawnPosition == null) return;

        GameObject newCustomerObj = Instantiate(customerPrefab, spawnPosition.position, Quaternion.identity);
        CustomerNPC newCustomer = newCustomerObj.GetComponent<CustomerNPC>();

        if (npcAppearances.Count > 0)
        {
            Animator customerAnimator = newCustomerObj.GetComponent<Animator>();
            if (customerAnimator != null)
            {
                int randomIndex = Random.Range(0, npcAppearances.Count);
                customerAnimator.runtimeAnimatorController = npcAppearances[randomIndex];
            }
        }

        if (newCustomer != null)
        {
            // 尝试获取预设顾客数据
            CustomerJsonData presetData = GetRandomUnusedCustomerData();

            if (presetData != null)
            {
                // 直接赋值给CustomerNPC
                newCustomer.customerName = presetData.name;
                newCustomer.customerName_eng = presetData.name_eng;
                newCustomer.baseMood = presetData.baseMood;
                newCustomer.story = presetData.story;
                newCustomer.story_eng = presetData.story_eng;
                newCustomer.returnIndex = presetData.returnIndex;
                newCustomer.favoriteDishes = new List<string>(presetData.favDishes);
                newCustomer.personality = presetData.personalityType;
                newCustomer.personality_eng = presetData.personalityType_eng;
                // 基于背景故事和心情设置其他属性
                newCustomer.satisfaction = presetData.baseMood;
                // 设置顾客ID
                for (int i = 0; i < customerDatabase.Count; i++)
                {
                    if (customerDatabase[i].name == presetData.name)
                    {
                        newCustomer.customerId = i;
                        break;
                    }
                }
            }
            else
            {
                // 使用原有的随机生成逻辑
                GenerateCustomerName();
            }

            // 设置位置信息
            newCustomer.queuePosition = queuePosition;
            newCustomer.entrancePosition = entrancePosition;
            newCustomer.exitPosition = exitPosition;
            newCustomer.cashierPosition = cashierPosition;

            // 计算排队位置间隔
            Vector3 spacedQueuePosition = CalculateQueuePositionWithSpacing();
            AddToQueue(newCustomer);
            newCustomer.SetQueueTargetPosition(spacedQueuePosition);
            Debug.Log($"[RestaurantManager] 新顾客 {newCustomer.customerName} 加入排队，位置: {spacedQueuePosition}");
        }
    }

    // 计算带间隔的排队位置
    Vector3 CalculateQueuePositionWithSpacing()
    {
        // 获取当前排队中的顾客数量（不包括即将加入的新顾客）
        int currentQueueSize = customerQueue.Count;

        // 定义排队间隔距离
        float spacing = 1.5f; // 可以根据需要调整这个值

        // 计算排队方向向量（从queuePosition指向spawnPosition）
        Vector3 queueDirection = (spawnPosition.position - queuePosition.position).normalized;

        // 计算新顾客应该在的位置
        // 第一个顾客在queuePosition，后续顾客依次向spawnPosition方向排列
        Vector3 targetPosition = queuePosition.position + queueDirection * (currentQueueSize * spacing);

        return targetPosition;
    }
    int GetCurrentQueueSize()
    {
        // 这里需要根据你的实际实现来获取当前排队中的顾客数量
        // 假设你有一个列表或数组来跟踪排队中的顾客
        return customerQueue.Count; // 直接使用customerQueue的数量
    }


    #endregion


    #region 预设服务员
    private List<WaiterJsonData> waiterDatabase = new List<WaiterJsonData>(); // 服务员json
    private List<int> usedWaiterIds = new List<int>(); // 当日已使用的服务员ID
    [Header("服务员配置")]
    public Transform[] waiterRestingPositions; // 在Inspector中设置所有服务员的休息位置
    public GameObject waiterPrefab; // 服务员预制体
    public Transform kitchenPosition; //厨房位置
    public Transform cleanerPosition; //清洁、倒垃圾位置
    private List<NPCBehavior> activeWaiters = new List<NPCBehavior>();
    [SerializeField] private List<AnimatorOverrideController> waiterAppearances = new List<AnimatorOverrideController>(); //外观animator
    private void InitializeWaiters()
    {
        // 清除现有的活跃服务员
        activeWaiters.Clear();

        // 获取场景中所有NPCBehavior组件
        NPCBehavior[] allNPCs = FindObjectsOfType<NPCBehavior>();

        // 筛选出服务员
        foreach (NPCBehavior npc in allNPCs)
        {
            if (npc.occupation == "服务员")
            {
                activeWaiters.Add(npc);
                // 初始禁用所有服务员
                npc.gameObject.SetActive(false);
            }
        }

        Debug.Log($"[RestaurantManager] 找到 {activeWaiters.Count} 个服务员");
    }



    public List<WaiterJsonData> GetWaiterDatabase()
    {
        return new List<WaiterJsonData>(waiterDatabase);
    }
    void LoadWaiterDatabase() //从Json读取基础服务员，然后玩家就可以排布选择了。 
    {
        string jsonPath = "";

        //jsonPath = Path.Combine(Application.streamingAssetsPath, "Waiters/negativewaiters.json");
        jsonPath = Path.Combine(Application.streamingAssetsPath, "Waiters/waiters.json");
        if (File.Exists(jsonPath))
        {
            string jsonString = File.ReadAllText(jsonPath);
            // 读取Json数组
            WaiterJsonData[] waiterArray = JsonHelper.FromJson<WaiterJsonData>(jsonString);
            //Debug.Log(jsonString);
            waiterDatabase.AddRange(waiterArray);
            Debug.Log($"[RestaurantManager] 成功加载 {waiterDatabase.Count} 个预设服务员");
        }
        else
        {
            Debug.LogError($"[RestaurantManager] 找不到服务员数据文件: {jsonPath}");
        }
    }

    // 设置今日选中的服务员
    private List<WaiterJsonData> todayWaiters = new List<WaiterJsonData>();
    public void SetSelectedWaiters(List<WaiterJsonData> selected)
    {
        todayWaiters = new List<WaiterJsonData>(selected);
        Debug.Log($"今日服务员: {string.Join(", ", todayWaiters.Select(w => w.name))}");

        // 清除现有的活跃服务员
        foreach (NPCBehavior waiter in activeWaiters)
        {
            if (waiter != null)
                Destroy(waiter.gameObject);
        }
        activeWaiters.Clear();

        // 生成新的服务员
        for (int i = 0; i < todayWaiters.Count; i++)
        {
            WaiterJsonData waiterData = todayWaiters[i];

            // 检查是否有足够的休息位置
            if (i >= waiterRestingPositions.Length)
            {
                Debug.LogError($"[RestaurantManager] 没有足够的休息位置给服务员 {waiterData.name}。需要 {i + 1} 个位置，但只有 {waiterRestingPositions.Length} 个可用。");
                continue;
            }

            // 获取休息位置
            Transform restingPos = waiterRestingPositions[i];
            if (restingPos == null)
            {
                Debug.LogError($"[RestaurantManager] 休息位置 {i} 为空，无法创建服务员 {waiterData.name}");
                continue;
            }

            // 实例化服务员预制体在休息位置
            GameObject waiterObj = Instantiate(waiterPrefab, restingPos.position, restingPos.rotation);
            NPCBehavior waiter = waiterObj.GetComponent<NPCBehavior>();

            if (waiterAppearances.Count > 0) //为其生成动画外观
            {
                Animator customerAnimator = waiterObj.GetComponent<Animator>();
                if (customerAnimator != null)
                {
                    int randomIndex = Random.Range(0, waiterAppearances.Count);
                    customerAnimator.runtimeAnimatorController = waiterAppearances[randomIndex];
                }
            }
            if (waiter == null)
            {
                Debug.LogError($"[RestaurantManager] 服务员预制体上没有 NPCBehavior 组件");
                Destroy(waiterObj);
                continue;
            }

            // 设置服务员属性
            waiter.npcName = waiterData.name;
            waiter.npcName_eng = waiterData.name_eng;
            waiter.energy = waiterData.Energy;
            waiter.mood = waiterData.Mood;
            waiter.backgroundStory = waiterData.story;
            waiter.backgroundStory_eng = waiterData.story_eng;
            waiter.personality = waiterData.personalityType;
            waiter.personality_eng = waiterData.personalityType_eng;
            // 分配位置引用
            waiter.entrancePosition = entrancePosition;
            waiter.kitchenPosition = kitchenPosition;
            waiter.cleanerPosition = cleanerPosition;
            waiter.restingPosition = restingPos; // 使用实际的休息位置Transform

            // 添加到活跃列表
            activeWaiters.Add(waiter);

            Debug.Log($"[RestaurantManager] 生成服务员: {waiter.npcName} 在位置 {restingPos.name}");
            Debug.Log($"[RestaurantManager] 位置分配 - 入口: {waiter.entrancePosition.name}, 厨房: {waiter.kitchenPosition.name}, " +
                     $"休息: {waiter.restingPosition.name}, 清洁: {waiter.cleanerPosition.name}");
        }
    }

    // 临时位置对象列表
    private List<GameObject> temporaryPositionObjects = new List<GameObject>();

    // 清理临时位置对象的方法
    public void CleanupTemporaryObjects()
    {
        foreach (GameObject obj in temporaryPositionObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        temporaryPositionObjects.Clear();
    }


    // 获取今日服务员
    public List<WaiterJsonData> GetTodayWaiters()
    {
        return todayWaiters;
    }


    #endregion

    // 排队管理
    private static Queue<CustomerNPC> customerQueue = new Queue<CustomerNPC>();
    private static bool isProcessingQueue = false;
    private float lastSpawnTime = 0f;
    private static RestaurantManager instance;
    public static RestaurantManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<RestaurantManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("RestaurantManager");
                    instance = go.AddComponent<RestaurantManager>();
                }
            }
            return instance;
        }
    }
    public static int ActiveCustomerCount//检测剩余顾客数量，准备结束营业
    {
        get { return activeCustomers.Count; }
    }

    public static int QueueCustomerCount
    {
        get { return customerQueue.Count; }
    }

    // 全局状态
    private static bool customerAtEntrance = false;
    public static NPCBehavior greetingWaiter = null;
    private static List<CustomerNPC> activeCustomers = new List<CustomerNPC>();
    private static Dictionary<Transform, bool> tableOccupancy = new Dictionary<Transform, bool>();

    [Header("餐厅配置")]
    public Transform[] availableTables; // 在Inspector中设置所有餐桌
    public Transform entrancePosition;
    public Transform queuePosition;
    public Transform exitPosition;
    public Transform cashierPosition;
    public string RestaurantMenu; //json直接读进来易于理解
    public static MenuItem[] menuItems; //每道菜，用于匹配
    private static bool isMenuLoaded = false;


    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }

        // 初始化餐桌状态
        InitializeTables();
        // 初始化菜单
        string jsonPath = Path.Combine(Application.streamingAssetsPath, "Menu/menu.json");
        if (File.Exists(jsonPath))
        {
            RestaurantMenu = File.ReadAllText(jsonPath);
            menuItems = JsonHelper.FromJson<MenuItem>(RestaurantMenu);
            if (menuItems != null)
            {
                isMenuLoaded = true;
                Debug.Log($"成功加载 {menuItems.Length} 个菜单项");
            }
            else
            {
                Debug.LogError("菜单解析失败");
            }
        }
    }
    void Start()
    {
        initializeDifficulty();
        LoadCustomerDatabase();//载入顾客预设json
        LoadWaiterDatabase();// 载入服务员预设
        // 确保评价面板初始时是禁用的
        if (reviewsPanel != null && reviewsPanel.activeSelf)
        {
            reviewsPanel.SetActive(false);
        }

        // 确保继续按钮初始时是禁用的
        if (continueButton != null && continueButton.activeSelf)
        {
            continueButton.SetActive(false);
        }

        RegisterInspectorChefs();
        StartCoroutine(CustomerSpawnRoutine());
        StartCoroutine(QueueProcessingRoutine());
        StartCoroutine(OrderManagementRoutine()); // 订单管理协程
        
    }
    void initializeDifficulty()
    {
        if (gameSettings == null)
        {
            Debug.LogError("RestaurantManager: GameSettings未分配!");
            return;
        }
        customerSpawnInterval = gameSettings.customerSpawnInterval; //排队间隔
        testNegatives = gameSettings.isNegativeCustomer;

    }

    void InitializeTables()
    {
        if (availableTables != null)
        {
            foreach (var table in availableTables)
            {
                if (table != null && !tableOccupancy.ContainsKey(table))
                {
                    tableOccupancy[table] = false;
                }
            }
        }
    }
    public void OnGameStateChanged(GameState previousState, GameState newState)
    {
        switch (newState)
        {
            case GameState.DayStart:
                // 重置每日数据
                ResetDailyReviews();
                usedCustomerIds.Clear();
                break;

            case GameState.DayEnd:
                // 停止生成新顾客
                StopAllCoroutines();
                break;

            case GameState.Paused:
                // 暂停所有协程或动画
                break;
        }
    }
    public void ResetCustomerQueue()
    {
        customerQueue.Clear();
        usedCustomerIds.Clear();
        Debug.Log("[RestaurantManager] 顾客队列已重置");
    }
    #region 顾客入口管理
    public static bool IsCustomerAtEntrance() => customerAtEntrance;

    public static bool IsAnyoneGreeting() => greetingWaiter != null;

    public static void SetCustomerAtEntrance(bool value)
    {
        customerAtEntrance = value;
        if (!value)
        {
            greetingWaiter = null; // 顾客已被迎接，清除标志
        }

        Debug.Log($"[RestaurantManager] 入口顾客状态: {value}");
    }

    public static void SetGreetingWaiter(NPCBehavior waiter)
    {
        greetingWaiter = waiter;
        Debug.Log($"[RestaurantManager] {waiter.npcName} 正在迎接顾客");
    }

    #endregion

    #region 顾客管理
    public static void RegisterCustomer(CustomerNPC customer)
    {
        if (!activeCustomers.Contains(customer))
        {
            activeCustomers.Add(customer);
            Debug.Log($"[RestaurantManager] 顾客 {customer.customerName} 已注册，当前顾客数: {activeCustomers.Count}");
        }
    }

    public static void UnregisterCustomer(CustomerNPC customer)
    {
        if (activeCustomers.Contains(customer))
        {
            activeCustomers.Remove(customer);
            Debug.Log($"[RestaurantManager] 顾客 {customer.customerName} 已离开，剩余顾客数: {activeCustomers.Count}");
        }
    }
    public static void ClearAllCustomers()
    {
        Debug.Log("清除所有顾客");

        // 清除活跃顾客
        foreach (var customer in activeCustomers)
        {
            if (customer != null)
            {
                Destroy(customer.gameObject);
            }
        }
        activeCustomers.Clear();

        // 清除排队顾客
        customerQueue.Clear();

        Debug.Log($"清除后 - 活跃顾客: {activeCustomers.Count}, 排队顾客: {customerQueue.Count}");
    }
    public static List<CustomerNPC> GetActiveCustomers()
    {
        return new List<CustomerNPC>(activeCustomers);
    }

    public static CustomerNPC GetCustomerWaitingForService()
    {
        foreach (var customer in activeCustomers)
        {
            if (customer != null && customer.IsWaitingForService())
            {
                return customer;
            }
        }
        return null;
    }
    public static CustomerNPC GetCustomerNeedingOrder()
    {
        foreach (var customer in activeCustomers)
        {
            if (customer != null && customer.NeedsOrdering())
            {
                return customer;
            }
        }
        return null;
    }
    
    public static bool TryAssignCustomerToWaiter(NPCBehavior waiter, CustomerNPC customer)
    {
        if (customer != null && !customer.IsBeingServed())
        {
            customer.AssignWaiter(waiter);
            return true;
        }
        return false;
    }


    public static CustomerNPC GetCustomerAssignedToWaiter(NPCBehavior waiter)
    {
        foreach (var customer in activeCustomers)
        {
            if (customer != null && customer.assignedWaiter == waiter)
                return customer;
        }
        return null;
    }


    public static List<Order> GetReadyOrders()
    {
        return new List<Order>(readyOrders);
    }

    public static Order GetReadyOrderForWaiter(NPCBehavior waiter)
    {
        return readyOrders.FirstOrDefault(o => o.waiter == waiter);
    }

    public static bool HasReadyOrders()
    {
        return readyOrders.Count > 0;
    }

    #endregion
    #region 顾客生成和排队管理
    private static readonly object greetingLock = new object();

    public static bool TryReserveGreeting(NPCBehavior waiter)
    {
        lock(greetingLock)
        {
            // 检查是否有顾客在入口且没有其他服务员在迎接
            var entranceCustomer = GetCustomerAtEntrance();
            if (entranceCustomer == null || entranceCustomer.IsBeingServed() || greetingWaiter != null)
            {
                return false;
            }

            greetingWaiter = waiter;
            entranceCustomer.AssignWaiter(waiter);
            return true;
        }
    }

    // 清除迎接状态
    public static void ClearGreetingWaiter(NPCBehavior waiter)
    {
        if (greetingWaiter == waiter)
        {
            Debug.Log($"[RestaurantManager] 清除 {waiter.npcName} 的迎接任务");
            greetingWaiter = null;
        }
    }


    IEnumerator CustomerSpawnRoutine()
    {
        yield return new WaitForSeconds(2f); // 初始延迟

        while (true)
        {
            // 检查是否应该生成新顾客
            //if (customerQueue.Count < maxQueueSize &&
            //    Time.time - lastSpawnTime >= customerSpawnInterval)
            if (customerQueue.Count < maxQueueSize &&
                TimeManager.Instance.GetTotalMinutes()- lastSpawnTime >= customerSpawnInterval)
            {
                if(TimeManager.Instance.CurrentHour<20)
                    SpawnNewCustomer();
                lastSpawnTime = TimeManager.Instance.GetTotalMinutes();
            }

            yield return new WaitForSeconds(1f);
        }
    }

    string GenerateCustomerName() //默认的随即生成，已被顾客卡池取代
    {
        string[] surnames = { "张", "王", "李", "赵", "刘", "陈", "杨", "黄" };
        string[] names = { "先生", "女士", "小姐", "大哥", "阿姨" };
        return surnames[UnityEngine.Random.Range(0, surnames.Length)] +
               names[UnityEngine.Random.Range(0, names.Length)];
    }

    public static void AddToQueue(CustomerNPC customer)
    {
        customerQueue.Enqueue(customer);
        customer.QueingNumber = customerQueue.Count;
        Debug.Log($"[RestaurantManager] {customer.customerName} 排队号: {customer.QueingNumber}");
    }

    IEnumerator QueueProcessingRoutine()
    {
        while (true)
        {
            // 检查是否有顾客在排队且没有顾客在入口
            if (customerQueue.Count > 0 && !customerAtEntrance && !isProcessingQueue)
            {
                isProcessingQueue = true;

                // 获取队首顾客（排队号为1的顾客）
                CustomerNPC nextCustomer = customerQueue.Peek();

                // 检查是否有空桌
                if (GetAvailableTableCount() > 0)
                {
                    // 出队
                    customerQueue.Dequeue();

                    // 让顾客进入餐厅
                    nextCustomer.NotifyEnterRestaurant();

                    // 更新后续顾客的排队号
                    UpdateQueueNumbers();

                    Debug.Log($"[RestaurantManager] 通知 {nextCustomer.customerName} 进入餐厅");
                }
                else
                {
                    Debug.Log("[RestaurantManager] 没有空桌，顾客继续等待");
                }

                isProcessingQueue = false;
            }
            // 额外检查：如果入口标志被设置但没有找到入口的顾客，清除标志
            if (customerAtEntrance && GetCustomerAtEntrance() == null)
            {
                Debug.Log("[RestaurantManager] 检测到入口标志异常，清除标志");
                customerAtEntrance = false;
            }

            yield return new WaitForSeconds(0.1f); // 每2秒检查一次
        }
    }

    public static CustomerNPC GetCustomerAtEntrance()
    {
        foreach (var customer in activeCustomers)
        {
            if (customer != null && customer.GetCurrentState() == CustomerNPC.CustomerState.Entering)
            {
                return customer;
            }
        }
        return null;
    }

    static void UpdateQueueNumbers()
    {
        int number = 1;
        foreach (var customer in customerQueue)
        {
            customer.QueingNumber = number++;
        }
    }

    public static int GetQueueLength()
    {
        return customerQueue.Count;
    }

    public static List<CustomerNPC> GetQueuedCustomers()
    {
        return new List<CustomerNPC>(customerQueue);
    }
    #endregion
    #region 餐桌管理
    public static Transform AssignTable()
    {
        foreach (var kvp in tableOccupancy)
        {
            if (!kvp.Value) // 找到空闲餐桌
            {
                tableOccupancy[kvp.Key] = true;
                Debug.Log($"[RestaurantManager] 分配餐桌: {kvp.Key.name}");
                return kvp.Key;
            }
        }

        Debug.LogWarning("[RestaurantManager] 没有空闲餐桌");
        return null;
    }

    public static void FreeTable(Transform table)
    {
        if (table == null)
        {
            Debug.LogWarning("[RestaurantManager] 尝试释放空餐桌");
            return;
        }
        if (tableOccupancy.ContainsKey(table))
        {
            tableOccupancy[table] = false;
            Debug.Log($"[RestaurantManager] 餐桌 {table.name} 已释放");
        }
        else
        {
            Debug.LogError($"[RestaurantManager] 餐桌 {table.name} 不在管理列表中");
        }
    }

    public static bool IsTableAvailable(Transform table)
    {
        return tableOccupancy.ContainsKey(table) && !tableOccupancy[table];
    }

    public static int GetAvailableTableCount()
    {
        return tableOccupancy.Count(kvp => !kvp.Value);
    }

    #endregion

    #region 小费管理
    public static void AddTipToNearestWaiter(Vector3 customerPosition, int tipAmount)
    {
        NPCBehavior nearestWaiter = null;
        float minDistance = float.MaxValue;

        // 查找所有服务员NPC
        NPCBehavior[] allWaiters = FindObjectsOfType<NPCBehavior>();

        foreach (var waiter in allWaiters)
        {
            if (waiter.occupation == "服务员")
            {
                float distance = Vector3.Distance(customerPosition, waiter.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestWaiter = waiter;
                }
            }
        }

        if (nearestWaiter != null)
        {
            nearestWaiter.tips += tipAmount;
            nearestWaiter.AddMemory($"获得{tipAmount}元小费，总小费：{nearestWaiter.tips}元");
            //nearestWaiter.statusUI?.UpdateTexts();
            Debug.Log($"[RestaurantManager] {nearestWaiter.npcName} 获得 {tipAmount} 元小费");
        }
    }

    #endregion

    #region 餐厅状态查询
    /*public static RestaurantStatus GetRestaurantStatus()
    {
        return new RestaurantStatus
        {
            totalTables = tableOccupancy.Count,
            occupiedTables = tableOccupancy.Count(kvp => kvp.Value),
            customerCount = activeCustomers.Count,
            hasCustomerAtEntrance = customerAtEntrance,
            greetingWaiterName = greetingWaiter?.npcName ?? "无"
        };
    }*/

    #endregion

    #region 厨师管理
    [Header("厨师配置")]
    public ChefBehavior[] chefs; // 在Inspector中拖拽分配厨师

    // 厨师注册系统（动态注册）
    private static List<ChefBehavior> registeredChefs = new List<ChefBehavior>();

    void RegisterInspectorChefs()
    {
        if(chefs !=null)
        {
            foreach(var chef in chefs)
            {
                if(chef!=null)
                {
                    RegisterChef(chef);
                }
            }
        }
    }
    // 注册厨师
    public static void RegisterChef(ChefBehavior chef)
    {
        if (!registeredChefs.Contains(chef))
        {
            registeredChefs.Add(chef);
            Debug.Log($"[RestaurantManager] 厨师 {chef.npcName} 已注册，当前厨师数: {registeredChefs.Count}");
        }
    }

    // 注销厨师
    public static void UnregisterChef(ChefBehavior chef)
    {
        if (registeredChefs.Contains(chef))
        {
            registeredChefs.Remove(chef);
            Debug.Log($"[RestaurantManager] 厨师 {chef.npcName} 已注销，剩余厨师数: {registeredChefs.Count}");
        }
    }

    // 获取所有厨师
    public static List<ChefBehavior> GetAllChefs()
    {
        return new List<ChefBehavior>(registeredChefs);
    }

    // 获取空闲厨师
    public static ChefBehavior GetAvailableChef()
    {
        foreach (var chef in registeredChefs)
        {
            Debug.Log($"是否有订单：{chef.hasOrder}，当前状态：{chef.chefState}");
            if (chef != null && !chef.hasOrder && chef.chefState == ChefBehavior.ChefState.Idle)
            {
                return chef;
            }
        }
        return null;
    }

    // 获取厨师统计信息
    public static string GetChefStats()
    {
        int totalChefs = registeredChefs.Count;
        int busyChefs = registeredChefs.Count(c => c != null && c.hasOrder);
        int idleChefs = totalChefs - busyChefs;

        return $"厨师总数: {totalChefs}, 忙碌: {busyChefs}, 空闲: {idleChefs}";
    }
    #endregion



    #region 订单管理
    [System.Serializable]
    public class Order
    {
        public int orderId;
        public CustomerNPC customer;
        public NPCBehavior waiter;
        public Transform table;
        public string dishName;
        public float orderTime;
        public bool isBeingPrepared;
        public bool isReady;
        public ChefBehavior assignedChef; // 添加分配的厨师

        public Order(int id, CustomerNPC cust, NPCBehavior wait, Transform tab, string dish)
        {
            orderId = id;
            customer = cust;
            waiter = wait;
            table = tab;
            dishName = dish;
            orderTime = Time.time;
            isBeingPrepared = false;
            isReady = false;
            assignedChef = null;
        }
    }


    private static List<Order> pendingOrders = new List<Order>();
    private static List<Order> preparingOrders = new List<Order>();
    private static List<Order> readyOrders = new List<Order>();
    private static int nextOrderId = 1;
    IEnumerator OrderManagementRoutine()
    {
        while (true)
        {
            // 每5秒检查一次是否有待分配的订单
            TryAssignPendingOrders();

            // 输出统计信息（可选，用于调试）
            if (pendingOrders.Count > 0 || preparingOrders.Count > 0 || readyOrders.Count > 0)
            {
                Debug.Log($"[RestaurantManager] {GetOrderStats()} | {GetChefStats()}");
            }

            yield return new WaitForSeconds(5f);
        }
    }

    // 添加新订单
    public static void AddOrder(CustomerNPC customer, NPCBehavior waiter, Transform table, string dishName)
    {
        Order newOrder = new Order(nextOrderId++, customer, waiter, table, dishName);
        pendingOrders.Add(newOrder);

        Debug.Log($"[RestaurantManager] 新订单 #{newOrder.orderId}: {dishName} - 桌子 {table.name}");
        Debug.Log($"[RestaurantManager] 当前待处理订单数: {pendingOrders.Count}");

        // 立即尝试分配给空闲厨师
        AssignOrderToChef(newOrder);
    }
    private static bool AssignOrderToChef(Order order)
    {
        ChefBehavior availableChef = GetAvailableChef();

        if (availableChef != null)
        {
            // 移除待处理列表，加入制作中列表
            pendingOrders.Remove(order);
            preparingOrders.Add(order);

            // 分配给厨师
            order.assignedChef = availableChef;
            order.isBeingPrepared = true;

            // 通知厨师开始工作
            availableChef.AssignOrder(order);

            Debug.Log($"[RestaurantManager] 订单 #{order.orderId} 已分配给厨师 {availableChef.npcName}");
            return true;
        }
        else
        {
            Debug.Log($"[RestaurantManager] 暂无空闲厨师，订单 #{order.orderId} 等待中");
            return false;
        }
    }
    // 尝试分配待处理的订单（定期调用）
    public static void TryAssignPendingOrders()
    {
        for (int i = pendingOrders.Count - 1; i >= 0; i--)
        {
            if (AssignOrderToChef(pendingOrders[i]))
            {
                // 成功分配一个订单后可以继续，或者break只分配一个
                break;
            }
        }
    }
    // 通知厨师有新订单
    /*private static void NotifyChefOfNewOrder()
    {
        ChefBehavior[] chefs = FindObjectsOfType<ChefBehavior>();

        foreach (var chef in chefs)
        {
            if (!chef.hasOrder) // 找到空闲的厨师
            {
                chef.SetOrderStatus(true);
                Debug.Log($"[RestaurantManager] 通知厨师 {chef.npcName} 处理新订单");
                break; // 只通知一个厨师
            }
        }
    }*/

    // 厨师开始处理订单
    public static Order GetNextPendingOrder()
    {
        if (pendingOrders.Count > 0)
        {
            Order order = pendingOrders[0];
            pendingOrders.RemoveAt(0);
            preparingOrders.Add(order);
            order.isBeingPrepared = true;

            Debug.Log($"[RestaurantManager] 订单 #{order.orderId} 开始制作");
            return order;
        }
        return null;
    }
    // 尝试给特定厨师分配订单的方法
    public static Order TryAssignOrderToChef(ChefBehavior chef)
    {
        if (pendingOrders.Count > 0 && chef != null && !chef.hasOrder && chef.chefState == ChefBehavior.ChefState.Idle)
        {
            Order order = pendingOrders[0];

            // 移除待处理列表，加入制作中列表
            pendingOrders.RemoveAt(0);
            preparingOrders.Add(order);

            // 分配给厨师
            order.assignedChef = chef;
            order.isBeingPrepared = true;

            // 通知厨师开始工作
            chef.AssignOrder(order);

            Debug.Log($"[RestaurantManager] 订单 #{order.orderId} 已分配给空闲厨师 {chef.npcName}");
            return order;
        }
        return null;
    }

    // 检查是否有待处理订单的方法
    public static bool HasPendingOrdersForChef()
    {
        return pendingOrders.Count > 0;
    }

    // 获取待处理订单数量
    public static int GetPendingOrderCount()
    {
        return pendingOrders.Count;
    }
    // 厨师完成订单
    public static void CompleteOrder(Order completedOrder)
    {
        if (completedOrder != null && preparingOrders.Contains(completedOrder))
        {
            preparingOrders.Remove(completedOrder);
            readyOrders.Add(completedOrder);
            completedOrder.isReady = true;

            Debug.Log($"[RestaurantManager] 订单 #{completedOrder.orderId} 制作完成，等待上菜");

            // 通知对应的服务员
            if (completedOrder.waiter != null)
            {
                completedOrder.waiter.NotifyOrderReady(completedOrder);
            }
            else
            {
                Debug.LogWarning($"[RestaurantManager] 订单 #{completedOrder.orderId} 没有指定服务员");
                // 如果没有指定服务员，找一个空闲的服务员
                NPCBehavior[] waiters = FindObjectsOfType<NPCBehavior>();
                foreach (var waiter in waiters)
                {
                    if (waiter.occupation == "服务员" && waiter.waiterState == NPCBehavior.WaiterState.Idle)
                    {
                        waiter.NotifyOrderReady(completedOrder);
                        break;
                    }
                }
            }
        }
    }

    // 添加方法获取需要上菜的订单
    public static List<Order> GetOrdersReadyForServing()
    {
        return new List<Order>(readyOrders);
    }

    // 服务员取走订单
    public static void PickupOrder(int orderId)
    {
        Order order = readyOrders.FirstOrDefault(o => o.orderId == orderId);
        if (order != null)
        {
            readyOrders.Remove(order);
            // 同时从其他列表中移除（防止内存泄漏）
            pendingOrders.Remove(order);
            preparingOrders.Remove(order);
            Debug.Log($"[RestaurantManager] 订单 #{orderId} 已被服务员取走并完全清理");
        }
    }
    public static void CancelOrdersForTable(Transform table)
    {
        if (table == null)
        {
            Debug.LogWarning("[RestaurantManager] 尝试取消空餐桌的订单");
            return;
        }
        int cancelledCount = 0;

        cancelledCount +=pendingOrders.RemoveAll(order => order.table == table);
        cancelledCount +=preparingOrders.RemoveAll(order => order.table == table);
        cancelledCount += readyOrders.RemoveAll(order => order.table == table);
        if (cancelledCount > 0)
        {
            Debug.Log($"[RestaurantManager] 已取消餐桌 {table.name} 的 {cancelledCount} 个订单");
        }
    }
    // 检查是否有待处理的订单
    public static bool HasPendingOrders()
    {
        return pendingOrders.Count > 0;
    }

    // 获取订单统计
    public static string GetOrderStats()
    {
        return $"待处理: {pendingOrders.Count}, 制作中: {preparingOrders.Count}, 待取餐: {readyOrders.Count}";
    }
    #endregion

    #region 餐厅信息整理
    public string GetRestaurantStatusSummary()
    {
        StringBuilder sb = new StringBuilder();
        // 餐桌状态
        int occupiedTables = tableOccupancy.Count(kvp => kvp.Value);
        int totalTables = tableOccupancy.Count;
        sb.AppendLine($"当前餐桌占用: {occupiedTables}/{totalTables}");
        // 排队情况
        sb.AppendLine($"排队人数: {customerQueue.Count}");

        // 订单状态
        sb.AppendLine($"待处理订单: {pendingOrders.Count}");
        sb.AppendLine($"制作中订单: {preparingOrders.Count}");
        sb.AppendLine($"待上菜订单: {readyOrders.Count}");

        // 服务员状态
        int idleWaiters = activeWaiters.Count(w => w.waiterState == WaiterState.Idle);
        sb.AppendLine($"空闲服务员: {idleWaiters}/{activeWaiters.Count}");

        // 厨师状态
        int busyChefs = registeredChefs.Count(c => c.hasOrder);
        sb.AppendLine($"忙碌厨师: {busyChefs}/{registeredChefs.Count}"); 

        // 其他顾客状态
        var eatingCustomers = activeCustomers.Count(c => c.currentState == CustomerState.Eating);
        var waitingCustomers = activeCustomers.Count(c => c.currentState == CustomerState.Seating);
        sb.AppendLine($"用餐中顾客: {eatingCustomers}");
        sb.AppendLine($"等待上菜顾客: {waitingCustomers}");

        return sb.ToString();
    }
    #endregion
    #region 顾客感知

    private static string GetRelativeTablePosition(Vector3 observerPos, Vector3 targetPos)
    {
        Vector3 direction = (targetPos - observerPos).normalized;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return direction.x > 0 ? "右边桌" : "左边桌";
        }
        else
        {
            return direction.z > 0 ? "前面桌" : "后面桌";
        }
    }

    public static List<string> GetNearbyCustomerStatus(CustomerNPC observer, float perceptionRange = 5f)
    {
        List<string> statusInfo = new List<string>();
        if (observer == null || observer.assignedTable == null)
        {
            return statusInfo;
        }
        Vector3 observerPosition = observer.assignedTable.position;
        foreach (var customer in activeCustomers)
        {
            // 跳过自己
            if (customer == observer || customer == null || customer.assignedTable == null)
                continue;
            // 检查距离是否在感知范围内
            float distance = Vector3.Distance(observerPosition, customer.assignedTable.position);
            if (distance <= perceptionRange)
            {
                string tablePos = GetRelativeTablePosition(observerPosition, customer.assignedTable.position);
                switch (customer.currentState)
                {
                    case CustomerState.Eating:
                        if (!string.IsNullOrEmpty(customer.orderedFood))
                        {
                            statusInfo.Add($"{tablePos}的顾客正在享用{customer.orderedFood}");
                        }
                        else
                        {
                            statusInfo.Add($"{tablePos}的顾客正在用餐");
                        }
                        break;
                    case CustomerState.Seating:
                        statusInfo.Add($"{tablePos}的顾客正在等待上菜");
                        break;
                    case CustomerState.Ordering:
                        statusInfo.Add($"{tablePos}的顾客正在点菜");
                        break;
                }
            }
        }
        Debug.Log($"{statusInfo}");
        return statusInfo;
    }

    public static string GetDiningEnvironmentSummary(CustomerNPC observer, float perceptionRange = 5f)
    {
        if (observer == null || observer.assignedTable == null)
        {
            return "";
        }
        Vector3 observerPosition = observer.assignedTable.position;
        int nearbyCount = 0;
        int eatingCount = 0;
        int waitingCount = 0;
        foreach (var customer in activeCustomers)
        {
            if (customer == observer || customer == null || customer.assignedTable == null)
                continue;
            float distance = Vector3.Distance(observerPosition, customer.assignedTable.position);
            if (distance <= perceptionRange)
            {
                nearbyCount++;
                if (customer.currentState == CustomerState.Eating)
                    eatingCount++;
                else if (customer.currentState == CustomerState.Seating)
                    waitingCount++;
            }
        }
        if (nearbyCount == 0)
        {
            return "周围没有其他顾客";
        }
        return $"周围有{nearbyCount}位顾客，其中{eatingCount}位正在用餐，{waitingCount}位在等待上菜";
    }
    #endregion


}
