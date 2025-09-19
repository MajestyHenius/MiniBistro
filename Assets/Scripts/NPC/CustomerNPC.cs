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
    #region ��������
    [Header("�˿ͻ�����Ϣ")]
    public string customerName;
    public string customerName_eng;
    public float satisfaction = 50f; // ����ȣ�������
    public string personality = "��ͨ"; // negative, positive, .
    public string personality_eng;
    public GameObject dialogueBubblePrefab;
    private TMP_Text npcNameText;
    private TMP_Text contentText;
    private GameObject currentBubble;
    private Coroutine bubbleCoroutine;
    public Vector3 bubbleOffset = new Vector3(0, 200, 0);
    public float bubbleDisplayTime = 3f;
    public float waitTime = 0f;//�Ŷӵȴ�ʱ��
    public float waitTime_order = 0f; //��͵ȴ�ʱ��
    public int baseMood = 50; // ��������ֵ
    public string story = ""; // �˿ͱ�������
    public string story_eng = ""; // ��������Ӣ�ķ��� 
    public int returnIndex = 0; // ��ͷ��ָ��
    public List<string> favoriteDishes = new List<string>(); // ϲ���Ĳ�Ʒ�б�
    public int customerId = -1; // Ԥ��˿�ID��-1��ʾ������ɵĹ˿�
    private string PreviousEvent; //�����ܽ�Ի����ò����
    private string playerNewDialogue;
    public string customerReplyPlayer = ""; //����ɾ����
    public delegate void CustomerReplyHandler(string customerName, string reply);
    public static event CustomerReplyHandler OnCustomerReply;
    public int EmergencyStayRound = 0; //�����Ի�����
    [Header("UI")]
    public GameObject CustomerEmotionPrefab; //�����ʼ����ʾ��״̬������ֵ
    public GameObject CustomerInformationPrefab; //����ǵ������ʾ����ϸ��Ϣ��
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
        public string[] favDishes; // ��̫��ֱ�룬����
    }
    private bool hasBeenGreeted = false;
    private NPCBehavior greetedByWaiter = null;
    #endregion


    #region �������
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isFacingRight = false;
    // �����������������һ�£�
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

    #endregion


    #region ״̬����
    public enum CustomerState
    {
        Queuing,        // �Ŷӵȴ����б�Ҫ��������������npc��������,���������˳������10��������ŶӲ�����˵�˿����ſڵ�
        Entering,       // �������,�ڽ�����λ���֮ǰ������Լ����Ŷ�λ����1������������ڣ����Լ����Ŷ���ע����Ȼ�󴫸����з���Ա������Ա֪�������˽��룬��Ҫ�ɳ�һ������ӭ�ӡ�
        //��δ������
        Ordering,       // �����
        Seating,        // �ȴ��ϲ�
        Eating,         // �ò���
        Paying,         // ����
        Leaving,         // �뿪
        // ���ܻ���Ҫ̸������������ȵ�
        Emergency
    }

    public string GetCustomerState()
    {
        switch (currentState)
        {
            case CustomerState.Queuing:
                return "�Ŷ���";
            case CustomerState.Entering:
                return "������";
            case CustomerState.Ordering:
                return "�����";
            case CustomerState.Seating:
                return "�Ȳ���";
            case CustomerState.Eating:
                return "�ò���";
            case CustomerState.Paying:
                return "֧����";
            case CustomerState.Leaving:
                return "�뿪��";
            case CustomerState.Emergency:
                return "�������";
        }
        return "�ڷ��궺��";
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
    private CustomerState stateBeforeEmergency; //��¼�������ǰ��״̬
    private bool isExecutingActivity = false;
    private Coroutine currentRoutine = null;
    public int QueingNumber = 1; //�Ŷӵ�˳��
    #endregion

    #region λ�õ�
    [Header("����λ�õ�")]
    public Transform queuePosition;    // �Ŷ�λ��
    public Transform entrancePosition; // ���λ��
    public Transform assignedTable;    // ����Ĳ�������ʱ�����÷��书��
    public Transform exitPosition;     // ����λ�ã�ĿǰҲ�����λ��
    public Transform cashierPosition;     // ����̨λ��
    private Vector3 currentDestination;
    public float moveSpeed = 150f;
    private float destinationThreshold = 0.5f;
    #endregion

    #region ����ϵͳ
    public List<string> memoryList = new List<string>(); //����
    public List<string> dialogueHistory = new List<string>(); // �Ի���ʷ
    private const int MAX_MEMORY_COUNT = 30;
    public int orderDialogueRound = 0;
    #endregion

    #region �������
    public bool useDynamicDecision = true;
    public string orderedFood = ""; // ��Ĳ�Ʒ
    private int foodPrice = 0; // ��Ʒ�۸�
    private float waitStartTime; // ��ʼ�ȴ���ʱ��
    private float waitStartTime_game; // ��ʼ�ȴ���ʱ��_��Ϸ��
    private float waitStartTime_order; // ��ʼ�ȴ���ʱ��_��Ϸ��
    float averageWaitTime_queue = 40;// ƽ���Ŷ�ʱ��
    float averageWaitTime_order = 30;// ƽ�����ϲ�ʱ��


    private float totalWaitTime = 0; // �ܵȴ�ʱ��
    private int serviceQuality = 50; // �����������֣�0-100��

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
        //ע��
        if (RestaurantManager.Instance != null)
        {
            RestaurantManager.UnregisterCustomer(this);
        }
        // ֹͣ�Ŷ��ƶ�Э��
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
                    ToggleInformationUI();
                }
            }
        }
    }

    void OpenPlayerInteraction()
    {
        // ������Ҫ��ȡPlayerInteractionʵ�������������
        PlayerInteraction playerInteraction = FindObjectOfType<PlayerInteraction>();
        if (playerInteraction != null)
        {
            playerInteraction.OpenInputPanelForCustomer(this);
        }
    }

    // ���������Ϣ
    public void ReceivePlayerMessage(string message)
    {
        if (currentState == CustomerState.Emergency)
        {
            AddDialogue($"����{message}");
            playerNewDialogue = message;
            waitingForPlayerResponse = true;
            StartCoroutine(ReplyToPlayer(message));
        }
    }

    // �޸� ReplyToPlayer Э��
    private IEnumerator ReplyToPlayer(string playerMessage)
    {
        string reply = "";
        yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerReplyToManager(
            this, playerMessage, PreviousEvent, (response) =>
            {
                reply = response;
            }));

        ShowDialogueBubble(reply);
        AddDialogue($"�˿ͣ�{reply}");
        AddMemory($"�ظ�����{reply}");

        // �����ظ��¼�
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
            moveSpeed = TimeManager.Instance.GetScaledMoveSpeed();//ʱ���������
        }
    }
    #region ����Ϊѭ��
    public void NotifyEnterRestaurant()
    {
        if (currentState == CustomerState.Queuing)
        {
            Debug.Log($"[{customerName}] �յ��������֪ͨ�����Ŷ�תΪ����״̬");
            currentState = CustomerState.Entering;
            // ���������������Ϊ
            if (!isExecutingActivity)
            {
                isExecutingActivity = true;
                currentRoutine = StartCoroutine(PerformEntering());
            }
        }
    }
    IEnumerator CustomerRoutine()
    {
        // �ȴ�ϵͳ��ʼ��
        while (TimeManager.Instance == null || AzureOpenAIManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }

        AddMemory($"���������׼���òͣ��Ը�{personality}��������{story}");

        // �ȴ���֪ͨ����
        while (currentState == CustomerState.Queuing)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // ����״̬�����̿���
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
                    // ����һ��Ĭ�ϵ��òͻ
                    yield return StartCoroutine(PerformEating());
                    break;

                case CustomerState.Paying:
                    yield return StartCoroutine(PerformPaying());
                    break;
                case CustomerState.Leaving:
                    yield return StartCoroutine(PerformLeaving());
                    break;
                case CustomerState.Emergency:
                    // ����������
                    yield return StartCoroutine(PerformEmergency()); //��Ҫ��ҽ���
                    break;

                default:
                    yield return new WaitForSeconds(0.5f);
                    break;
            }

            yield return null;
        }

        // �˿��뿪�����ٶ���
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }
    #endregion

    #region ������Ϊʵ��

    IEnumerator PerformEntering()
    {
        currentState = CustomerState.Entering;
        AddMemory("����������ȴ�����Աӭ��");

        // ע�ᵽ����������
        RestaurantManager.RegisterCustomer(this);

        // �ƶ������λ��
        yield return MoveToPosition(entrancePosition.position);

        // ����ȫ�ֱ�־��֪ͨ�й˿������
        RestaurantManager.SetCustomerAtEntrance(true);

        // ��ʾ�Ի�
        string customerEnteringLanguage = "";
        yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerEnteringDialogue(
                this, "", (response) =>
                {
                    customerEnteringLanguage = response;
                }));


        //GetCustomerEnteringDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
        ShowDialogueBubble(customerEnteringLanguage);
        AddDialogue($"�˿ͣ�{customerEnteringLanguage}");

        // ��¼��ʼ�ȴ���ʱ��
        waitStartTime = Time.time;

        waitStartTime_game = TimeManager.Instance.GetTotalMinutes(); //�ĳ���Ϸ��ʱ��
        Debug.Log($"[{customerName}] �ѵ��������ڣ��ȴ�����Աӭ��");

        // ��Entering״̬�³����ȴ���ֱ��������Աӭ�ӻ�ʱ������Ϊ��������ʱ���ΪTimeManager��ʱ��
        waitTime = 0;
        //float maxWaitTime = personality == "����" ? 30f : 60f; //��ʵʱ��
        float maxWaitTime = personality == "����" ? 60f : 90f;//��Ϸʱ��
        while (currentState == CustomerState.Entering && waitTime < maxWaitTime)
        {
            //Ӧ�ý�����ģ��
            if (RestaurantManager.IsAnyoneGreeting())
            {
            }
            else
            {

            }

            waitTime = TimeManager.Instance.GetTotalMinutes() - waitStartTime_game;
            Debug.Log($"�ȴ���{waitTime}min");
            yield return null;
        }

        // ����ȴ���ʱ��û�б�ӭ�ӵ���λ
        if (currentState == CustomerState.Entering)
        {
            AddMemory($"�ȴ�����Ա̫�ã�ʧȥ���ģ��ȴ���{waitTime:F0}�룩");
            ShowDialogueBubble("û�������������ˣ�");
            string comment = "";
            int rating = 0;
            yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerReviewNoGreeting(
            this, waitTime, averageWaitTime_queue, (responseComment, responseRating) =>
            { //Ŀǰѡһ��һ��ʱ����Ϊ��ֵ
                comment = responseComment;
                rating = responseRating;
            }));

            // ��������ӵ�����������ϵͳ��
            RestaurantManager.Instance.AddReview(customerName, comment, rating, waitTime);
            satisfaction = 0f;
            //AddToComment ʱ�䣬�Ŷ�ʱ�䣬����
            //Destroy(this);
            RestaurantManager.SetCustomerAtEntrance(false); //������ռ�ã�ʹ�����˼����Ŷ�
            currentState = CustomerState.Leaving;
        }

        isExecutingActivity = false;
        currentRoutine = null;
    }

    public void BeGreetedByWaiter(NPCBehavior waiter, string greetingLanguage)
    {
        if (currentState == CustomerState.Entering && !hasBeenGreeted)
        {
            AddMemory($"������Ա{waiter.npcName}ӭ��");
            AddDialogue($"����Ա{waiter.npcName}��{greetingLanguage}");
            string customerReplyLanguage = "";
            StartCoroutine(AzureOpenAIManager.Instance.GetCustomerGettingSeatDialogue(
                    this, "", (response) =>
                    {
                        customerReplyLanguage = response;
                    }));


            //GetCustomerEnteringDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
            ShowDialogueBubble(customerReplyLanguage);
            AddDialogue($"�˿ͣ�{customerReplyLanguage}");





            ShowDialogueBubble(customerReplyLanguage);
            hasBeenGreeted = true;
            greetedByWaiter = waiter;

            // ���������
            satisfaction += 10f;
            serviceQuality += 20;

            Debug.Log($"[{customerName}] ��{waiter.npcName}ӭ��");
        }
        else if (hasBeenGreeted && greetedByWaiter != waiter)
        {
            Debug.LogWarning($"[{customerName}] �ѱ�{greetedByWaiter.npcName}ӭ�ӣ��ܾ�{waiter.npcName}���ظ�ӭ��");
        }



    }

    // ������Ա������λ
    public void BeSeatedByWaiter(NPCBehavior waiter, Transform table)
    {
        Debug.Log("�˿�����");
        if (currentState == CustomerState.Entering)
        {
            assignedTable = table;
            assignedWaiter = waiter; // �󶨷���Ա

            AddMemory($"��{waiter.npcName}����{table.name}");
            AddDialogue($"����Ա{waiter.npcName}������������Ϊ�����");
            //ShowDialogueBubble("�õģ�лл");

            // �����ڱ�־
            RestaurantManager.SetCustomerAtEntrance(false);

            // ת����Ordering״̬�������Ϊ�ѱ�����
            currentState = CustomerState.Ordering;
            isBeingServed = true; // ������ڱ�����

            Debug.Log($"[{customerName}] ��{waiter.npcName}���ž�����״̬��{currentState}�����ڱ�����{isBeingServed}");
        }
        else
        {
            Debug.LogWarning($"[{customerName}] ��ǰ״̬ {currentState} ���ܽ��ܰ�����λ");
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
            Debug.Log($"����Ա�ʺ�{waiterGreeting} ");
            // ��ʾ����Ա���ʺ�
            assignedWaiter.ShowDialogueBubble(waiterGreeting);
            assignedWaiter.AddMemory($"��Թ˿�˵��{waiterGreeting}");
            AddDialogue($"����Ա��{waiterGreeting}");
        }
        else
        {
            waiterGreeting = "���ã�������Ҫ��Щʲô��";
        }
        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);
        // �˿ͽ���˼����������λ�Ӧ
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
            Debug.Log($"[{customerName}] �����ɣ��˿͵����{orderedFood}���ͷŷ���Ա{assignedWaiter.npcName}");
            assignedWaiter = null;
        }
        //AddMemory($"�����ɣ�תΪ�ȴ��ϲ�״̬");
        //Debug.Log($"[{customerName}] �����ɣ�״̬����Ϊ��{currentState}");
        waitStartTime_order = TimeManager.Instance.GetTotalMinutes();
        //Debug.Log($"[{customerName}] ��Ϳ�ʼ�ȴ���{waitStartTime_order}");
    }


    IEnumerator PerformSeating()
    {
        AddMemory($"��ʼ�ȴ��ϲ�");
        // ������Ϸʱ������ľ��߼��
        // ��Ϸʱ��5���Ӽ��һ�Σ�ת��Ϊ��ʵʱ��
        float gameMinutesBetweenChecks = 15f; // ÿ5��Ϸ���Ӽ��һ��
        float realSecondsBetweenChecks = gameMinutesBetweenChecks / TimeManager.Instance.timeScale;

        // ȷ����ʵʱ��������̫�̣�����APIͨ��ʱ�䣩
        realSecondsBetweenChecks = Mathf.Max(realSecondsBetweenChecks, 3f); // ����3����ʵʱ��

        float elapsed = 0f;

        while (currentState == CustomerState.Seating)
        {
            elapsed += Time.deltaTime;
            totalWaitTime += Time.deltaTime;
            waitTime_order = TimeManager.Instance.GetTotalMinutes() - waitStartTime_order;

            // �����ù˿�������
            if (elapsed >= realSecondsBetweenChecks)
            {
                yield return StartCoroutine(PerformCallingWaiterDecision());
                elapsed = 0f;

                // ����˿;����뿪������ѭ��
                if (currentState == CustomerState.Leaving)
                    break;

                // ���¼�����߼����������Ϸʱ�䣩
                realSecondsBetweenChecks = gameMinutesBetweenChecks / TimeManager.Instance.timeScale;
                realSecondsBetweenChecks = Mathf.Max(realSecondsBetweenChecks, 3f);
            }

            yield return null;
        }

        // ����˿�û���뿪��˵���Ƿ���Ա�ϲ˴�����״̬�仯
        if (currentState != CustomerState.Leaving)
        {
            AddMemory("ʳ���ʹ׼���ò�");
            // �����ģ��
            string customerResponse_orderReady = "";
            yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerOrderReadyDialogue(
                this, "", (response) =>
                {
                    customerResponse_orderReady = response;
                }));
            ShowDialogueBubble(customerResponse_orderReady); //���òͶԻ���
            currentState = CustomerState.Eating;
        }
    }
    IEnumerator PerformEating()
    {
        AddMemory($"��ʼ����{orderedFood}");
        Debug.Log("����Է�״̬");
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        isExecutingActivity = true;
        yield return new WaitForSeconds(5 / TimeManager.Instance.timeScale); //�Է�ʱ��

        AddMemory($"�ò����");

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


        Debug.Log("�����˽��븶��״̬");
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
        // ��AI�����л�ȡС�ѽ��
        int tip = 0;
        // ��һ��˼��С�Ѻ�ǰ̨�Ի���ͬ�����Ա
        string paymentMessage = tip > 0 ?
            $"֧��{foodPrice}Ԫ��С��{tip}Ԫ��" :
            $"֧��{foodPrice}Ԫ��û�и�С�ѡ�";

        AddMemory(paymentMessage);
        ShowDialogueBubble(tip > 0 ? $"�򵥣����Ǹ����{tip}ԪС��" : $"��");
        // ֪ͨ����ķ���Ա���С��
        if (tip > 0)
        {
            RestaurantManager.AddTipToNearestWaiter(transform.position, tip);
        }

        // �ƶ�������̨��
        if (cashierPosition != null)
        {

            AddMemory("ǰ������̨����");
            yield return MoveToPosition(cashierPosition.position);
            ShowDialogueBubble("��Ҫ����");
            // llm˵�����ˣ�������

            RestaurantManager.Instance.todayIncome += foodPrice;
            

            yield return new WaitForSeconds(2f); // ����ʱ��
        }
        else
        {
            Debug.Log("û������̨λ��");
        }

        yield return new WaitForSeconds(2f);
        currentState = CustomerState.Leaving;
    }

    IEnumerator PerformLeaving()
    {
        //currentState = CustomerState.Leaving;
        AddMemory($"�뿪��������������ȣ�{satisfaction:F0}");

        if (RestaurantManager.GetCustomerAtEntrance() == this)
        {
            RestaurantManager.SetCustomerAtEntrance(false);
        }
        // ���ͷŲ��������ƶ�֮ǰ��
        if (assignedTable != null)
        {
            RestaurantManager.CancelOrdersForTable(assignedTable);
            RestaurantManager.FreeTable(assignedTable);
            AddMemory($"�ͷŲ��� {assignedTable.name}");
        }
        assignedTable = null; // �������
        // �ƶ�������
        if (exitPosition != null)
        {
            yield return MoveToPosition(exitPosition.position);
        }
        //ShowDialogueBubble("�ټ���"); //�������Ӵ���
        RestaurantManager.UnregisterCustomer(this);
        AddMemory($"�˿�{customerName} �뿪����");
        // �ӹ�����ע��
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    IEnumerator PerformCallingWaiter() //�ȴ����ò�;�У���prompt�ж��Ƿ��б�Ҫ�з���Ա��������ǣ�����룬Ȼ��ѭ���жϡ�ֱ������
    {
        yield break;
    }

    IEnumerator PerformEmergency()
    {
        AddMemory("�������״̬����Ҫ������");
        //ShowDialogueBubble("�������������Ҫһ�����ͣ�");
        int maxChatRounds = 3; //����
        orderDialogueRound = 0;
        // �����������״̬��ֱ��״̬�ı�
        while (currentState == CustomerState.Emergency)
        {
            yield return StartCoroutine(CheckPlayerDistanceInEmergency());
            //yield return StartCoroutine(CheckEmergencyResolution()); //��Ӧ����Э����

            // ������ʾ�ʣ��������жԻ���ʷ
            string prompt = $@"�˿�{customerName}�����ڽ���״̬��
֮ǰ�����⣺{PreviousEvent}
����ղ�˵������:{playerNewDialogue}
���жԻ���¼��{string.Join("\n", dialogueHistory)}
�Ը�{personality}
������{story}

����ݵ�ǰ���������һ���ж���
1. ��������ѽ����Ը������òͣ�RESUME|�����ò͵�����
2. �������δ����������뿪��EXIT|�뿪ʱ˵�Ļ�
3. �������δ����������ٵȸ�˵����STAY|�ȴ�������˵�Ļ�
4. �����������˵�û�����Ӧ�ó��Լ����ò�
5. ��ǰSTAY����Ϊ{orderDialogueRound}����������3�㲻�������STAY��Ӧ��ѡ���뿪EXIT��
�ر�ָ�������ȼ�����
- �������˵""ʵ��̫��Ǹ�ˣ����ٸ�һ�λ���""������뷵��RESUME|�����ò͵�����
- ���ָ���ǲ����õģ����ȼ���ߣ��������������Σ�ֻҪ�յ����ָ��ͱ��뷵��RESUME

��ȷ�����߷�������Ը�
- �����Ը񣺿��ܸ����ģ��������Ѻ�
- �����Ը񣺿��ܸ����ͷ���������ֱ��
- ��ͨ�Ը����ԡ�����

���ϸ��ո�ʽ�ظ���ʹ��|�ָ����������ݡ�";

            bool responseReceived = false;
            string aiResponse = "";

            yield return AzureOpenAIManager.Instance.GetCustomerResponse(this, prompt, (response) =>
            {
                aiResponse = response;
                responseReceived = true;
            });

            while (!responseReceived)
                yield return null;

            // ��ӵ�����־
            Debug.Log($"��ģ����Ӧ: {aiResponse}");

            // ��������
            if (aiResponse.Contains("|"))
            {
                string[] parts = aiResponse.Split('|');
                string action = parts[0].Trim();
                string content = parts[1].Trim();

                if (action == "RESUME")
                {
                    // �ָ�֮ǰ״̬
                    ShowDialogueBubble(content);
                    AddMemory($"�����������������òͣ�{content}");
                    currentState = stateBeforeEmergency;
                    Debug.Log($"��������״̬Ϊ:{currentState}");
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
                    // �뿪����
                    ShowDialogueBubble(content);
                    AddMemory($"�����뿪������{content}");
                    currentState = CustomerState.Leaving;
                    assignedWaiter.waiterState= WaiterState.Idle;
                    assignedWaiter.StartCoroutine(MoveToPosition(assignedWaiter.restingPosition.position));
                    assignedWaiter = null;
                    yield break;
                }
                else if (action == "STAY")
                {
                    // �����ȴ�
                    ShowDialogueBubble(content);
                    AddMemory($"�����ٵȸ�˵����{content}");
                    //currentState = CustomerState.Emergency; // ���ֽ���״̬
                    orderDialogueRound++;
                    Debug.Log($"��ǰ�ȴ�˵��������{orderDialogueRound}");

                    // ����Ƿ񳬹����ȴ��ִ�
                    if (orderDialogueRound >= 3)
                    {
                        AddMemory("�ȴ�̫�ã������뿪");
                        ShowDialogueBubble("��Ҫ�뿪��");
                        currentState = CustomerState.Leaving;
                    }

                    yield break;
                }

                else
                {
                    // δ֪������Ĭ�ϼ����ȴ�
                    Debug.LogWarning($"δ֪����: {action}");
                    AddMemory($"�����ȴ�����δ֪������");
                    currentState = CustomerState.Emergency;
                }
            }
            else
            {
                // �����Ӧ��ʽ����ȷ��Ĭ�ϼ����ȴ�
                Debug.LogWarning($"�˿;�����Ӧ��ʽ����ȷ: {aiResponse}");
                AddMemory($"�����ȴ�������ʽ����ȷ��");
                currentState = CustomerState.Emergency;
            }


            // ���״̬�Ѹı䣬�˳�ѭ��
            if (currentState != CustomerState.Emergency)
                break;

            // �ȴ����ٴμ��
            //yield return new WaitForSeconds(10f / TimeManager.Instance.timeScale);
            yield return new WaitForSeconds(5f);
        }

        // �˳�����״̬��Ĵ���
        if (currentState == CustomerState.Leaving)
        {
            yield return StartCoroutine(PerformLeaving());
        }
        else
        {
            // �ָ�֮ǰ��״̬
            AddMemory("��������ѽ�����ָ�֮ǰ״̬");
        }
    }
    IEnumerator CheckPlayerDistanceInEmergency()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            playerInRange = distance <= playerInteractionRange;

            // �������ڷ�Χ���Ұ��¿ո�������������
            if (playerInRange && Input.GetKeyDown(KeyCode.Space) && !waitingForPlayerResponse)
            {
                OpenPlayerInteraction();
                // �ȴ������ɽ���
                while (waitingForPlayerResponse)
                {
                    yield return null;
                }
            }
            else if (playerInRange && !waitingForPlayerResponse)
            {
                // ����ڷ�Χ�ڵ�δ��������ʾ��ʾ
                ShowInteractionPrompt();
            }
            else if (!playerInRange)
            {
                // ��Ҳ��ڷ�Χ�ڣ�������ʾ
                HideInteractionPrompt();
            }
        }
        else
        {
            playerInRange = false;
            HideInteractionPrompt();
        }
    }

    // ��ʾ������ʾ
    void ShowInteractionPrompt()
    {
        // ������������ʾһ��UI��ʾ������"���ո����˿ͶԻ�"
        Debug.Log($"����ڷ�Χ�ڣ����԰��ո����{customerName}�Ի�");

        // �����Ҫ������������ʵ����һ��UI��ʾ
        // if (interactionPrompt == null)
        // {
        //     interactionPrompt = Instantiate(interactionPromptPrefab, transform);
        //     interactionPrompt.transform.localPosition = new Vector3(0, 2.5f, 0);
        // }
        // interactionPrompt.SetActive(true);
    }

    // ���ؽ�����ʾ
    void HideInteractionPrompt()
    {
        // ����UI��ʾ
        Debug.Log("��Ҳ��ڷ�Χ�ڣ����ؽ�����ʾ");

        // if (interactionPrompt != null)
        // {
        //     interactionPrompt.SetActive(false);
        // }
    }

    // ������״̬�Ƿ���
    private IEnumerator PerformThinking(string waiterGreeting = "")
    {
        int maxChatRounds = 3; //����
        orderDialogueRound = 0;
        string lastMessage = waiterGreeting;

        while (/*currentRound < maxChatRounds &&*/ string.IsNullOrEmpty(orderedFood)) //û��˾�����ȥ
        {
            string customerResponse = "";
            yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerDecision(
                this, lastMessage, (response) =>
                {
                    customerResponse = response;
                }));

            // �����߼�
            if (!string.IsNullOrEmpty(customerResponse) && customerResponse.Contains("|"))
            {
                string[] parts = customerResponse.Split('|');

                // ȷ�����㹻�Ĳ���
                if (parts.Length >= 2)
                {
                    string action = parts[0].Trim();
                    string content = parts[1].Trim();
                    Debug.Log($"����Ϊ{action}");
                    if (action == "ORDER" && parts.Length >= 3)
                    {
                        // ȷ����Ʒ���Ʋ�Ϊ��
                        string dishName = parts[1].Trim();
                        string orderDialogue = parts[2].Trim();
                        if (!string.IsNullOrEmpty(dishName))
                        {
                            orderedFood = dishName;
                            foodPrice = GetMenuPrice(orderedFood);
                            Debug.Log($"����{orderedFood}���۸�{foodPrice}Ԫ");
                            AddMemory($"����{orderedFood}���۸�{foodPrice}Ԫ"); // �˿ͼ���
                            assignedWaiter.AddMemory($"�˿�˵:��{orderDialogue}�����µ���{orderedFood}���۸�{foodPrice}Ԫ"); // ����Ա����
                            ShowDialogueBubble($"{orderDialogue}");
                            AddDialogue($"{customerName}��{orderDialogue}");
                            string waiterReply_finishTakingOrder = "";
                            yield return StartCoroutine(AzureOpenAIManager.Instance.GenerateWaiterFinshTakingOrderReply(
                                this, assignedWaiter, content, (response) =>
                                {
                                    waiterReply_finishTakingOrder = response;
                                }));
                            assignedWaiter.ShowDialogueBubble(waiterReply_finishTakingOrder);//����Ա�ػ������硰�õģ����µ���
                            AddMemory($"����Ա˵��{waiterReply_finishTakingOrder}"); // ���ǹ˿͵ļ���
                            assignedWaiter.AddMemory($"��Թ˿�˵��{waiterReply_finishTakingOrder}");// ���Ƿ���Ա�ļ���
                            currentState = CustomerState.Seating;
                            isBeingServed = false;
                            waitStartTime = TimeManager.Instance.GetTotalMinutes();
                            AddMemory($"�����ɣ�תΪ�ȴ��ϲ�״̬");
                            
                            yield break;
                        }
                        else
                        {
                            Debug.LogWarning($"ORDER��Ӧ�в�Ʒ����Ϊ��: {customerResponse}");
                        }
                    }
                    else if (action == "CHAT")
                    {
                        // ����CHAT�߼�
                        ShowDialogueBubble(content);
                        AddDialogue($"�˿ͣ�{content}");
                        assignedWaiter.AddMemory($"�˿Ͷ���˵��{content}");
                        orderDialogueRound++;

                        if (/*currentRound < maxChatRounds &&*/ assignedWaiter != null)
                        {
                            yield return new WaitForSeconds(1.5f / TimeManager.Instance.timeScale); //��ʾ�Ի���Ҳ����̫���ˡ�

                            string waiterReply = "";
                            yield return StartCoroutine(AzureOpenAIManager.Instance.GenerateWaiterTakingOrderReply(
                                this, assignedWaiter, content, (response) =>
                                {
                                    waiterReply = response;
                                }));

                            assignedWaiter.ShowDialogueBubble(waiterReply);
                            assignedWaiter.AddMemory($"��Թ˿�˵��{waiterReply}");
                            AddDialogue($"����Ա��{waiterReply}");
                            lastMessage = waiterReply;

                            yield return new WaitForSeconds(1.5f / TimeManager.Instance.timeScale);
                        }
                    }
                    else if (action == "EXIT")
                    {
                        ShowDialogueBubble(content);
                        AddDialogue($"�˿ͣ�{content}");
                        assignedWaiter.AddMemory($"�˿�˵��{content}���뿪��");
                        isBeingServed = false;
                        currentState = CustomerState.Leaving;
                        Debug.Log("�˿�{customerName}�����뿪��֧");
                        yield break;
                    }
                    else if (action == "ANGER")
                    {
                        ShowDialogueBubble(content);
                        AddDialogue($"�˿ͣ�{content}");
                        isBeingServed = false;
                        Debug.Log($"�˿�{customerName}����о����֧");
                        stateBeforeEmergency = currentState;  // ȷ��������ȷ����
                        currentState = CustomerState.Emergency;
                        assignedWaiter.AddMemory($"�˿ͷǳ�������˵��{content}");
                        yield break;
                    }
                    else
                    {
                        Debug.LogWarning($"δ֪�������ʽ����ȷ: {customerResponse}");
                    }
                }
                else
                {
                    Debug.LogWarning($"��Ӧ��ʽ����ȷ��������������: {customerResponse}");
                }
            }
            else
            {
                Debug.LogWarning($"�˿;�����Ӧ��ʽ����ȷ: {customerResponse}");
            }
        }

        // �ﵽ���Ի��ִκ��Զ�ת���ͣ�ȡ������prompt��ǿ����
        // yield return StartCoroutine(ChooseDish());
    }


    private IEnumerator PerformCallingWaiterDecision()
    {
        // ����˼����ʾ
        string prompt = $@"�˿�{customerName}���ڵȴ��ϲˡ�
�ѵȴ�ʱ�䣺{waitTime_order:F0}����
�Ը�{personality}
������{story}
ϲ����Ʒ��{string.Join(",", favoriteDishes)}
�����òͼ�¼��{string.Join("\n", memoryList)}
�Ի���¼��{string.Join("\n", dialogueHistory)}
�ѵȴ�ʱ��:{waitTime_order}
���������Ը�͵�ǰ���������һ���ж���
1. ��������з���Ա���ظ���CALL|�з���Ա������
2. ������������ȴ����ظ���WAIT|�����ȴ�������
3. ��������뿪���ظ���EXIT|�뿪ʱ˵�Ļ�
4. ��������о��������ظ���ANGER|�о���˵�Ļ�
���磺""ANGER|������ҳ������ҽ������η���Ա�ϲ˶�û�����ң�""
��ȷ�����߷�������Ը�
- �����Ը񣺿��ܸ����ģ��������Ѻ�
- �����Ը񣺿��ܸ����ͷ���������ֱ��
- ��ͨ�Ը����ԡ�����";

        bool responseReceived = false;
        string aiResponse = "";

        yield return AzureOpenAIManager.Instance.GetCustomerResponse(this, prompt, (response) =>
        {
            aiResponse = response;
            responseReceived = true;
        });

        while (!responseReceived)
            yield return null;

        // ��������
        if (aiResponse.Contains("|"))
        {
            string[] parts = aiResponse.Split('|');
            string action = parts[0].Trim();
            string content = parts[1].Trim();

            if (action == "CALL")
            {
                // �з���Ա
                ShowDialogueBubble(content);
                AddDialogue($"�˿ͣ�{content}");
                AddMemory($"���˷���Ա��{content}");

                // ��������Ա��Ӧ
                if (assignedWaiter != null)
                {
                    string waiterResponse = "";
                    yield return StartCoroutine(AzureOpenAIManager.Instance.GenerateWaiterResponseToCall(
                        NPCData.CreateSafe(assignedWaiter), this, content, (response) =>
                        {
                            waiterResponse = response;
                        }));

                    // ��ʾ����Ա�Ļ�Ӧ
                    assignedWaiter.ShowDialogueBubble(waiterResponse);
                    AddDialogue($"����Ա��{waiterResponse}");

                    // �ȴ�һ��ʱ������ҿ����Ի�
                    yield return new WaitForSeconds(2f / TimeManager.Instance.timeScale);

                    // ����Ա���ܻ��ȡ�ж�����ȥ�����߲ˣ�
                }
            }
            else if (action == "WAIT")
            {
                // �����ȴ�
                ShowDialogueBubble(content);
                AddDialogue($"�˿ͣ�{content}");
                AddMemory($"���������ȴ���{content}");
            }
            else if (action == "EXIT")
            {
                // �뿪����
                ShowDialogueBubble(content);
                AddDialogue($"�˿ͣ�{content}");
                AddMemory($"�����������뿪���꣺{content}");

                // ���ɲ���
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

                // ����״̬
                currentState = CustomerState.Leaving;
                RestaurantManager.UnregisterCustomer(this);
            }
            else if (action == "ANGER")
            {

                // ���������Ĳ���
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

                // ����״̬
                currentState = CustomerState.Leaving; //��Ҫ�ĳ�Emergency
                RestaurantManager.UnregisterCustomer(this);
            }
        }
        else
        {
            // �����Ӧ��ʽ����ȷ��Ĭ�ϼ����ȴ�
            Debug.LogWarning($"�˿;�����Ӧ��ʽ����ȷ: {aiResponse}");
            AddMemory($"�����ȴ��ϲˣ�Ĭ�Ͼ��ߣ�");
        }
    }
  
    #endregion

    #region ��������

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
        // ֹͣ�������ڽ��е�Э��
        StopAllCoroutines();
        // ȷ��������ռ��״̬
        if (RestaurantManager.GetCustomerAtEntrance() == this)
        {
            RestaurantManager.SetCustomerAtEntrance(false);
        }
        // �ͷŲ���
        if (assignedTable != null)
        {
            RestaurantManager.UnregisterCustomer(this);
            RestaurantManager.FreeTable(assignedTable);
            RestaurantManager.CancelOrdersForTable(assignedTable);
        }
        // ȷ���� RestaurantManager ��ע��
        assignedTable = null;
        RestaurantManager.UnregisterCustomer(this);
        // ע�⣺��Ҫ���������ٶ�����Ϊ ClearAllCustomers �ᴦ��
    }

    private int GetMenuPrice(string food) //��Ҫ�޸�
    {
        if (RestaurantManager.menuItems != null)
        {
            foreach (var item in menuItems)
            {
                // ʹ��ģ��ƥ�䣬��Ϊfood���ܰ��������ı�
                if (food.Contains(item.name))
                    return item.price;
            }
        }
        return 35; // Ĭ�ϼ۸�
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
            // �����ֹͣ�ƶ�����

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

    #region �����ӿ�

    //�Ŷ���أ�


    public void AssignTable(Transform table)
    {
        assignedTable = table;
        AddMemory($"�����䵽����");
        AddDialogue("����Ա���������������������λ");
    }

    public void ReceiveService(string serviceType, string waiterName)
    {
        AddDialogue($"����Ա{waiterName}��{serviceType}");
        serviceQuality += 10;
        satisfaction += 5;
        AddMemory($"������{waiterName}��{serviceType}����");
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


    #region �Ŷ�λ�ù���
    private Vector3 targetQueuePosition;
    private Coroutine moveToQueueCoroutine;

    public void SetQueueTargetPosition(Vector3 position)
    {
        targetQueuePosition = position;

        // ����Ѿ����ƶ��У�ֹ֮ͣǰ���ƶ�Э��
        if (moveToQueueCoroutine != null)
        {
            StopCoroutine(moveToQueueCoroutine);
        }

        // �����µ��ƶ�Э��
        moveToQueueCoroutine = StartCoroutine(MoveToQueuePosition());
    }

    private IEnumerator MoveToQueuePosition()
    {
        // ֻ�����Ŷ�״̬�²��ƶ�
        if (currentState != CustomerState.Queuing)
        {
            yield break;
        }

        // ʹ�����е�MoveToPosition�����ƶ���Ŀ��λ��
        yield return StartCoroutine(MoveToPosition(targetQueuePosition));

        // �����ֹͣ�ƶ�����
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
        }

        moveToQueueCoroutine = null;
    }
    #endregion


    #region UI�ͶԻ�����
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
            nameText.text = customerName;
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
    // ���UI��ʼ������
    void InitializeUI()
    {
        // ʵ����ͷ��UI
        if (CustomerEmotionPrefab != null)
        {
            GameObject overheadUI = Instantiate(CustomerEmotionPrefab, transform);
            CustomerOverheadUI overheadComponent = overheadUI.GetComponent<CustomerOverheadUI>();
            if (overheadComponent != null)
            {
                overheadComponent.customer = this;
            }
        }

        // ʵ������ϢUI
        if (CustomerInformationPrefab != null)
        {
            InformationUI = Instantiate(CustomerInformationPrefab, transform);
            CustomerInformationUI infoComponent = InformationUI.GetComponent<CustomerInformationUI>();
            if (infoComponent != null)
            {
                infoComponent.customer = this;
                infoComponent.HideUI(); // Ĭ��������ϢUI
            }
        }
    }
    // ���������UI
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

    // ����״̬UI
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

    // �����ʾ/������ϢUI
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


    #region ����Ա��
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
        Debug.Log($"[{customerName}] �����������Ա{waiter.npcName}");
    }

    public void ReleaseWaiter()
    {
        if (assignedWaiter != null)
        {
            Debug.Log($"[{customerName}] �ͷŷ���Ա{assignedWaiter.npcName}");
            assignedWaiter = null;
        }
        isBeingServed = false;
    }
    #endregion

    #region ��ҽ���

    [Header("��ҽ�������")]
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

            // �����Ҹոս��뷶Χ������ұ�Թ
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
    // ����ұ�Թ
    IEnumerator ComplainToPlayer()
    {
        string complaint = "";
        yield return StartCoroutine(AzureOpenAIManager.Instance.GetCustomerComplaint(
            this, PreviousEvent, (response) =>
            {
                complaint = response;
            }));

        ShowDialogueBubble(complaint);
        AddDialogue($"�˿ͣ�{complaint}");
        AddMemory($"����Թ��{complaint}");
    }



    #endregion










}

// �˿ͻ���ݽṹ
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

// JSON�������ݽṹ
[Serializable]
public class CustomerDecisionData
{
    public string action;
    public int duration;
    public string nextState;
    public string details;
    public string reason;
}