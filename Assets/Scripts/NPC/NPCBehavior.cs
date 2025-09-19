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
    // �������ߵ����ݽṹ
    [System.Serializable]
    private class ActivityDecisionData
    {
        public string activity;
        public int duration;
        public string location;
        public string reason;
    }
    // �ճ̱��붯̬�����ã�
    private int dynamicActivityEndMinutes = -1; // -1��ʾδ����
    private int dynamicActivityStartDay = -1; // -1��ʾδ����
    private bool isScheduleReady = true; //���ڲ���Ҫai�ճ̱��ˣ�ֱ�Ӹ�true
    //private bool isWaitingForSchedule = false; 
    // ����������ֵ�����ݽṹ
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
        GREET,      // ӭ�ӹ˿ͣ����������ĵ�ˣ�
        SERVE,      // �ϲ˷���
        IDLE        // ����/�ȴ�
        //CLEAN��   // Ŀǰû����ֵ���������岻��
        //EXIT      // ��ŭ֮�²��ϰ���
    }

    // ��Ӷ���������
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isFacingRight = false;

    // �����������������һ�£�
    private const int UP = 0;
    private const int DOWN = 1;
    private const int LEFT = 2;
    private const int RIGHT = 3;
    // AI�ظ�
    [Header("AI�ظ�����")]
    public string defaultReply = "��Ϣ���յ�";
    public float replyDisplayTime = 3f;
    public bool playerInRange { get; private set; } = false; // ��Ϊ���ԣ���PlayerInteraction����

    //�����˻�е�Ի�������������е�ظ��İ汾������ʱ��ʾ
    [Header("�����������")]
    private static bool hasCustomerAtEntrance = false;  // ȫ�ֱ�־������Ƿ��й˿�
    private static NPCBehavior greetingWaiter = null;  // ����ӭ�ӵķ���Ա
    public float greetingResponseDelay = 2f;  // ��Ӧӭ�ӵ��ӳ�ʱ��
    private bool hasDecidedGreeting = false;  // �Ƿ��Ѿ�����ӭ�Ӿ���
    // ����Ա״̬ö��
    public enum WaiterState
    {
        Idle, //����Ϣ�ҵȴ�
        Greeting, //ӭ����ڵĹ˿ͣ���Ҫ��ȫ��֪ͨ���˿͵ִ 
        TakingOrder, //����λ�ϵȹ˿͵�͡����ԶԻ����������Ϣ�͵������
        DeliveringOrder, //ȥ����òˣ���Ҫ��֪ͨ��������������ɡ�
        ServingFood, //�Ͳ�
        Cleaning, //�˿��ߺ������������Ҫ��֪ͨ���˿���ȥ���������ӿ��б�־��ʾ
        MovingToDestination,
        Resting         // ��Ϣ
    }

    public string GetWaiterState()
    {
        switch (waiterState)
        {
            case WaiterState.Idle:
                return "����Ա����Ϣ�Ҵ���";
            case WaiterState.Greeting:
                return "����ӭ�ӹ˿�";
            case WaiterState.TakingOrder:
                return "����Ϊ�˿͵��";
            case WaiterState.DeliveringOrder:
                return "����ȡ��";
            case WaiterState.ServingFood:
                return "�����ϲ�";
            case WaiterState.Cleaning:
                return "�����������";
            case WaiterState.MovingToDestination:
                return "����æ";

        }
        return "����æ";
    }

    #region
    // �����������
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


    [Header("NPC������Ϣ")]
    public string npcName = "С��";
    public string npcName_eng = "";
    private float currentMoveSpeed = 8f; //�ƶ��ٶȲ������ﶨ�壬������GameManager�Ļ����ٶ�
    public float GetCurrentMoveSpeed()
    {
        return currentMoveSpeed;
    }
    public string personality = "���ĳ�";
    public string personality_eng = "";
    public string occupation = "����Ա";
    [SerializeField] public int health = 100;
    [SerializeField] public int energy = 50;
    [SerializeField] public int mood = 0;
    [SerializeField] public int tips = 50; //С��
    [SerializeField] public int wood = 0;
    [SerializeField] public string backgroundStory = "";
    [SerializeField] public string backgroundStory_eng = "";//Ӣ�ķ���ı�������
    [SerializeField] public int meat = 0;
    [SerializeField] public int vege = 0;
    [Header("���ֵ����")]
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
        return $"����: {energy:F0}/{maxEnergy:F0}| ����: {mood:F0}/{maxMood:F0} | ���: {tips}";
    }

    [Header("AI�Ի�����")]
    private bool useAI = true;  // �Ƿ�ʹ��AI���ɻظ���Խ��Խ���ˣ����ﲻҪ�ĳ�false

    [Header("NPC��������")]
    public float npcInteractionRadius = 3f;
    public float npcGreetChance = 0.7f; // ���к��ĸ���
    public float minDialogueInterval = 30f; // ���ζԻ�����С���ʱ��
    private float lastDialogueTime = -100f;
    private NPCBehavior currentTalkingTo;
    private bool isInDialogue = false;
    private const float DIALOGUE_PAUSE_DURATION = 1.5f; // �Ի�ʱ�Ķ���ͣ�٣�ֻ��NPC vs NPC ʹ���ˣ�NPC vs �˿�δʹ��
    private string lastDialogueContent = "";

    [Header("AI�ճ�����")]  //�Ƿ�ʹ��AI���ɵ��ճ�
    [SerializeField] private bool useDynamicDecision = true;  // �Ƿ�ʹ�ö�̬����
    private System.DateTime lastScheduleGeneration;
    private bool isGeneratingSchedule = false;
    private static int totalNPCCount = 0;
    private static int readyNPCCount = 0;
    private static bool hasStartedTime = false;
    private float previousTimeScale;
    public event System.Action<NPCBehavior> OnNPCScheduleReady;

    [Header("λ������")]
    public Transform entrancePosition; //�˿ͽ���Ĵ���
    public Transform kitchenPosition; //����λ��
    public Transform tablePosition; //����λ��
    public Transform restingPosition; //������Ϣλ��
    public Transform cleanerPosition; //��ࡢ������λ��
    public float destinationThreshold = 1f;
    [Header("Ѱ·����")]
    
    [Header("��������")]
    public float interactionRadius = 2f;
    public string playerTag = "Player"; // ʹ��Player��ǩ��ʶ�����
    private const string NPCTag = "NPC"; // ��ǩ����,NPC���NPC��������NPC����ָ����Ա���˿�������Tag��"Customer"��ʾ
    public GameObject dialogueBubblePrefab;
    public Vector3 bubbleOffset = new Vector3(0, 200, 0);
    public float bubbleDisplayTime = 3f;
    // ����ϵͳ
    private List<string> memoryList = new List<string>(); //������ʵʱ���ߣ�Ҳ���ڱ��������ɼ�txt
    private MemoryManager memoManager;
    public List<string> memoryList_resouce = new List<string>();//����Դ������صļ��䣬�������������api��Ϊ�Լ�������
    private int maxResourceMemories = 10;
    // ������������
    private const int MAX_MEMORY_COUNT = 200;
    private List<string> recentMemorySummary = new List<string>(); // ��������ժҪ
    // ԭʼ��NPC״̬
    public WaiterState waiterState = WaiterState.Idle;

    private Transform currentDestination;
    private Coroutine currentRoutine;

    // �Ի����
    private GameObject currentBubble;
    private Coroutine bubbleCoroutine;
    //private bool playerInRange = false;
    private GameObject currentPlayer;
    // ai�����ճ�
    private List<DailyActivity> dailySchedule;
    private DailyActivity currentActivity;
    private bool isExecutingActivity = false;

    #region****************************************Unity���****************************************
    private void Awake()
    {
    }
    void Start()
    {
        //InitializeStatusUI();
        //CreateStatusUI();
        memoManager = GetComponent<MemoryManager>();
        //TimeManager.Instance.SetCustomTimeScale(0f);  // �ʼ��ͣ
        SetupTimeManager();
        SetupDialogueBubble();
        InitializeAnimComponents();
        lastDialogueTime = Time.time - minDialogueInterval; // ȷ����ʼ�ɶԻ�
        currentActivity = new DailyActivity();
        StartCoroutine(DelayedInitialize());
    }

    void Update()
    {
        // ������������
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("��⵽�������");
            // �����λ�÷�������
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);

            // ��������Ƿ���������NPC
            if (hit.collider != null)
            {
                Debug.Log("��⵽�������������: " + hit.collider.gameObject.name);
                if (hit.collider.gameObject == gameObject)
                {
                    Debug.Log("��⵽���������NPC��");
                    HandleClick();
                }
            }
        }
        // ������
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

    #region****************************************�������****************************************
    void InitializeAnimComponents()
    {
        // ��ʼ���������
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = isFacingRight;
        }

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetInteger("Direction", DOWN);//Ĭ������
        }
    }
    public void HandleAnimation(Vector2 moveDirection)
    {
        bool isMoving = moveDirection.magnitude > 0.1f;

        if (animator != null)
        {
            animator.SetBool("IsWalking", isMoving);

            // ֻ�����ƶ�����ʱ���·���
            if (isMoving)
            {
                int direction = GetDirection(moveDirection);
                animator.SetInteger("Direction", direction);
            }
        }
        // ����ˮƽ��ת
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




    #region****************************************��ҽ������****************************************

    void SetupTimeManager()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.AddListener(OnTimeScaleChanged);
            currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();//ʱ���������
        }
    }



    void CheckPlayerInteraction()
    {
        // ��ȡ��Χ�ڵ�������ײ��
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRadius);

        bool playerFound = false;
        GameObject foundPlayer = null;

        // �����Ƿ������
        foreach (var collider in colliders)
        {
            if (collider.CompareTag(playerTag))
            {
                playerFound = true;
                foundPlayer = collider.gameObject;
                break;
            }
        }

        // ������ҽ���/�뿪
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
        // ���ݵ�ǰ״̬ѡ��Ի�,�ĳ������������ĵ���
        StartCoroutine(GreetPlayer());

    }

    void OnPlayerExitRange()
    {
        playerInRange = false;  // ȷ���������
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

        // ����Content�ı��������ȷ·��
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
                        Debug.LogWarning("�������ʧ�ܣ���ʹ��Ĭ������");
                    }
                }
                else
                {
                    Debug.LogError("Content�������Ҳ���TMP_Text���");
                }
            }
            else
            {
                Debug.LogError("Panel���Ҳ���Content����");
            }
            Transform nameLayer = panel.Find("Header");
            Transform NPCNameLayer = nameLayer.Find("NPCName");
            TMP_Text nameText = NPCNameLayer.GetComponent<TMP_Text>();
            //nameText.font = Resources.Load<TMP_FontAsset>("Fonts/msyh SDF");
            nameText.text = npcName;
        }
        else
        {
            Debug.LogError("�Ҳ���Panel����");
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

    // ��ӽ�����Ϣ�ķ���
    public void ReceivePlayerMessage(string message)
    {
        Debug.Log($"[{npcName}] �յ������Ϣ: {message}");
        AddMemory($"�ϰ����˵��{message}");
        if(message.Contains("��ȥ")|| message.Contains("��") || message.Contains("͵��") || message.Contains("��ֹ") || message.Contains("����"))
        {
            statusUI.UpdateMood(mood, mood - 10);
            mood -= 10;
        }
        // ��ʾ�ظ�
        string reply = "";
        this.GetAIResponse(message, (reply) =>
        {
            Debug.Log($"�յ�AI�ظ�: {reply}");
            ShowDialogueBubble(reply);
            AddMemory($"��ظ��ϰ�˵��{reply}");
        });
        //string reply = GenerateReply(message); //����Ĭ�ϵ�
        //ShowDialogueBubble(reply);

        // ȷ���Ի���ʾ�㹻����ʱ��
        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
        }
        bubbleCoroutine = StartCoroutine(ShowBubbleForDuration(replyDisplayTime));
    }

    // ���ɻظ��ķ��������Խ���AI��
    string GenerateReply(string playerMessage)
    {
        // ����������Ե���AI API
        // �����ȷ���Ĭ�ϻظ�
        return defaultReply;
    }

    // ���һ���µ�Э�̷�����������ʾʱ��
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
            NPCData npcData = NPCData.CreateSafe(this); // ʹ�ð�ȫ�Ĺ�������
            StartCoroutine(AzureOpenAIManager.Instance.GetNPCResponse(npcData, playerMessage, onResponse));
        }
        catch (Exception e)
        {
            Debug.LogError($"��ȡAI�ظ�ʱ����: {e.Message}");
            onResponse?.Invoke("��Ǹ���������޷���Ӧ");
        }
    }


    // �������ϰ��ʺ��Э��
    IEnumerator GreetPlayer()
    {
        // �����������
        string situation = "";

        switch (waiterState)
        {
            case WaiterState.ServingFood:
                situation = "�����ڸ��˿��ϲˣ��ϰ����˹���";
                break;
            case WaiterState.TakingOrder:
                situation = "������Ϊ�˿͵�ˣ��ϰ徭��";
                break;
            case WaiterState.Cleaning:
                situation = "����������������ϰ��ߵ��Ա�";
                break;
            case WaiterState.Idle:
                situation = "��պ��пգ��ϰ����˹���";
                break;
            case WaiterState.Greeting:
                situation = "������ӭ���¹˿ͣ��ϰ�Ҳ�ڸ���";
                break;
            default:
                situation = "�ϰ����㸽��";
                break;
        }

        // �����Ƿ��ʺ�
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
            // ��ʹ��AIʱ��Ĭ����Ϊ
            if (waiterState == WaiterState.Idle)
            {
                greeting = "�ϰ�ã�";
            }
            else
            {
                greeting = "..."; // æµʱ��˵��
            }
        }

        // ��ʾ�Ի����������˵����
        if (greeting != "...")
        {
            ShowDialogueBubble(greeting);
            AddMemory($"���ϰ��ʺ�{greeting}");
        }
        else
        {
            // �������һ����ͷ���������������Ա�ʾ
            Debug.Log($"[{npcName}] ̫æ�ˣ�û�к��ϰ���к�");
        }

    }

    private string GetLastPlayerCommand()
    {
        // ��ӿ�ֵ���
        if (memoryList == null || memoryList.Count == 0)
        {
            return null;
        }

        // �������5�������е����ָ��
        var recentPlayerMessages = memoryList
            .Where(m => m.Contains("�ϰ����˵"))
            .TakeLast(5)
            .ToList();

        if (recentPlayerMessages.Count > 0)
        {
            // ���������һ�����ָ��
            string lastMessage = recentPlayerMessages.Last();
            int startIndex = lastMessage.IndexOf("�ϰ����˵") + "�ϰ����˵".Length;
            return lastMessage.Substring(startIndex).Trim();
        }

        return null;
    }

    private DailyActivity? ParsePlayerCommand(string command)
    {
        // ��ӿ�ֵ���
        if (string.IsNullOrEmpty(command))
        {
            return null;
        }

        int currentHour = TimeManager.Instance.CurrentHour;
        int currentMinute = TimeManager.Instance.CurrentMinute;

        if (command.Contains("��ȥentrance") || command.Contains("ȥ�ſ�") || command.Contains("�˿�"))
        {
            //waiterState = WaiterState.Greeting;
            
            return new DailyActivity(currentHour, currentMinute, 10, "ִ���ϰ�ָ��:ӭ�ӹ˿�", entrancePosition);
        }
        else if (command.Contains("ȥ�ɻ�") || command.Contains("����Ϣ") || command.Contains("����ȥ�ɻ�") || command.Contains("�ɻ�"))
        {
            // ���ݵ�ǰ���ѡ������ʵĹ���
            if (RestaurantManager.IsCustomerAtEntrance())
            {
                //waiterState = WaiterState.Greeting;
                return new DailyActivity(currentHour, currentMinute, 10, "ִ���ϰ�ָ��:ӭ�ӹ˿�", entrancePosition);
            }
            else
            {
                //waiterState = WaiterState.Cleaning;
                return new DailyActivity(currentHour, currentMinute, 10, "ִ���ϰ�ָ��:�������", tablePosition);
            }
        }
        else if (command.Contains("�ϲ�") || command.Contains("ȡ��"))
        {
            //waiterState = WaiterState.DeliveringOrder;
            return new DailyActivity(currentHour, currentMinute, 5, "ִ���ϰ�ָ��:�ϲ�", kitchenPosition);
        }
        else if (command.Contains("ȥ��Ϣ") || command.Contains("��Ϣ"))
        {
            //waiterState = WaiterState.Idle;
            return new DailyActivity(currentHour, currentMinute, 15, "ִ���ϰ�ָ��:��Ϣ", restingPosition);
        }

        return null; // ���ؿɿ�����
    }

    #endregion



    #region****************************************npc���ཻ�����****************************************

    public string GetCurrentLocationName()
    {
        try
        {
            // ʹ���б�����ֵ��null key����
            var locations = new System.Collections.Generic.List<(Transform transform, string name)>();

            // ֻ��ӷǿյ�λ��
            if (entrancePosition != null) locations.Add((entrancePosition, "�����ſ�"));
            if (kitchenPosition != null) locations.Add((kitchenPosition, "������"));
            if (tablePosition != null) locations.Add((tablePosition, "����"));
            if (restingPosition != null) locations.Add((restingPosition, "Ա����Ϣ��"));
            if (cleanerPosition != null) locations.Add((cleanerPosition, "����"));

            // ���û���κ���Чλ��
            if (locations.Count == 0)
            {
                return "Ա����Ϣ��"; // ����Ĭ��λ��
            }

            // ���������λ��
            float minDistance = float.MaxValue;
            string closestLocation = "·��";

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
            Debug.LogError($"[{npcName}] GetCurrentLocationName�쳣: {e.Message}");
            return "Ա����Ϣ��"; // ����ʱ����Ĭ��ֵ
        }
    }
    void CheckNPCInteraction()
    {
        // ��ӵ�����־
        //Debug.Log($"[{npcName}] ���NPC���� - ״̬: {currentState}, ִ�л: {isExecutingActivity}");
        // ������ڶԻ���ִ�л��ʱ����������������
        if (isInDialogue || isExecutingActivity ||
            Time.time - lastDialogueTime < minDialogueInterval)
        {
            return;
        }

        // ��ȡ��Χ�ڵ�NPC
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, npcInteractionRadius);
        List<NPCBehavior> nearbyNPCs = new List<NPCBehavior>();

        foreach (var collider in colliders)
        {
            // ȷ��ֻ���NPC��ǩ�Ķ���
            if (collider.CompareTag("NPC") && collider.gameObject != gameObject)
            {
                NPCBehavior npc = collider.GetComponent<NPCBehavior>();
                if (npc != null && !npc.isInDialogue &&
                    !npc.isExecutingActivity && // ȷ���Է�û��ִ�л
                    Time.time - npc.lastDialogueTime > npc.minDialogueInterval)
                {
                    nearbyNPCs.Add(npc);
                    Debug.Log($"[{npcName}] ���ָ���NPC: {npc.npcName}");
                }
            }
        }

        // ���ѡ���Ƿ���Ի�������
        //if (nearbyNPCs.Count > 0 && UnityEngine.Random.value < npcGreetChance)
        {
            NPCBehavior targetNPC = GetClosestNPC(nearbyNPCs);
            if (targetNPC != null)
            {
                //Debug.Log($"[{npcName}] ������ {targetNPC.npcName} �Ի�");
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

    // ��ӶԻ�Э��
    private IEnumerator InitiateDialogue(NPCBehavior otherNPC)
    {
        // ���˫��Ϊ�Ի���
        isInDialogue = true;
        otherNPC.isInDialogue = true;
        currentTalkingTo = otherNPC;

        // ����ԭʼ�ƶ�״̬����Ӱ��ʵ���ƶ���
        Vector3 originalPosition = transform.position;
        Vector3 otherOriginalPosition = otherNPC.transform.position;

        // �����ʺ���
        yield return StartCoroutine(GenerateDialogue(otherNPC, true));
        
        // ����ͣ��
        yield return new WaitForSeconds(DIALOGUE_PAUSE_DURATION);
        // ���ɻ�Ӧ
        yield return otherNPC.StartCoroutine(otherNPC.GenerateDialogue(this, false));
        // �ָ�״̬
        isInDialogue = false;
        otherNPC.isInDialogue = false;
        currentTalkingTo = null;
        lastDialogueTime = Time.time;
        otherNPC.lastDialogueTime = Time.time;
    }

    // ��ӶԻ����ɷ���
    public IEnumerator GenerateDialogue(NPCBehavior otherNPC, bool isInitiator)
    {
        string safeNpcName = string.IsNullOrEmpty(npcName) ? "NPC" : npcName;
        string safePersonality = string.IsNullOrEmpty(personality) ? "" : personality;

        if (otherNPC == null)
        {
            Debug.LogError($"[{safeNpcName}] �Ի����󲻴��ڣ���ֹ�Ի�");
            yield break;
        }

        string safeOtherNpcName = string.IsNullOrEmpty(otherNPC.npcName) ? "����NPC" : otherNPC.npcName;
        string safeLastDialogue = currentTalkingTo != null ?
            (string.IsNullOrEmpty(currentTalkingTo.lastDialogueContent) ? "���" : currentTalkingTo.lastDialogueContent)
            : "���";

        // ʹ�� NPCData,��һ�����Ӽ��䣬��ҡ�����ֵ�ȵ�Ӱ�졣
        // �ص��ȡ����һ������
        //����Ҫ���ʱ��
        string context;
        if (isInitiator)
        {
            //NPCData initiatorData = new NPCData(this);
            //NPCData targetData = new NPCData(otherNPC);
            NPCData initiatorData = NPCData.CreateSafe(this);
            NPCData targetData = NPCData.CreateSafe(otherNPC);
            //��ʱ���ӵص�
            context = $"{initiatorData.npcName}��{initiatorData.personality}��{initiatorData.occupation}��" +
                      $"��{initiatorData.currentLocation}������{targetData.npcName}��{targetData.personality}��{targetData.occupation}����" +
                      $"��ǰ����ֵ��{initiatorData.energy}/{initiatorData.maxEnergy}������ֵ��{initiatorData.mood}/{initiatorData.maxMood}" +
                      $"������յ���{initiatorData.money}С�ѡ�" +
            "���������Ը�͵�ǰ״̬����̴���к���1-2�仰��,�������ֵ������ֵ�ǳ��߻��߷ǳ��ͣ�Ҫ�����ڶԻ��С�";
        }
        else
        {
            NPCData responderData = new NPCData(this);
            //��ʱ���ӵص�
            context = $"{otherNPC.npcName}����˵��\"{safeLastDialogue}\"��\n" +
                      $"�㵱ǰ��{responderData.currentLocation}��״̬��{responderData.currentState}��" +
                      $"��ǰ����ֵ��{responderData.energy}/{responderData.maxEnergy}������ֵ��{responderData.mood}/{responderData.maxMood}" +
                      $"������յ���{responderData.money}С�ѡ�" +
                      $"���������Ը�{responderData.personality}���͵�ǰ״̬��̻ظ���1-2�仰�����������ֵ������ֵ�ǳ��߻��߷ǳ��ͣ�Ҫ�����ڶԻ��С�";
        }

        //Debug.Log($"[{safeNpcName}] �Ի�������: {context}");

        NPCData npcData = new NPCData(this);
        string dialogueContent = "";

        // ������װ��������Э�̽��
        CoroutineWithData coroutineData = new CoroutineWithData(
            this,
            AzureOpenAIManager.Instance.GetNPCResponse(
                npcData,
                context,
                (response) => { dialogueContent = response; }
            )
        );
        
            
        // �ȴ�APIЭ�����
        yield return coroutineData.coroutine;

        // ����Ƿ�����Ч��Ӧ
        if (string.IsNullOrEmpty(dialogueContent))
        {
            Debug.LogWarning("API���ؿ���Ӧ��ʹ��Ĭ�ϻظ�");
            dialogueContent = isInitiator ? "��ã�" : "�������ˣ�";
        }

        // ��¼�Ի�
        lastDialogueContent = dialogueContent;

        // ��ʾ�Ի�����
        ShowDialogueBubble(dialogueContent);

        // �ȴ��Ի���ʾʱ��
        yield return new WaitForSeconds(bubbleDisplayTime);
        if (isInitiator)
        {
            AddMemory($"��{otherNPC.npcName}���к�˵��{dialogueContent}");
        }
        else
        {
            AddMemory($"{otherNPC.npcName}����˵��{safeLastDialogue}");
            AddMemory($"��{otherNPC.npcName}�ظ�˵��{dialogueContent}");
        }


        // ��������
        HideDialogueBubble();

        // ��¼�Ի���־
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
            Debug.LogWarning("DialogueLoggerʵ��ȱʧ");
        }
    }

    // ��ӻ�ȡ������ķ���





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


    #region****************************************��������Ϊ���****************************************

    #region �ճ̱�

    public bool IsScheduleReady() //��ʵ�Ѿ�������
    {
        return isScheduleReady;
    }

    void InitializeDailySchedule()
    {
        // ��ʼ��һ��Ĭ�ϵ��ճ̣��ĳɷ���
        dailySchedule = new List<DailyActivity>
        {
            new DailyActivity(16, 0, 30, "׼������", entrancePosition),
            new DailyActivity(16, 30, 360, "Ӫҵ�׶Σ��������", restingPosition),
            new DailyActivity(22, 30, 30, "����׼������", cleanerPosition)
        };
    }
    // ����Ա������ΪӦ���������������¿��ˣ�ǰȥ��������ˣ�����������Ӻ���ϲ�
    IEnumerator DelayedInitialize()
    {
        // �ȴ�һ֡��ȷ������������ѳ�ʼ��
        yield return null;

        // �ȴ�TimeManager��ʼ��
        while (TimeManager.Instance == null)
        {
            Debug.Log($"[{npcName}] �ȴ� TimeManager ��ʼ��...");
            yield return new WaitForSeconds(0.1f);
        }

        // �ȴ�AzureOpenAIManager��ʼ�������ʹ��AI��
        if (useDynamicDecision)
        {
            while (AzureOpenAIManager.Instance == null)
            {
                Debug.Log($"[{npcName}] �ȴ� AzureOpenAIManager ��ʼ��...");
                yield return new WaitForSeconds(0.1f);
            }
        }

        InitializeStatusUI();
        isScheduleReady = true;
        InitializeDailySchedule();
        string memoryTemp = $"�ƶ��˽���ļƻ���{GetScheduleSummary()}";//get����Ҳ���Ը�Ϊֱ����npc���е�ȡ
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
        public Vector3 position; // ���λ���ֶΣ�����AI���ɵ��ճ�
        public DailyActivity(int hour, int minute, int duration, string act, Transform dest)
        {
            startHour = hour;
            startMinute = minute;
            durationMinutes = duration;
            activity = act;
            destination = dest;
            position = dest != null ? dest.position : Vector3.zero;
        }

        // �������캯����֧��Vector3λ�ã���������¼���Ѱ������Agent
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
        // ��ӿ�ֵ���
        if (dailySchedule == null || dailySchedule.Count == 0)
        {
            Debug.Log($"[{npcName}] �ճ̱�Ϊ�գ�����Ĭ���ճ̲ο�");
            return "����Ԥ���ճ̣����ɾ���ģʽ��";
        }

        StringBuilder summary = new StringBuilder();
        foreach (var activity in dailySchedule)
        {
            summary.AppendLine($"{activity.startHour:D2}:{activity.startMinute:D2} - {activity.activity}");
        }
        return summary.ToString();
    }

    // ͨ�ûִ�з���
    IEnumerator PerformGenericActivity(DailyActivity activity)
    {
        waiterState = WaiterState.Idle;

        while (IsStillInActivityTime(activity))
        {
            yield return new WaitForSeconds(0.5f);
        }
    }

    #endregion

    #region ʵʱ����
    [System.Serializable]
    public class WaiterDecision
    {
        public WaiterAction action;
        public string dialogue; // ������Ҫ˵�Ļ�
        public string reason;
    }
    private Coroutine decisionRoutine;
    private bool isMoving = false;
    public IEnumerator RealtimeDecision()
    {
        while (true)
        {
            // ֻ�е�����ʱ��������
            if (waiterState == WaiterState.Idle)
            {
                Debug.Log($"[{npcName}] ׼����ȡ��ָ��");

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

                Debug.Log($"[{npcName}] ����ִ��: {decision.action}, ����: {decision.reason}");

                // ���ݾ���ִ����Ӧ�ж�
                switch (decision.action)
                {
                    case WaiterAction.GREET:
                        yield return StartCoroutine(PerformGreetingAndOrderAction(decision.dialogue));
                        break;

                    case WaiterAction.SERVE:
                        yield return StartCoroutine(PerformServingAction());
                        break;

                    case WaiterAction.IDLE:
                        // ����״̬���ƶ�����Ϣ�㲢�ȴ�һ��ʱ��
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

        // ��黷��״̬ - ȷ��������µĶ���״̬
        CheckForNewOrders(); // ������У�ȷ���������������µ�
        var myReadyOrder = RestaurantManager.GetReadyOrderForWaiter(this);
        bool hasOrderToPickup = myReadyOrder != null || orderQueue.Count > 0;
        bool hasCustomerAtEntrance = RestaurantManager.IsCustomerAtEntrance();
        bool isAnyoneGreeting = RestaurantManager.IsAnyoneGreeting();
        bool canGreet = RestaurantManager.TryReserveGreeting(this);

        // ���������������
        string environmentInfo = "";

        if (hasCustomerAtEntrance)
        {
            if (canGreet)
            {
                environmentInfo += "- �ſ��й˿͵ȴ��������ȥӭ��\n";
            }
            else
            {
                environmentInfo += "- �ſ��й˿͵ȴ�����������������Ա��ӭ��\n";
            }
        }

        if (hasOrderToPickup)
        {
            environmentInfo += $"- ��{orderQueue.Count}��������Ҫ�ϲ�\n";
            environmentInfo += "- �ϲ�������������ȼ���\n";
        }

        // ����Ա״̬��Ϣ
        string statusInfo = $"����:{energy}/{maxEnergy}, С��:{tips}";
        string currentMemory = string.Join("\n", memoryList.TakeLast(3));

        string prompt = $@"��Ϊ{npcName}��{occupation}���������������������һ���ж���
ʱ�䣺{currentTime}
״̬��{statusInfo}
���������
{environmentInfo}
������䣺
{currentMemory}

���������Ը�{personality}��ѡ������ʵ��ж���
1. �ϲ�������������ȼ�������ж�����Ҫ�ϲˣ������ѡ��SERVE��
2. ����й˿����ſ�������ӭ�ӣ����Կ���ѡ��GREET
3. ���û�н������񣬿���ѡ��IDLE��Ϣ

��ѡ�ж���
- GREET: ӭ�ӹ˿ͣ����������ĵ�ˣ�
- SERVE: �ϲ˷���
- IDLE: ����/��Ϣ

�뷵��JSON��ʽ��
{{
  ""action"": ""�ж�����"",
  ""dialogue"": ""�����Ҫ˵�Ļ�"",
  ""reason"": ""ѡ����ж�������""
}}";

        // ����AI����
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

        // ����AI��Ӧ
        try
        {
            var decision = ParseWaiterDecision(aiResponse);

            // ��֤���ߺ����� - ǿ�����ȼ�
            if (hasOrderToPickup && decision.action != WaiterAction.SERVE)
            {
                Debug.LogWarning($"[{npcName}] �ж�����AIδѡ���ϲˣ�ǿ�Ƹ�Ϊ�ϲ�");
                decision.action = WaiterAction.SERVE;
                decision.reason = "�ж�����Ҫ�����ϲ�";
            }
            // ���AI��ӭ�ӵ��޷�Ԥ������Ϊ����
            else if (decision.action == WaiterAction.GREET && !canGreet)
            {
                Debug.LogWarning($"[{npcName}] ��ӭ�ӵ��޷�Ԥ������Ϊ����");
                decision.action = WaiterAction.IDLE;
                decision.reason = "�޷�ӭ�ӹ˿ͣ���Ϊ����";
            }

            onDecisionDecided?.Invoke(decision);
        }
        catch (Exception e)
        {
            Debug.LogError($"��������ʧ��: {e.Message}");
            // Ĭ�Ͼ��� - ����
            onDecisionDecided?.Invoke(new WaiterDecision
            {
                action = WaiterAction.IDLE,
                reason = "��������ʧ�ܣ�Ĭ�Ͽ���"
            });
        }
    }

    private WaiterDecision ParseWaiterDecision(string jsonResponse)
    {
        //Debug.Log($"AI������Ӧ: {jsonResponse}");
        try
        {
            // ��ȡJSON����
            int startIdx = jsonResponse.IndexOf('{');
            int endIdx = jsonResponse.LastIndexOf('}');
            if (startIdx >= 0 && endIdx >= 0)
            {
                jsonResponse = jsonResponse.Substring(startIdx, endIdx - startIdx + 1);
            }

            // ʹ��Unity��JsonUtility����
            return JsonUtility.FromJson<WaiterDecision>(jsonResponse);
        }
        catch (Exception e)
        {
            Debug.LogError($"���������쳣: {e.Message}");
            throw;
        }
    }

    // ӭ�Ӳ���˵��ж�
    private IEnumerator PerformGreetingAndOrderAction(string dialogue)
    {
        Debug.Log($"[{npcName}] ��ʼִ��ӭ�Ӻ͵���ж�");

        if (RestaurantManager.greetingWaiter != this)
        {
            Debug.Log($"[{npcName}] ӭ�������ѱ���������Ա�ӹ�");
            yield break;
        }

        // ��ȡ����ڵȴ��Ĺ˿�
        CustomerNPC waitingCustomer = RestaurantManager.GetCustomerAtEntrance();

        if (waitingCustomer == null)
        {
            Debug.LogWarning($"[{npcName}] û���ҵ��ȴ��Ĺ˿�");
            RestaurantManager.SetCustomerAtEntrance(false);
            yield break;
        }

        // �ƶ������λ��
        yield return StartCoroutine(MoveToDestination(entrancePosition));
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // ����жԻ����ݣ���ʾ�Ի�
        if (!string.IsNullOrEmpty(dialogue))
        {
            ShowDialogueBubble(dialogue);
        }
        else
        {
            ShowDialogueBubble("��ӭ���٣��������");
        }

        AddMemory($"ӭ�ӹ˿ͣ�{dialogue}");

        // ֪ͨ�˿ͱ�ӭ��
        waitingCustomer.BeGreetedByWaiter(this, dialogue);

        yield return new WaitForSeconds(0.5f / TimeManager.Instance.timeScale);

        // �������
        Transform assignedTable = RestaurantManager.AssignTable();
        if (assignedTable == null)
        {
            ShowDialogueBubble("��Ǹ��Ŀǰû�п��������Ե�");
            AddMemory("û�п����ɷ���");

            // ֻ���Լ��ǵ�ǰӭ����ʱ���ȫ��״̬
            if (RestaurantManager.greetingWaiter == this)
            {
                RestaurantManager.SetCustomerAtEntrance(false);
                RestaurantManager.SetGreetingWaiter(null);
            }

            yield break;
        }

        // �����˿͵�����
        ShowDialogueBubble("��������������");
        AddMemory($"����˿�ǰ��{assignedTable.name}");

        // ����Ա���ƶ�������
        yield return StartCoroutine(MoveToDestination(assignedTable));

        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // �ù˿͸��浽����
        yield return waitingCustomer.MoveToPosition(assignedTable.position);

        // ���ڲ��ù˿ͽ���Ordering״̬
        waitingCustomer.BeSeatedByWaiter(this, assignedTable);

        // ȷ���˿͵�assignedTable������
        if (waitingCustomer.assignedTable == null)
        {
            waitingCustomer.assignedTable = assignedTable;
        }

        AddMemory($"�ɹ����˿Ͱ��ŵ�{assignedTable.name}");

        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);

        // ִ�е��
        yield return StartCoroutine(PerformTakingOrder(assignedTable, waitingCustomer));
        AddMemory("��ɵ㵥��������Ϣ��");

        // ������Ϣ��
        yield return StartCoroutine(MoveToDestination(restingPosition));

        // ֻ���Լ��ǵ�ǰӭ����ʱ���ȫ��״̬
        if (RestaurantManager.greetingWaiter == this)
        {
            RestaurantManager.SetCustomerAtEntrance(false);
            RestaurantManager.SetGreetingWaiter(null);
        }

        Debug.Log($"[{npcName}] ӭ�Ӻ͵���ж����");
    }

    // ����ж�
    private IEnumerator PerformTakingOrder(Transform table, CustomerNPC customer)
    {
        if (customer == null || customer.gameObject == null)
        {
            Debug.LogWarning($"[{npcName}] �˿��Ѳ����ڣ���ֹ����");
            yield break;
        }

        Debug.Log($"[{npcName}] ��ʼ��˷���");
        AddMemory($"Ϊ�˿͵��");

        // �ȴ��˿͵��
        yield return new WaitUntil(() => !string.IsNullOrEmpty(customer.GetOrderedFood()));

        string orderedDish = customer.GetOrderedFood();

        yield return new WaitUntil(() => !customer.isBeingServed);

        RestaurantManager.AddOrder(customer, this, table, orderedDish);
        Debug.Log($"AddOrderִ�����");
        AddMemory($"���Ϊ�˿͵�ˣ�������ȷ��");

        // �ȴ��˿�״̬����
        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);

        if (customer != null)
        {
            customer.ReleaseWaiter();
            Debug.Log($"[{npcName}] �ͷŹ˿� {customer.customerName}");
        }

        Debug.Log($"[{npcName}] ��˷������");
    }

    // �ϲ��ж�
    private IEnumerator PerformServingAction()
    {
        Debug.Log($"[{npcName}] ��ʼִ���ϲ��ж�");

        // ȷ���������������µ�
        CheckForNewOrders();

        if (orderQueue.Count == 0)
        {
            Debug.LogWarning($"[{npcName}] û���ҵ��κδ�����Ķ�����������Ϣ��");
            // �ƶ�����Ϣ��
            yield return StartCoroutine(MoveToDestination(restingPosition));
            yield break;
        }

        RestaurantManager.Order currentOrder = orderQueue.Peek();
        if (currentOrder.customer == null || currentOrder.customer.gameObject == null)
        {
            Debug.Log($"[{npcName}] ���� #{currentOrder.orderId} �Ĺ˿����뿪��ȡ���ϲ�");
            orderQueue.Dequeue();
            RestaurantManager.PickupOrder(currentOrder.orderId);
            yield break;
        }

        CustomerNPC assignedCustomer = currentOrder.customer;
        if (currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Leaving ||
            currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Paying ||
            currentOrder.customer.GetCurrentState() == CustomerNPC.CustomerState.Eating)
        {
            Debug.Log($"[{npcName}] �˿����뿪�������òͣ�ȡ���ϲ˶��� #{currentOrder.orderId}");
            orderQueue.Dequeue();
            RestaurantManager.PickupOrder(currentOrder.orderId);
            yield break;
        }

        Debug.Log($"[{npcName}] ��ʼ�ϲ˷��� - ����#{currentOrder.orderId}");
        AddMemory($"ǰ������ȡ�ͣ�{currentOrder.dishName}");

        // ��ȥ����ȡ��
        yield return StartCoroutine(MoveToDestination(kitchenPosition));
        yield return new WaitForSeconds(2f / TimeManager.Instance.timeScale);

        // ֪ͨ���������������ѱ�ȡ��
        RestaurantManager.PickupOrder(currentOrder.orderId);
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // �ٵ���Ӧ����
        yield return StartCoroutine(MoveToDestination(currentOrder.table));

        ShowDialogueBubble($"����{currentOrder.dishName}���ˣ�������");
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;

        // ֪ͨ�˿Ϳ�ʼ�ò�
        if (assignedCustomer != null)
        {
            assignedCustomer.currentState = CustomerNPC.CustomerState.Eating;
            AddMemory($"Ϊ{assignedCustomer.customerName}����{currentOrder.dishName}");
        }

        // �Ӷ������Ƴ��Ѵ���Ķ���
        orderQueue.Dequeue();

        Debug.Log($"[{npcName}] �ϲ��ж����");
    }
    // �ϲ�
    IEnumerator PerformServingFood(DailyActivity activity)
    {
        CheckForNewOrders();
        if (orderQueue.Count == 0)
        {
            // ���Դ�RestaurantManager��ȡ����
            var readyOrders = RestaurantManager.GetReadyOrders();
            var myOrders = readyOrders.Where(o => o.waiter == this).ToList();

            if (myOrders.Count > 0)
            {
                Debug.Log($"[{npcName}] ��RestaurantManager�ҵ�{myOrders.Count}���������������");
                foreach (var order in myOrders)
                {
                    orderQueue.Enqueue(order);
                }
            }
            else
            {
                Debug.LogWarning($"[{npcName}] û���ҵ��κδ�����Ķ�����������Ϣ��");
                waiterState = WaiterState.Idle;

                // �ƶ�����Ϣ��
                yield return StartCoroutine(MoveToDestination(restingPosition));
                yield break;
            }
        }
        RestaurantManager.Order currentOrder = orderQueue.Peek();
        if (currentOrder.customer == null || currentOrder.customer.gameObject == null)
        {
            Debug.Log($"[{npcName}] ���� #{currentOrder.orderId} �Ĺ˿����뿪��ȡ���ϲ�");
            orderQueue.Dequeue();

            // ֪ͨRestaurantManager�����������
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
            Debug.Log($"[{npcName}] �˿����뿪��ȡ���ϲ˶��� #{currentOrder.orderId}");
            orderQueue.Dequeue();
            RestaurantManager.PickupOrder(currentOrder.orderId);
            yield break;
        }
        Debug.Log($"[{npcName}] ��ʼ�ϲ˷��� - ����#{currentOrder.orderId}");
        AddMemory($"ǰ������ȡ�ͣ�{currentOrder.dishName}");

        // ��ȥ����ȡ��
        yield return StartCoroutine(MoveToDestination(kitchenPosition));
        yield return new WaitForSeconds(2f / TimeManager.Instance.timeScale);

        // ֪ͨ���������������ѱ�ȡ��
        RestaurantManager.PickupOrder(currentOrder.orderId);
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;
        // �ٵ���Ӧ����
        yield return StartCoroutine(MoveToDestination(currentOrder.table));

        ShowDialogueBubble($"����{currentOrder.dishName}���ˣ�������");
        statusUI.UpdateEnergy(energy, energy - 5);
        energy -= 5;
        EffortPoint += 1;
        // ֪ͨ�˿Ϳ�ʼ�ò�
        if (assignedCustomer != null)
        {
            assignedCustomer.currentState = CustomerNPC.CustomerState.Eating;
            AddMemory($"Ϊ{assignedCustomer.customerName}����{currentOrder.dishName}");
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
            // �����ظ����
            if (!orderQueue.Any(o => o.orderId == order.orderId))
            {
                orderQueue.Enqueue(order);
                Debug.Log($"[{npcName}] ����¶���������: #{order.orderId}");
            }
        }
    }


    // ���ն������֪ͨ
    private Queue<RestaurantManager.Order> orderQueue = new Queue<RestaurantManager.Order>();
    public void NotifyOrderReady(RestaurantManager.Order order)
    {
        // �������������
        orderQueue.Enqueue(order);
        AddMemory($"�յ�֪ͨ������#{order.orderId}({order.dishName})����ɣ��������");

        // �������� ProcessOrderQueue��������������ϵͳ
        // ����ϵͳ������һ�ξ���ʱ��⵽������ѡ���ϲ�
    }


    // �������д�����
    private IEnumerator ProcessOrderQueue()
    {
        while (orderQueue.Count > 0)
        {
            RestaurantManager.Order nextOrder = orderQueue.Peek();

            DailyActivity servingActivity = new DailyActivity(
                TimeManager.Instance.CurrentHour,
                TimeManager.Instance.CurrentMinute,
                5, // 5�����ϲ�ʱ��
                $"�ϲˣ�{nextOrder.dishName}",
                nextOrder.table
            );

            currentActivity = servingActivity;
            isExecutingActivity = true;

            // ִ���ϲ˻
            yield return StartCoroutine(PerformServingFood(servingActivity));

            // ��ɺ�Ӷ������Ƴ�
            if (orderQueue.Count > 0 && orderQueue.Peek().orderId == nextOrder.orderId)
            {
                orderQueue.Dequeue();
            }
        }
    }

    // ���
    IEnumerator PerformCleaning(DailyActivity activity)
    {
        Debug.Log($"[{npcName}] ��ʼ��๤��");
        AddMemory($"�������");

        yield return new WaitForSeconds(5f / TimeManager.Instance.timeScale);

        waiterState = WaiterState.Idle;
    }

    IEnumerator MoveToDestination(Transform destination)
    {
        waiterState = WaiterState.MovingToDestination;
        currentDestination = destination;

        while (Vector2.Distance(transform.position, destination.position) > destinationThreshold)
        {
            // ʹ��MoveTowards������λ��
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
        // ʹ�� GetDayMinutes ��ȡ�����ڵķ�����
        int currentDayMinutes = TimeManager.Instance.GetDayMinutes();
        int currentDay = TimeManager.Instance.GetDayCount();

        // ����Ƕ�̬����ģʽ
        if (useDynamicDecision && dynamicActivityEndMinutes >= 0)
        {
            // ����Ƿ����
            if (dynamicActivityStartDay >= 0 && currentDay > dynamicActivityStartDay)
            {
                // �Ѿ����˵ڶ��죬��Ҫ��������
                int minutesSinceStart = (currentDay - dynamicActivityStartDay) * 24 * 60 + currentDayMinutes;
                //int activityStartMinutes = dynamicActivityEndMinutes - currentActivity.durationMinutes;

                // ����Ӧ���Ѿ�����
                if (minutesSinceStart >= currentActivity.durationMinutes)
                {
                    return false;
                }
            }
            else if (currentDay == dynamicActivityStartDay)
            {
                // ͬһ���ڣ�ֱ�ӱȽ�
                return currentDayMinutes < (dynamicActivityEndMinutes % (24 * 60));
            }

            return true;
        }

        // ԭ�еĹ̶��ճ̼���߼�
        int activityStartMinutes = activity.startHour * 60 + activity.startMinute;
        int activityEndMinutes = activityStartMinutes + activity.durationMinutes;

        if (activityEndMinutes > 24 * 60)
        {
            // �����
            activityEndMinutes = activityEndMinutes % (24 * 60);
            return currentDayMinutes >= activityStartMinutes || currentDayMinutes < activityEndMinutes;
        }
        else
        {
            return currentDayMinutes >= activityStartMinutes && currentDayMinutes < activityEndMinutes;
        }
    }


    IEnumerator PerformResting(DailyActivity activity) // ��ʱ��resting��idle�ϲ��ˣ�idle����Ҫ�ٲ���
    {
        // ����Ƿ�Ӧ��ִ��
        if (!isExecutingActivity)
        {
            yield break;
        }
        int startDay = TimeManager.Instance.GetDayCount();
        int startDayMinutes = TimeManager.Instance.GetDayMinutes();
        int startTotalMinutes = TimeManager.Instance.GetTotalMinutes();

        float hours = activity.durationMinutes / 60f;
        Debug.Log($"[{npcName}] ===== ��ʼ��Ϣ =====");
        Debug.Log($"[{npcName}] ��ʼ״̬ - ����: {energy}/{maxEnergy}, ľ��: {wood}");
        //int checkCount = 0;
        yield return StartCoroutine(MoveToDestination(restingPosition));

        // ��¼��һ��Сʱ�����ڼ��Сʱ�仯
        int lastHour = TimeManager.Instance.CurrentHour;
        int lastTime = TimeManager.Instance.GetTotalMinutes();
        int hoursRested = 0;
        int debugCounter = 0;
        while (IsStillInActivityTime(activity) && isExecutingActivity)
        {
            waiterState = WaiterState.Idle;
            int currentHour = TimeManager.Instance.CurrentHour;
            int currentTime = TimeManager.Instance.GetTotalMinutes();
            if (currentTime - lastTime>=10) //ÿһ��ʮ������Ϣ�ظ�10�㣬�����޸�
            {
                lastTime = currentTime;

                if (energy < maxEnergy)
                {
                    int energyBefore = energy;
                    energy += 10;
                    statusUI.UpdateEnergy(energyBefore, energy);

                    Debug.Log($"[{npcName}] ��ɵ�{hoursRested}����ϢСʱ - ����: {energy}/{maxEnergy}");

                }
                else
                {
                    Debug.LogWarning($"[{npcName}] ��������");
                    // ����ѡ���Ƿ�Ҫ��ǰ��������
                    // break;
                }


            }
            debugCounter++;
            if (debugCounter % 20 == 0)
            {
                int currentTotalMinutes = TimeManager.Instance.GetTotalMinutes();
                int elapsedMinutes = currentTotalMinutes - startTotalMinutes;

                //Debug.Log($"[{npcName}] ��Ϣ��... ����Ϣ{elapsedMinutes}����");
                //Debug.Log($"  isExecutingActivity: {isExecutingActivity}");
                //Debug.Log($"  currentRoutine: {currentRoutine}");
            }

            yield return new WaitForSeconds(0.5f / TimeManager.Instance.timeScale);
        }

        int endTotalMinutes = TimeManager.Instance.GetTotalMinutes();
        int actualDuration = endTotalMinutes - startTotalMinutes;

        //Debug.Log($"[{npcName}] ===== ��Ϣ���� =====");
        //Debug.Log($"[{npcName}] ʵ�ʳ���: {actualDuration}���ӣ����{hoursRested}����ϢСʱ");
        //Debug.Log($"[{npcName}] ����״̬ - ����: {energy}/{maxEnergy}, ľ��: {wood}");
        string memoryTemp2 = $"[{npcName}]��Ϣ��{actualDuration}���ӣ�����: {energy}/{maxEnergy}";//get����Ҳ���Ը�Ϊֱ����npc���е�ȡ
        AddMemory(memoryTemp2);


        waiterState = WaiterState.Idle;
    }

    #endregion


    #region �������
    public void AddMemory(string memoryContent) //ͨ�ã��������ƶ��ճ̱���¼�ƶ��������Ի�����������д��memoryContent��Ȼ��ӽ�����
    {
        // ��ȡ��ǰ��Ϸʱ��
        string timestamp = $"{TimeManager.Instance.CurrentHour:D2}:{TimeManager.Instance.CurrentMinute:D2}";
        // ��ʽ��������Ŀ
        string formattedMemory = $"[{timestamp}] {memoryContent}";
        // ��ӵ������б�
        memoryList.Add(formattedMemory);
        // ����Ƿ񳬳���������
        if (memoryList.Count > MAX_MEMORY_COUNT)
        {
            // �Ƴ�����ļ��䣨�����б��С��
            memoryList.RemoveAt(0);
        }
        //Debug.Log($"{npcName}���¼���: {formattedMemory}");
    }


    public void AddMemory_resource(string memoryContent)  // ��С�ѡ������йص���Դ���䣬�۽��ڲ���������
    {
        // ��ȡ��ǰ��Ϸʱ��
        string timestamp = $"{TimeManager.Instance.CurrentHour:D2}:{TimeManager.Instance.CurrentMinute:D2}";
        // ��ʽ��������Ŀ
        string formattedMemory = $"[{timestamp}] {memoryContent}";
        // ��ӵ������б�
        memoryList_resouce.Add(formattedMemory);
        // ����Ƿ񳬳���������
        if (memoryList_resouce.Count > maxResourceMemories)
        {
            // �Ƴ�����ļ��䣨�����б��С��
            memoryList.RemoveAt(0);
        }
        //Debug.Log($"{npcName}���¼���: {formattedMemory}");
    }
    public List<string> GetAllMemories() // ��ȡ���䣬����MemoryManager�����txt
    {
        return new List<string>(memoryList);
    }
    public void ClearDailyMemories() // ��յ��ռ��䡣���ǿ�����һ�����ڼ۸���Դ��ֵ��ժҪ
    {
        memoryList.Clear();
        Debug.Log($"{npcName}�ļ�������գ�׼���µ�һ��");
    }


    public IEnumerator GenerateDailyReflection(System.Action<string> onResponse)
    {
        if (memoryList.Count == 0)
        {
            onResponse?.Invoke("����ûʲô�ر�����鷢����");
            yield break;
        }

        // ����ϵͳ��ʾ
        string systemPrompt = $@"����һ����������Ա{npcName}������ݽ���Ĺ���������з�˼�ܽᡣ
�Ը�{personality}
�������£�{backgroundStory}
��˼Ҫ��
1. �õ�һ�˳�˼�����ܽ����ľ��������ܺ�ѧϰ��
2. ������ˣ�1-2�仰
3. ���ָ�����кͳɳ�����������Ը��ص�
4. ���ھ����¼�����˼��
5. ʹ����Ȼ�Ŀ��ﻯ���";

        // �������б�ת��Ϊ�ַ���
        string memoryContext = string.Join("\n",memoryList);
        // �����û���ʾ
        string userPrompt = $"���������Ĺ�����¼��\n{memoryContext}\n\n�������Щ�������з�˼��";

        yield return AzureOpenAIManager.Instance.SendDialogueRequest(systemPrompt, userPrompt, 150, 0.8f, onResponse);
    }


    public void TriggerDailyReflection()
    {
        if (memoryList.Count > 0)
        {
            string reflection="";
            StartCoroutine(GenerateDailyReflection(reflection =>
            {
                string reflectionMemory = $"���շ�˼: {reflection}";
                AddMemory(reflectionMemory);
                SaveMemory(); //��˼��һ�������ʱ�򣬼ȱ�����䣬Ҳ���淴˼��

            }));
            
            Debug.Log($"{npcName}�����ɱ��շ�˼: {reflection}");
            
        }
        else
        {
            Debug.Log($"����Ϊ��");
        }
    }
    public void SaveMemory()
    {
        // ��ȡ�򴴽�MemoryManagerʵ��
        MemoryManager memoryManager = FindObjectOfType<MemoryManager>();
        if (memoryManager != null)
        {
            memoryManager.SaveMemoryToFile(this);
        }
        else
        {
            Debug.LogError("�Ҳ���MemoryManagerʵ��");
        }
    }



    #endregion

    #endregion

    #region****************************************NPC_UI���****************************************

    private void InitializeStatusUI()
    {
        if (valueCanvasPrefab == null)
        {
            Debug.LogError($"NPC {npcName} ��״̬UIԤ����δ����!");
            return;
        }

        // ����UIʵ��
        GameObject uiInstance = Instantiate(valueCanvasPrefab, transform.position, Quaternion.identity);

        // ��ȡNPCStatusUI���
        statusUI = uiInstance.GetComponent<NPCStatusUI>();

        if (statusUI == null)
        {
            Debug.LogError("���ɵ�UIԤ������û��NPCStatusUI���!");
            Destroy(uiInstance);
            return;
        }

        // ����UI��NPC����
        statusUI.npc = this;

        // ��ʼ��UI�������ı��ȣ�
        statusUI.InitializeUI();

        // ȷ��UI��ʼʱ�����ص�
        statusUI.HideUI();
    }

    private void HandleClick()
    {
        Debug.Log($"�����NPC: {npcName}");

        if (statusUI != null)
        {
            statusUI.ToggleUI();
            Debug.Log($"UI״̬: {(statusUI.IsUIVisible ? "��ʾ" : "����")}");
        }
        else
        {
            Debug.LogError("statusUIΪ�գ�");
        }
    }
    #endregion





}
