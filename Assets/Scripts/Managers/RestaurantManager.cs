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
    [Header("Game Settings")] //����setting�����Ѷ�
    public GameSettings gameSettings; // ��GlobalGameSettings��Դ��ק������


    [System.Serializable] //��Ʒ��
    public class MenuItem
    {
        public string name;
        public int price;
        public string attribute;
        public int popularity;
    }

    [System.Serializable]
    #region ���۹���
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


    public int todayIncome = 0; // ��������

    [Header("���������۴���")]
    [Header("������ʾ����")]
    [SerializeField] private float reviewDisplayDuration = 100f; // ����������ʾʱ��
    [SerializeField] private float reviewSpacing = 0.1f; // ����֮��ļ��ʱ�䣨��Ϊ����
    [SerializeField] private GameObject reviewUIPrefab; // ����UIԤ����
    [SerializeField] private Transform reviewsContainer; // ����������ScrollView��Content��
    [SerializeField] private GameObject reviewsPanel; // ���������
    [SerializeField] private GameObject continueButton; // ��ӶԼ�����ť�����ã�����ʹ��Find



    private List<CustomerReview> dailyReviews = new List<CustomerReview>(); // ������������
    // �������ͼԤ�Ƽ�
    public GameObject chartUIPrefab;

    // �����ʷ�����б�
    public List<HistoricalData> historicalDataList = new List<HistoricalData>();


    private bool isBusinessEnded = false;
    public void AddReview(string customerName, string comment, int rating, float waitTime = 0, float orderWaitTime = 0, string orderTook = null)
    {
        CustomerReview review = new CustomerReview(customerName, comment, rating, waitTime, orderWaitTime, orderTook);
        dailyReviews.Add(review);
        Debug.Log($"[����] {customerName}: {comment} (����: {rating}/10)");
    }
    public IEnumerator ShowAllReviews()
    {
        IsShowingReviews = true;
        // ��ʾ�������
        if (reviewsPanel != null)
        {
            reviewsPanel.SetActive(true);
        }

        // �����������UI
        foreach (Transform child in reviewsContainer)
        {
            Destroy(child.gameObject);
        }

        // ����ƽ������
        float averageRating = CalculateAverageRating();

        // ���浽��ʷ����
        HistoricalData todayData = new HistoricalData
        {
            day = historicalDataList.Count + 1, // ����������1��ʼ
            averageRating = averageRating,
            income = todayIncome
        };
        historicalDataList.Add(todayData);

        // ���û�����ۣ���ʾ��������ʾ���ܽ�
        if (dailyReviews.Count == 0)
        {
            GameObject noReviews = Instantiate(reviewUIPrefab, reviewsContainer);
            // ������������ʾ�ı�
            SetupNoReviewText(noReviews);

            // ��ʾ�ܽ���Ϣ - ����ʹ�ò�ͬ�ı�����
            GameObject noReviewSummary = Instantiate(reviewUIPrefab, reviewsContainer);
            SetupSummaryText(noReviewSummary, averageRating, todayIncome);
            yield return StartCoroutine(FadeInReview(noReviewSummary));
            yield return new WaitForSecondsRealtime(0.3f);

            // �ȴ���ҵ������
            yield return WaitForPlayerContinue();

            // �����������
            if (reviewsPanel != null)
            {
                reviewsPanel.SetActive(false);
            }
            IsShowingReviews = false;
            yield break;
        }

        // ������������UI���󵫲�������ʾ
        List<GameObject> reviewUIList = new List<GameObject>();
        foreach (CustomerReview review in dailyReviews)
        {
            GameObject reviewUI = Instantiate(reviewUIPrefab, reviewsContainer);
            SetupReviewText(reviewUI, review);

            // ��ʼ����Ϊ͸��
            CanvasGroup canvasGroup = reviewUI.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = reviewUI.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            reviewUIList.Add(reviewUI);
        }

        // ��һ������ʾÿ������
        foreach (GameObject reviewUI in reviewUIList)
        {
            yield return StartCoroutine(FadeInReview(reviewUI));
            yield return new WaitForSecondsRealtime(0.3f); // ���ݼ��
        }

        // ��ʾ�ܽ���Ϣ - ����ʹ��summaryUI
        GameObject summaryUI = Instantiate(reviewUIPrefab, reviewsContainer);
        SetupSummaryText(summaryUI, averageRating, todayIncome);
        yield return StartCoroutine(FadeInReview(summaryUI));
        yield return new WaitForSecondsRealtime(0.3f);

        // �������ʷ���ݣ���ʾ����ͼ
        if (historicalDataList.Count > 1)
        {
            GameObject chartUI = Instantiate(chartUIPrefab, reviewsContainer);
            SetupChart(chartUI, historicalDataList);
            yield return StartCoroutine(FadeInReview(chartUI));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        // �ȴ���ҵ������
        yield return WaitForPlayerContinue();

        // ֻ������ҵ��������������������
        if (reviewsPanel != null)
        {
            reviewsPanel.SetActive(false);
        }

        IsShowingReviews = false;
    }

    // ����ƽ�����ֵķ���
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
    // �����ܽ��ı��ķ���
    private void SetupSummaryText(GameObject summaryUI, float averageRating, int income)
    {
        TMP_Text[] texts = summaryUI.GetComponentsInChildren<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text.name.Contains("Comment"))
            {
                text.text = $"����ƽ������: {averageRating:F1}/10, ����: {income}Ԫ";
            }
            else if (text.name.Contains("Rating"))
            {
                text.text = ""; // ��������ı�
            }
            else if (text.name.Contains("Name"))
            {
                text.text = "ÿ���ܽ�";
            }
            else if (text.name.Contains("Dish"))
            {
                text.text = ""; // ��ղ�Ʒ�ı�
            }
            else if (text.name.Contains("WaitTime"))
            {
                text.text = ""; // ��յȴ�ʱ���ı�
            }
        }
    }
    // ��ȡ�����������ı��ķ���
    private void SetupNoReviewText(GameObject noReviews)
    {
        TMP_Text[] texts = noReviews.GetComponentsInChildren<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text.name.Contains("Comment"))
            {
                text.text = "�������޹˿�����";
            }
            else if (text.name.Contains("Rating"))
            {
                text.text = "";
            }
            else if (text.name.Contains("Name"))
            {
                text.text = "ϵͳ��ʾ";
            }
            
        }
    }
    private void SetupChart(GameObject chartUI, List<HistoricalData> historicalData)
    {
        LineChart lineChart = chartUI.GetComponent<LineChart>();
        if (lineChart == null) return;

        // ׼������
        List<float> ratings = new List<float>();
        List<int> incomes = new List<int>();
        List<int> days = new List<int>();

        foreach (HistoricalData data in historicalData)
        {
            ratings.Add(data.averageRating);
            incomes.Add(data.income);
            days.Add(data.day);
        }

        // ��������ͼ����
        lineChart.SetData(days, ratings, incomes);
    }
    // ��ȡ���������ı��ķ���
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
                text.text = $"����: {review.rating}/10";

                // ��������������ɫ
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
                text.text = $"��Ʒ��{review.orderTook}";
            }
            else if (text.name.Contains("WaitTime") && review.waitTime > 0)
            {
                text.text = $"�ȴ�: {review.waitTime:F1}����";
            }
        }
    }

    // ����Ч��Э��
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
            elapsed += Time.unscaledDeltaTime; // ʹ�� unscaledDeltaTime ��Ϊʱ����ܱ���ͣ
            canvasGroup.alpha = Mathf.Lerp(0, 1, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 1;
    }
    // �ȴ���ҵ�������ķ���
    private IEnumerator WaitForPlayerContinue()
    {
        // ʹ�����л��ֶ����ü�����ť������ʹ��Find
        if (continueButton != null)
        {
            continueButton.SetActive(true);

            // �ȴ���ť���
            bool clicked = false;
            UnityEngine.UI.Button button = continueButton.GetComponent<UnityEngine.UI.Button>();
            if (button != null)
            {
                // ʹ��Lambda���ʽʱ����Ҫ���Ƴ����м������������ظ����
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => clicked = true);
            }

            yield return new WaitUntil(() => clicked);
            continueButton.SetActive(false);
        }
        else
        {
            // ���û�а�ť���ȴ�������Զ�����
            yield return new WaitForSecondsRealtime(3f);
        }
    }

    // ���һ���������۵ķ�������ÿ�쿪ʼʱ����
    public void ResetDailyReviews()
    {
        dailyReviews.Clear();
    }
    public void ResetDay()
    {
        // ����ÿ������
        ResetDailyReviews();
        usedCustomerIds.Clear();
        todayIncome = 0;

        // ���ö���ϵͳ
        pendingOrders.Clear();
        preparingOrders.Clear();
        readyOrders.Clear();
        nextOrderId = 1;

        // ����UI
        if (reviewsPanel != null && reviewsPanel.activeSelf)
        {
            reviewsPanel.SetActive(false);
        }

        // ������ʱ����
        CleanupTemporaryObjects();
    }

    public bool IsShowingReviews { get; private set; }

 

    #endregion


    #region �˿����ɺ͹˿����ݿ�
    [Header("�˿���������")]
    public GameObject customerPrefab; // �˿�Ԥ���壬����ĳ��б����ɳ��಻ͬ�Ĺ˿�
    public int maxQueueSize = 6; // ����Ŷ�����
    public float customerSpawnInterval = 10f; // �˿����ɼ�����룩
    public Transform spawnPosition; // �˿�����λ��
    public bool testNegatives = false; //�Ƿ���Ը���˿�
    [Header("Ԥ��˿͹���")]
    private List<CustomerJsonData> customerDatabase = new List<CustomerJsonData>(); // �˿�json
    private List<int> usedCustomerIds = new List<int>(); // ������ʹ�õĹ˿�ID
    [SerializeField] private List<AnimatorOverrideController> npcAppearances = new List<AnimatorOverrideController>(); //���animator
    void LoadCustomerDatabase() //��Json��ȡ
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
            
        //Debug.Log($"·���ǣ�{jsonPath}");
        if (File.Exists(jsonPath))
        {
            string jsonString = File.ReadAllText(jsonPath);
            // ��ȡJson����
            CustomerJsonData[] customersArray = JsonHelper.FromJson<CustomerJsonData>(jsonString); ;
            customerDatabase.AddRange(customersArray);
            Debug.Log($"[RestaurantManager] �ɹ����� {customerDatabase.Count} ��Ԥ��˿�");
        }
        else
        {
            Debug.LogError($"[RestaurantManager] �Ҳ����˿������ļ�: {jsonPath}");
        }
    }

    // ��ȡһ��δʹ�õĹ˿�����
    CustomerJsonData GetRandomUnusedCustomerData()
    {
        // ��ȡ���õĹ˿�ID�б�
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
            Debug.LogWarning("[RestaurantManager] ����Ԥ��˿Ͷ���ʹ��");
            return null;
        }

        // ���ѡ��һ��
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
            // ���Ի�ȡԤ��˿�����
            CustomerJsonData presetData = GetRandomUnusedCustomerData();

            if (presetData != null)
            {
                // ֱ�Ӹ�ֵ��CustomerNPC
                newCustomer.customerName = presetData.name;
                newCustomer.customerName_eng = presetData.name_eng;
                newCustomer.baseMood = presetData.baseMood;
                newCustomer.story = presetData.story;
                newCustomer.story_eng = presetData.story_eng;
                newCustomer.returnIndex = presetData.returnIndex;
                newCustomer.favoriteDishes = new List<string>(presetData.favDishes);
                newCustomer.personality = presetData.personalityType;
                newCustomer.personality_eng = presetData.personalityType_eng;
                // ���ڱ������º�����������������
                newCustomer.satisfaction = presetData.baseMood;
                // ���ù˿�ID
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
                // ʹ��ԭ�е���������߼�
                GenerateCustomerName();
            }

            // ����λ����Ϣ
            newCustomer.queuePosition = queuePosition;
            newCustomer.entrancePosition = entrancePosition;
            newCustomer.exitPosition = exitPosition;
            newCustomer.cashierPosition = cashierPosition;

            // �����Ŷ�λ�ü��
            Vector3 spacedQueuePosition = CalculateQueuePositionWithSpacing();
            AddToQueue(newCustomer);
            newCustomer.SetQueueTargetPosition(spacedQueuePosition);
            Debug.Log($"[RestaurantManager] �¹˿� {newCustomer.customerName} �����Ŷӣ�λ��: {spacedQueuePosition}");
        }
    }

    // �����������Ŷ�λ��
    Vector3 CalculateQueuePositionWithSpacing()
    {
        // ��ȡ��ǰ�Ŷ��еĹ˿�����������������������¹˿ͣ�
        int currentQueueSize = customerQueue.Count;

        // �����ŶӼ������
        float spacing = 1.5f; // ���Ը�����Ҫ�������ֵ

        // �����Ŷӷ�����������queuePositionָ��spawnPosition��
        Vector3 queueDirection = (spawnPosition.position - queuePosition.position).normalized;

        // �����¹˿�Ӧ���ڵ�λ��
        // ��һ���˿���queuePosition�������˿�������spawnPosition��������
        Vector3 targetPosition = queuePosition.position + queueDirection * (currentQueueSize * spacing);

        return targetPosition;
    }
    int GetCurrentQueueSize()
    {
        // ������Ҫ�������ʵ��ʵ������ȡ��ǰ�Ŷ��еĹ˿�����
        // ��������һ���б�������������Ŷ��еĹ˿�
        return customerQueue.Count; // ֱ��ʹ��customerQueue������
    }


    #endregion


    #region Ԥ�����Ա
    private List<WaiterJsonData> waiterDatabase = new List<WaiterJsonData>(); // ����Աjson
    private List<int> usedWaiterIds = new List<int>(); // ������ʹ�õķ���ԱID
    [Header("����Ա����")]
    public Transform[] waiterRestingPositions; // ��Inspector���������з���Ա����Ϣλ��
    public GameObject waiterPrefab; // ����ԱԤ����
    public Transform kitchenPosition; //����λ��
    public Transform cleanerPosition; //��ࡢ������λ��
    private List<NPCBehavior> activeWaiters = new List<NPCBehavior>();
    [SerializeField] private List<AnimatorOverrideController> waiterAppearances = new List<AnimatorOverrideController>(); //���animator
    private void InitializeWaiters()
    {
        // ������еĻ�Ծ����Ա
        activeWaiters.Clear();

        // ��ȡ����������NPCBehavior���
        NPCBehavior[] allNPCs = FindObjectsOfType<NPCBehavior>();

        // ɸѡ������Ա
        foreach (NPCBehavior npc in allNPCs)
        {
            if (npc.occupation == "����Ա")
            {
                activeWaiters.Add(npc);
                // ��ʼ�������з���Ա
                npc.gameObject.SetActive(false);
            }
        }

        Debug.Log($"[RestaurantManager] �ҵ� {activeWaiters.Count} ������Ա");
    }



    public List<WaiterJsonData> GetWaiterDatabase()
    {
        return new List<WaiterJsonData>(waiterDatabase);
    }
    void LoadWaiterDatabase() //��Json��ȡ��������Ա��Ȼ����ҾͿ����Ų�ѡ���ˡ� 
    {
        string jsonPath = "";

        //jsonPath = Path.Combine(Application.streamingAssetsPath, "Waiters/negativewaiters.json");
        jsonPath = Path.Combine(Application.streamingAssetsPath, "Waiters/waiters.json");
        if (File.Exists(jsonPath))
        {
            string jsonString = File.ReadAllText(jsonPath);
            // ��ȡJson����
            WaiterJsonData[] waiterArray = JsonHelper.FromJson<WaiterJsonData>(jsonString);
            //Debug.Log(jsonString);
            waiterDatabase.AddRange(waiterArray);
            Debug.Log($"[RestaurantManager] �ɹ����� {waiterDatabase.Count} ��Ԥ�����Ա");
        }
        else
        {
            Debug.LogError($"[RestaurantManager] �Ҳ�������Ա�����ļ�: {jsonPath}");
        }
    }

    // ���ý���ѡ�еķ���Ա
    private List<WaiterJsonData> todayWaiters = new List<WaiterJsonData>();
    public void SetSelectedWaiters(List<WaiterJsonData> selected)
    {
        todayWaiters = new List<WaiterJsonData>(selected);
        Debug.Log($"���շ���Ա: {string.Join(", ", todayWaiters.Select(w => w.name))}");

        // ������еĻ�Ծ����Ա
        foreach (NPCBehavior waiter in activeWaiters)
        {
            if (waiter != null)
                Destroy(waiter.gameObject);
        }
        activeWaiters.Clear();

        // �����µķ���Ա
        for (int i = 0; i < todayWaiters.Count; i++)
        {
            WaiterJsonData waiterData = todayWaiters[i];

            // ����Ƿ����㹻����Ϣλ��
            if (i >= waiterRestingPositions.Length)
            {
                Debug.LogError($"[RestaurantManager] û���㹻����Ϣλ�ø�����Ա {waiterData.name}����Ҫ {i + 1} ��λ�ã���ֻ�� {waiterRestingPositions.Length} �����á�");
                continue;
            }

            // ��ȡ��Ϣλ��
            Transform restingPos = waiterRestingPositions[i];
            if (restingPos == null)
            {
                Debug.LogError($"[RestaurantManager] ��Ϣλ�� {i} Ϊ�գ��޷���������Ա {waiterData.name}");
                continue;
            }

            // ʵ��������ԱԤ��������Ϣλ��
            GameObject waiterObj = Instantiate(waiterPrefab, restingPos.position, restingPos.rotation);
            NPCBehavior waiter = waiterObj.GetComponent<NPCBehavior>();

            if (waiterAppearances.Count > 0) //Ϊ�����ɶ������
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
                Debug.LogError($"[RestaurantManager] ����ԱԤ������û�� NPCBehavior ���");
                Destroy(waiterObj);
                continue;
            }

            // ���÷���Ա����
            waiter.npcName = waiterData.name;
            waiter.npcName_eng = waiterData.name_eng;
            waiter.energy = waiterData.Energy;
            waiter.mood = waiterData.Mood;
            waiter.backgroundStory = waiterData.story;
            waiter.backgroundStory_eng = waiterData.story_eng;
            waiter.personality = waiterData.personalityType;
            waiter.personality_eng = waiterData.personalityType_eng;
            // ����λ������
            waiter.entrancePosition = entrancePosition;
            waiter.kitchenPosition = kitchenPosition;
            waiter.cleanerPosition = cleanerPosition;
            waiter.restingPosition = restingPos; // ʹ��ʵ�ʵ���Ϣλ��Transform

            // ��ӵ���Ծ�б�
            activeWaiters.Add(waiter);

            Debug.Log($"[RestaurantManager] ���ɷ���Ա: {waiter.npcName} ��λ�� {restingPos.name}");
            Debug.Log($"[RestaurantManager] λ�÷��� - ���: {waiter.entrancePosition.name}, ����: {waiter.kitchenPosition.name}, " +
                     $"��Ϣ: {waiter.restingPosition.name}, ���: {waiter.cleanerPosition.name}");
        }
    }

    // ��ʱλ�ö����б�
    private List<GameObject> temporaryPositionObjects = new List<GameObject>();

    // ������ʱλ�ö���ķ���
    public void CleanupTemporaryObjects()
    {
        foreach (GameObject obj in temporaryPositionObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        temporaryPositionObjects.Clear();
    }


    // ��ȡ���շ���Ա
    public List<WaiterJsonData> GetTodayWaiters()
    {
        return todayWaiters;
    }


    #endregion

    // �Ŷӹ���
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
    public static int ActiveCustomerCount//���ʣ��˿�������׼������Ӫҵ
    {
        get { return activeCustomers.Count; }
    }

    public static int QueueCustomerCount
    {
        get { return customerQueue.Count; }
    }

    // ȫ��״̬
    private static bool customerAtEntrance = false;
    public static NPCBehavior greetingWaiter = null;
    private static List<CustomerNPC> activeCustomers = new List<CustomerNPC>();
    private static Dictionary<Transform, bool> tableOccupancy = new Dictionary<Transform, bool>();

    [Header("��������")]
    public Transform[] availableTables; // ��Inspector���������в���
    public Transform entrancePosition;
    public Transform queuePosition;
    public Transform exitPosition;
    public Transform cashierPosition;
    public string RestaurantMenu; //jsonֱ�Ӷ������������
    public static MenuItem[] menuItems; //ÿ���ˣ�����ƥ��
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

        // ��ʼ������״̬
        InitializeTables();
        // ��ʼ���˵�
        string jsonPath = Path.Combine(Application.streamingAssetsPath, "Menu/menu.json");
        if (File.Exists(jsonPath))
        {
            RestaurantMenu = File.ReadAllText(jsonPath);
            menuItems = JsonHelper.FromJson<MenuItem>(RestaurantMenu);
            if (menuItems != null)
            {
                isMenuLoaded = true;
                Debug.Log($"�ɹ����� {menuItems.Length} ���˵���");
            }
            else
            {
                Debug.LogError("�˵�����ʧ��");
            }
        }
    }
    void Start()
    {
        initializeDifficulty();
        LoadCustomerDatabase();//����˿�Ԥ��json
        LoadWaiterDatabase();// �������ԱԤ��
        // ȷ����������ʼʱ�ǽ��õ�
        if (reviewsPanel != null && reviewsPanel.activeSelf)
        {
            reviewsPanel.SetActive(false);
        }

        // ȷ��������ť��ʼʱ�ǽ��õ�
        if (continueButton != null && continueButton.activeSelf)
        {
            continueButton.SetActive(false);
        }

        RegisterInspectorChefs();
        StartCoroutine(CustomerSpawnRoutine());
        StartCoroutine(QueueProcessingRoutine());
        StartCoroutine(OrderManagementRoutine()); // ��������Э��
        
    }
    void initializeDifficulty()
    {
        if (gameSettings == null)
        {
            Debug.LogError("RestaurantManager: GameSettingsδ����!");
            return;
        }
        customerSpawnInterval = gameSettings.customerSpawnInterval; //�ŶӼ��
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
                // ����ÿ������
                ResetDailyReviews();
                usedCustomerIds.Clear();
                break;

            case GameState.DayEnd:
                // ֹͣ�����¹˿�
                StopAllCoroutines();
                break;

            case GameState.Paused:
                // ��ͣ����Э�̻򶯻�
                break;
        }
    }
    public void ResetCustomerQueue()
    {
        customerQueue.Clear();
        usedCustomerIds.Clear();
        Debug.Log("[RestaurantManager] �˿Ͷ���������");
    }
    #region �˿���ڹ���
    public static bool IsCustomerAtEntrance() => customerAtEntrance;

    public static bool IsAnyoneGreeting() => greetingWaiter != null;

    public static void SetCustomerAtEntrance(bool value)
    {
        customerAtEntrance = value;
        if (!value)
        {
            greetingWaiter = null; // �˿��ѱ�ӭ�ӣ������־
        }

        Debug.Log($"[RestaurantManager] ��ڹ˿�״̬: {value}");
    }

    public static void SetGreetingWaiter(NPCBehavior waiter)
    {
        greetingWaiter = waiter;
        Debug.Log($"[RestaurantManager] {waiter.npcName} ����ӭ�ӹ˿�");
    }

    #endregion

    #region �˿͹���
    public static void RegisterCustomer(CustomerNPC customer)
    {
        if (!activeCustomers.Contains(customer))
        {
            activeCustomers.Add(customer);
            Debug.Log($"[RestaurantManager] �˿� {customer.customerName} ��ע�ᣬ��ǰ�˿���: {activeCustomers.Count}");
        }
    }

    public static void UnregisterCustomer(CustomerNPC customer)
    {
        if (activeCustomers.Contains(customer))
        {
            activeCustomers.Remove(customer);
            Debug.Log($"[RestaurantManager] �˿� {customer.customerName} ���뿪��ʣ��˿���: {activeCustomers.Count}");
        }
    }
    public static void ClearAllCustomers()
    {
        Debug.Log("������й˿�");

        // �����Ծ�˿�
        foreach (var customer in activeCustomers)
        {
            if (customer != null)
            {
                Destroy(customer.gameObject);
            }
        }
        activeCustomers.Clear();

        // ����Ŷӹ˿�
        customerQueue.Clear();

        Debug.Log($"����� - ��Ծ�˿�: {activeCustomers.Count}, �Ŷӹ˿�: {customerQueue.Count}");
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
    #region �˿����ɺ��Ŷӹ���
    private static readonly object greetingLock = new object();

    public static bool TryReserveGreeting(NPCBehavior waiter)
    {
        lock(greetingLock)
        {
            // ����Ƿ��й˿��������û����������Ա��ӭ��
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

    // ���ӭ��״̬
    public static void ClearGreetingWaiter(NPCBehavior waiter)
    {
        if (greetingWaiter == waiter)
        {
            Debug.Log($"[RestaurantManager] ��� {waiter.npcName} ��ӭ������");
            greetingWaiter = null;
        }
    }


    IEnumerator CustomerSpawnRoutine()
    {
        yield return new WaitForSeconds(2f); // ��ʼ�ӳ�

        while (true)
        {
            // ����Ƿ�Ӧ�������¹˿�
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

    string GenerateCustomerName() //Ĭ�ϵ��漴���ɣ��ѱ��˿Ϳ���ȡ��
    {
        string[] surnames = { "��", "��", "��", "��", "��", "��", "��", "��" };
        string[] names = { "����", "Ůʿ", "С��", "���", "����" };
        return surnames[UnityEngine.Random.Range(0, surnames.Length)] +
               names[UnityEngine.Random.Range(0, names.Length)];
    }

    public static void AddToQueue(CustomerNPC customer)
    {
        customerQueue.Enqueue(customer);
        customer.QueingNumber = customerQueue.Count;
        Debug.Log($"[RestaurantManager] {customer.customerName} �ŶӺ�: {customer.QueingNumber}");
    }

    IEnumerator QueueProcessingRoutine()
    {
        while (true)
        {
            // ����Ƿ��й˿����Ŷ���û�й˿������
            if (customerQueue.Count > 0 && !customerAtEntrance && !isProcessingQueue)
            {
                isProcessingQueue = true;

                // ��ȡ���׹˿ͣ��ŶӺ�Ϊ1�Ĺ˿ͣ�
                CustomerNPC nextCustomer = customerQueue.Peek();

                // ����Ƿ��п���
                if (GetAvailableTableCount() > 0)
                {
                    // ����
                    customerQueue.Dequeue();

                    // �ù˿ͽ������
                    nextCustomer.NotifyEnterRestaurant();

                    // ���º����˿͵��ŶӺ�
                    UpdateQueueNumbers();

                    Debug.Log($"[RestaurantManager] ֪ͨ {nextCustomer.customerName} �������");
                }
                else
                {
                    Debug.Log("[RestaurantManager] û�п������˿ͼ����ȴ�");
                }

                isProcessingQueue = false;
            }
            // �����飺�����ڱ�־�����õ�û���ҵ���ڵĹ˿ͣ������־
            if (customerAtEntrance && GetCustomerAtEntrance() == null)
            {
                Debug.Log("[RestaurantManager] ��⵽��ڱ�־�쳣�������־");
                customerAtEntrance = false;
            }

            yield return new WaitForSeconds(0.1f); // ÿ2����һ��
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
    #region ��������
    public static Transform AssignTable()
    {
        foreach (var kvp in tableOccupancy)
        {
            if (!kvp.Value) // �ҵ����в���
            {
                tableOccupancy[kvp.Key] = true;
                Debug.Log($"[RestaurantManager] �������: {kvp.Key.name}");
                return kvp.Key;
            }
        }

        Debug.LogWarning("[RestaurantManager] û�п��в���");
        return null;
    }

    public static void FreeTable(Transform table)
    {
        if (table == null)
        {
            Debug.LogWarning("[RestaurantManager] �����ͷſղ���");
            return;
        }
        if (tableOccupancy.ContainsKey(table))
        {
            tableOccupancy[table] = false;
            Debug.Log($"[RestaurantManager] ���� {table.name} ���ͷ�");
        }
        else
        {
            Debug.LogError($"[RestaurantManager] ���� {table.name} ���ڹ����б���");
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

    #region С�ѹ���
    public static void AddTipToNearestWaiter(Vector3 customerPosition, int tipAmount)
    {
        NPCBehavior nearestWaiter = null;
        float minDistance = float.MaxValue;

        // �������з���ԱNPC
        NPCBehavior[] allWaiters = FindObjectsOfType<NPCBehavior>();

        foreach (var waiter in allWaiters)
        {
            if (waiter.occupation == "����Ա")
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
            nearestWaiter.AddMemory($"���{tipAmount}ԪС�ѣ���С�ѣ�{nearestWaiter.tips}Ԫ");
            //nearestWaiter.statusUI?.UpdateTexts();
            Debug.Log($"[RestaurantManager] {nearestWaiter.npcName} ��� {tipAmount} ԪС��");
        }
    }

    #endregion

    #region ����״̬��ѯ
    /*public static RestaurantStatus GetRestaurantStatus()
    {
        return new RestaurantStatus
        {
            totalTables = tableOccupancy.Count,
            occupiedTables = tableOccupancy.Count(kvp => kvp.Value),
            customerCount = activeCustomers.Count,
            hasCustomerAtEntrance = customerAtEntrance,
            greetingWaiterName = greetingWaiter?.npcName ?? "��"
        };
    }*/

    #endregion

    #region ��ʦ����
    [Header("��ʦ����")]
    public ChefBehavior[] chefs; // ��Inspector����ק�����ʦ

    // ��ʦע��ϵͳ����̬ע�ᣩ
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
    // ע���ʦ
    public static void RegisterChef(ChefBehavior chef)
    {
        if (!registeredChefs.Contains(chef))
        {
            registeredChefs.Add(chef);
            Debug.Log($"[RestaurantManager] ��ʦ {chef.npcName} ��ע�ᣬ��ǰ��ʦ��: {registeredChefs.Count}");
        }
    }

    // ע����ʦ
    public static void UnregisterChef(ChefBehavior chef)
    {
        if (registeredChefs.Contains(chef))
        {
            registeredChefs.Remove(chef);
            Debug.Log($"[RestaurantManager] ��ʦ {chef.npcName} ��ע����ʣ���ʦ��: {registeredChefs.Count}");
        }
    }

    // ��ȡ���г�ʦ
    public static List<ChefBehavior> GetAllChefs()
    {
        return new List<ChefBehavior>(registeredChefs);
    }

    // ��ȡ���г�ʦ
    public static ChefBehavior GetAvailableChef()
    {
        foreach (var chef in registeredChefs)
        {
            Debug.Log($"�Ƿ��ж�����{chef.hasOrder}����ǰ״̬��{chef.chefState}");
            if (chef != null && !chef.hasOrder && chef.chefState == ChefBehavior.ChefState.Idle)
            {
                return chef;
            }
        }
        return null;
    }

    // ��ȡ��ʦͳ����Ϣ
    public static string GetChefStats()
    {
        int totalChefs = registeredChefs.Count;
        int busyChefs = registeredChefs.Count(c => c != null && c.hasOrder);
        int idleChefs = totalChefs - busyChefs;

        return $"��ʦ����: {totalChefs}, æµ: {busyChefs}, ����: {idleChefs}";
    }
    #endregion



    #region ��������
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
        public ChefBehavior assignedChef; // ��ӷ���ĳ�ʦ

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
            // ÿ5����һ���Ƿ��д�����Ķ���
            TryAssignPendingOrders();

            // ���ͳ����Ϣ����ѡ�����ڵ��ԣ�
            if (pendingOrders.Count > 0 || preparingOrders.Count > 0 || readyOrders.Count > 0)
            {
                Debug.Log($"[RestaurantManager] {GetOrderStats()} | {GetChefStats()}");
            }

            yield return new WaitForSeconds(5f);
        }
    }

    // ����¶���
    public static void AddOrder(CustomerNPC customer, NPCBehavior waiter, Transform table, string dishName)
    {
        Order newOrder = new Order(nextOrderId++, customer, waiter, table, dishName);
        pendingOrders.Add(newOrder);

        Debug.Log($"[RestaurantManager] �¶��� #{newOrder.orderId}: {dishName} - ���� {table.name}");
        Debug.Log($"[RestaurantManager] ��ǰ����������: {pendingOrders.Count}");

        // �������Է�������г�ʦ
        AssignOrderToChef(newOrder);
    }
    private static bool AssignOrderToChef(Order order)
    {
        ChefBehavior availableChef = GetAvailableChef();

        if (availableChef != null)
        {
            // �Ƴ��������б������������б�
            pendingOrders.Remove(order);
            preparingOrders.Add(order);

            // �������ʦ
            order.assignedChef = availableChef;
            order.isBeingPrepared = true;

            // ֪ͨ��ʦ��ʼ����
            availableChef.AssignOrder(order);

            Debug.Log($"[RestaurantManager] ���� #{order.orderId} �ѷ������ʦ {availableChef.npcName}");
            return true;
        }
        else
        {
            Debug.Log($"[RestaurantManager] ���޿��г�ʦ������ #{order.orderId} �ȴ���");
            return false;
        }
    }
    // ���Է��������Ķ��������ڵ��ã�
    public static void TryAssignPendingOrders()
    {
        for (int i = pendingOrders.Count - 1; i >= 0; i--)
        {
            if (AssignOrderToChef(pendingOrders[i]))
            {
                // �ɹ�����һ����������Լ���������breakֻ����һ��
                break;
            }
        }
    }
    // ֪ͨ��ʦ���¶���
    /*private static void NotifyChefOfNewOrder()
    {
        ChefBehavior[] chefs = FindObjectsOfType<ChefBehavior>();

        foreach (var chef in chefs)
        {
            if (!chef.hasOrder) // �ҵ����еĳ�ʦ
            {
                chef.SetOrderStatus(true);
                Debug.Log($"[RestaurantManager] ֪ͨ��ʦ {chef.npcName} �����¶���");
                break; // ֻ֪ͨһ����ʦ
            }
        }
    }*/

    // ��ʦ��ʼ������
    public static Order GetNextPendingOrder()
    {
        if (pendingOrders.Count > 0)
        {
            Order order = pendingOrders[0];
            pendingOrders.RemoveAt(0);
            preparingOrders.Add(order);
            order.isBeingPrepared = true;

            Debug.Log($"[RestaurantManager] ���� #{order.orderId} ��ʼ����");
            return order;
        }
        return null;
    }
    // ���Ը��ض���ʦ���䶩���ķ���
    public static Order TryAssignOrderToChef(ChefBehavior chef)
    {
        if (pendingOrders.Count > 0 && chef != null && !chef.hasOrder && chef.chefState == ChefBehavior.ChefState.Idle)
        {
            Order order = pendingOrders[0];

            // �Ƴ��������б������������б�
            pendingOrders.RemoveAt(0);
            preparingOrders.Add(order);

            // �������ʦ
            order.assignedChef = chef;
            order.isBeingPrepared = true;

            // ֪ͨ��ʦ��ʼ����
            chef.AssignOrder(order);

            Debug.Log($"[RestaurantManager] ���� #{order.orderId} �ѷ�������г�ʦ {chef.npcName}");
            return order;
        }
        return null;
    }

    // ����Ƿ��д��������ķ���
    public static bool HasPendingOrdersForChef()
    {
        return pendingOrders.Count > 0;
    }

    // ��ȡ������������
    public static int GetPendingOrderCount()
    {
        return pendingOrders.Count;
    }
    // ��ʦ��ɶ���
    public static void CompleteOrder(Order completedOrder)
    {
        if (completedOrder != null && preparingOrders.Contains(completedOrder))
        {
            preparingOrders.Remove(completedOrder);
            readyOrders.Add(completedOrder);
            completedOrder.isReady = true;

            Debug.Log($"[RestaurantManager] ���� #{completedOrder.orderId} ������ɣ��ȴ��ϲ�");

            // ֪ͨ��Ӧ�ķ���Ա
            if (completedOrder.waiter != null)
            {
                completedOrder.waiter.NotifyOrderReady(completedOrder);
            }
            else
            {
                Debug.LogWarning($"[RestaurantManager] ���� #{completedOrder.orderId} û��ָ������Ա");
                // ���û��ָ������Ա����һ�����еķ���Ա
                NPCBehavior[] waiters = FindObjectsOfType<NPCBehavior>();
                foreach (var waiter in waiters)
                {
                    if (waiter.occupation == "����Ա" && waiter.waiterState == NPCBehavior.WaiterState.Idle)
                    {
                        waiter.NotifyOrderReady(completedOrder);
                        break;
                    }
                }
            }
        }
    }

    // ��ӷ�����ȡ��Ҫ�ϲ˵Ķ���
    public static List<Order> GetOrdersReadyForServing()
    {
        return new List<Order>(readyOrders);
    }

    // ����Աȡ�߶���
    public static void PickupOrder(int orderId)
    {
        Order order = readyOrders.FirstOrDefault(o => o.orderId == orderId);
        if (order != null)
        {
            readyOrders.Remove(order);
            // ͬʱ�������б����Ƴ�����ֹ�ڴ�й©��
            pendingOrders.Remove(order);
            preparingOrders.Remove(order);
            Debug.Log($"[RestaurantManager] ���� #{orderId} �ѱ�����Աȡ�߲���ȫ����");
        }
    }
    public static void CancelOrdersForTable(Transform table)
    {
        if (table == null)
        {
            Debug.LogWarning("[RestaurantManager] ����ȡ���ղ����Ķ���");
            return;
        }
        int cancelledCount = 0;

        cancelledCount +=pendingOrders.RemoveAll(order => order.table == table);
        cancelledCount +=preparingOrders.RemoveAll(order => order.table == table);
        cancelledCount += readyOrders.RemoveAll(order => order.table == table);
        if (cancelledCount > 0)
        {
            Debug.Log($"[RestaurantManager] ��ȡ������ {table.name} �� {cancelledCount} ������");
        }
    }
    // ����Ƿ��д�����Ķ���
    public static bool HasPendingOrders()
    {
        return pendingOrders.Count > 0;
    }

    // ��ȡ����ͳ��
    public static string GetOrderStats()
    {
        return $"������: {pendingOrders.Count}, ������: {preparingOrders.Count}, ��ȡ��: {readyOrders.Count}";
    }
    #endregion

    #region ������Ϣ����
    public string GetRestaurantStatusSummary()
    {
        StringBuilder sb = new StringBuilder();
        // ����״̬
        int occupiedTables = tableOccupancy.Count(kvp => kvp.Value);
        int totalTables = tableOccupancy.Count;
        sb.AppendLine($"��ǰ����ռ��: {occupiedTables}/{totalTables}");
        // �Ŷ����
        sb.AppendLine($"�Ŷ�����: {customerQueue.Count}");

        // ����״̬
        sb.AppendLine($"��������: {pendingOrders.Count}");
        sb.AppendLine($"�����ж���: {preparingOrders.Count}");
        sb.AppendLine($"���ϲ˶���: {readyOrders.Count}");

        // ����Ա״̬
        int idleWaiters = activeWaiters.Count(w => w.waiterState == WaiterState.Idle);
        sb.AppendLine($"���з���Ա: {idleWaiters}/{activeWaiters.Count}");

        // ��ʦ״̬
        int busyChefs = registeredChefs.Count(c => c.hasOrder);
        sb.AppendLine($"æµ��ʦ: {busyChefs}/{registeredChefs.Count}"); 

        // �����˿�״̬
        var eatingCustomers = activeCustomers.Count(c => c.currentState == CustomerState.Eating);
        var waitingCustomers = activeCustomers.Count(c => c.currentState == CustomerState.Seating);
        sb.AppendLine($"�ò��й˿�: {eatingCustomers}");
        sb.AppendLine($"�ȴ��ϲ˹˿�: {waitingCustomers}");

        return sb.ToString();
    }
    #endregion
    #region �˿͸�֪

    private static string GetRelativeTablePosition(Vector3 observerPos, Vector3 targetPos)
    {
        Vector3 direction = (targetPos - observerPos).normalized;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return direction.x > 0 ? "�ұ���" : "�����";
        }
        else
        {
            return direction.z > 0 ? "ǰ����" : "������";
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
            // �����Լ�
            if (customer == observer || customer == null || customer.assignedTable == null)
                continue;
            // �������Ƿ��ڸ�֪��Χ��
            float distance = Vector3.Distance(observerPosition, customer.assignedTable.position);
            if (distance <= perceptionRange)
            {
                string tablePos = GetRelativeTablePosition(observerPosition, customer.assignedTable.position);
                switch (customer.currentState)
                {
                    case CustomerState.Eating:
                        if (!string.IsNullOrEmpty(customer.orderedFood))
                        {
                            statusInfo.Add($"{tablePos}�Ĺ˿���������{customer.orderedFood}");
                        }
                        else
                        {
                            statusInfo.Add($"{tablePos}�Ĺ˿������ò�");
                        }
                        break;
                    case CustomerState.Seating:
                        statusInfo.Add($"{tablePos}�Ĺ˿����ڵȴ��ϲ�");
                        break;
                    case CustomerState.Ordering:
                        statusInfo.Add($"{tablePos}�Ĺ˿����ڵ��");
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
            return "��Χû�������˿�";
        }
        return $"��Χ��{nearbyCount}λ�˿ͣ�����{eatingCount}λ�����òͣ�{waitingCount}λ�ڵȴ��ϲ�";
    }
    #endregion


}
