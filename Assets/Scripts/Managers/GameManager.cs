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
    [Header("��Ϸģʽ����")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera; // ��Ϊ�������
    [SerializeField] private CinemachineBrain cinemachineBrain; // ��ѡ�����ڻ�ȡBrain���
    [SerializeField] GameObject playerObject;
    [SerializeField] Transform player;
    private PlayerController playerController;
    [SerializeField] private TimeManager timeManager;
    private int hasTriggeredNegotiation = 0; // �Ѿ�������̸�д���
    [SerializeField] public int negotiationGap = 2; //ÿx�췢��һ�ν�������
    [Header("���������")]
    [SerializeField] private float normalCameraSize = 5f;
    [SerializeField] private float overviewCameraSize = 15f;
    [SerializeField] private float cameraTransitionDuration = 0.5f;
    [SerializeField] private Vector3 normalCameraOffset = new Vector3(0, 0, -10);
    [SerializeField] private Vector3 overviewCameraOffset = new Vector3(0, 5, -20);

    // ���Cinemachine�������
    [Header("Cinemachine����")]
    [SerializeField] private bool useOrthographic = true; // �Ƿ�ʹ������ͶӰ
    [SerializeField] private float normalFieldOfView = 60f; // ͸��ͶӰʱ������FOV
    [SerializeField] private float overviewFieldOfView = 90f; // ͸��ͶӰʱ��ȫ��FOV

    [Header("UI�������")]
    [SerializeField] private GameObject controlPanel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button normalSpeedButton;
    [SerializeField] private Button fastSpeedButton;
    [SerializeField] private Button veryFastSpeedButton;
    [SerializeField] private TMP_Text currentSpeedText;
    [SerializeField] private Slider speedSlider;

    [SerializeField] private GameObject freeActivityUI; // "���ɻ"��UI���л��׶�
    [SerializeField] private TMP_Text freeActivityText; // ��ʾ���ֵ�Text���
    [SerializeField] private float uiDisplayDuration = 2f; // UI��ʾ����ʱ��
    [SerializeField] private float uiFadeInDuration = 0.5f; // ����ʱ��
    [SerializeField] private float uiFadeOutDuration = 0.5f; // ����ʱ��
    [SerializeField] private DialogueUIManager dialogutUI; // ̸�жԻ���ص�UI
    [Header("NPC����")]
    [SerializeField] private List<NPCBehavior> allNPCs = new List<NPCBehavior>();
    [SerializeField] private GameObject loadingUI; // ��ʾ�����е�UI
    [SerializeField] private TMP_Text loadingText; // ���ؽ�������
    [SerializeField] private GameObject waiterSelectionUI; // ��ӷ���Աѡ��UI����

    



    private int totalNPCs = 0;
    private int readyNPCs = 0;
    private bool gameStarted = false;

    [Header("ʱ���������")]
    [SerializeField] private float normalTimeScale = 2f;
    [SerializeField] private float fastTimeScale = 10f;
    [SerializeField] private float veryFastTimeScale = 60f;

    [Header("�˵���ʾ����")]
    [SerializeField] private GameObject menuDisplayPanel; // �˵���ʾ���
    [SerializeField] private GameObject menuItemPrefab; // �˵���Ԥ����
    [SerializeField] private Transform menuContentParent; // �˵������
    [SerializeField] private ScrollRect menuScrollView; // ������ͼ
    [SerializeField] private TMP_Text menuTitleText; // �˵������ı�
    [SerializeField] private Button toggleMenuButton; // �л��˵���ʾ/���صİ�ť
    [SerializeField] private TMP_Text toggleMenuButtonText; // ��ť�ı����
    private bool isMenuDisplayed = false; // ���ٲ˵���ʾ״̬
    // ���¹�����Ϸ״̬
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
        // ���״̬û�б仯��ֱ�ӷ���
        if (_currentState == newState) return;

        GameState previousState = _currentState;
        _currentState = newState;

        Debug.Log($"��Ϸ״̬�仯: {previousState} -> {newState}");

        switch (newState)
        {
            case GameState.Initializing:
                // ��ʼ��״̬����ͣʱ�䣬�ȴ�NPC׼��
                TimeManager.Instance.PauseTime();
                if (loadingUI != null) loadingUI.SetActive(true);
                StartCoroutine(WaitForAllNPCsReady());
                break;

            case GameState.SelectingWaiters:
                // ����Աѡ��״̬����ʾѡ��UI
                if (waiterSelectionUI != null) waiterSelectionUI.SetActive(true);
                break;

            case GameState.DayStart:
                // �տ�ʼ״̬����ʾ��ʼӪҵUI���ָ�ʱ��
                StartCoroutine(ShowFreeActivityUI("��ʼӪҵ!"));
                TimeManager.Instance.SetTimeScale(normalTimeScale);
                if (waiterSelectionUI != null) waiterSelectionUI.SetActive(false);
                break;

            case GameState.Operating:
                // ����Ӫҵ״̬��ȷ��ʱ����������
                if (TimeManager.Instance.GetCurrentTimeScale() == 0)
                {
                    TimeManager.Instance.ResumeTime();
                }
                break;

            case GameState.DayEnd:
                // �ս���״̬����ͣʱ�䣬��ʾӪҵ����UI��չʾ����
                TimeManager.Instance.PauseTime();
                Debug.Log("ͨ��EndBusinessSequence����");
                StartCoroutine(EndBusinessSequence());
                break;

            case GameState.Paused:
                // ��ͣ״̬����ͣʱ�䣬��ʾ������壨������ã�
                TimeManager.Instance.PauseTime();
                if (controlPanel != null && isOverviewMode) controlPanel.SetActive(true);
                break;

            case GameState.OverviewMode:
                // ���ģʽ�������Ѿ�ͨ��ToggleGameMode��������ȷ��״̬��ȷ
                if (!isOverviewMode) ToggleGameMode();
                break;
        }

        // ����״̬�仯�¼���֪ͨ����������
        OnGameStateChanged?.Invoke(previousState, newState);
    }


    public delegate void GameStateChangeHandler(GameState previousState, GameState newState);
    public static event GameStateChangeHandler OnGameStateChanged;

    private void HandleGameStateChange(GameState previousState, GameState newState)
    {
        // ֪ͨ����������״̬�仯
        if (RestaurantManager.Instance != null)
        {
            RestaurantManager.Instance.OnGameStateChanged(previousState, newState);
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnGameStateChanged(previousState, newState);
        }

        // ���Ը���״̬�仯ִ���ض��߼�
        switch (newState)
        {
            case GameState.DayStart:
                // �µ�һ�쿪ʼ�����ò���״̬
                RestaurantManager.Instance.ResetDailyReviews();
                RestaurantManager.Instance.ResetCustomerQueue();
                break;

            case GameState.DayEnd:
                // һ�����������˿�
                RestaurantManager.ClearAllCustomers();
                break;
        }
    }
    private bool isOverviewMode = false;
    private Coroutine cameraTransitionCoroutine;
    private float previousTimeScale;

    // ����ԭʼ�������������
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
        // ע��״̬�仯������
        OnGameStateChanged += HandleGameStateChange;


    }

    void Start()
    {


        if (allNPCs.Count == 0)
        {
            allNPCs = FindObjectsOfType<NPCBehavior>().ToList();
        }

        totalNPCs = allNPCs.Count;

        // ����UI
        if (freeActivityUI != null)
            freeActivityUI.SetActive(false);
        if (loadingUI != null)
            loadingUI.SetActive(false);
        StartCoroutine(InitializeGame());
        //dialogutUI.HideImmediate();
        // �Զ���ȡ�������
        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            virtualCamera.Follow = originalFollow;
            virtualCamera.LookAt = originalLookAt;
            //�ӽǸ���ֵ
            cameraTransitionCoroutine = StartCoroutine(TransitionVirtualCamera(
                useOrthographic ? normalCameraSize : normalFieldOfView,
                normalCameraOffset, 
                false
            ));
            if (virtualCamera == null)
            {
                Debug.LogError("δ�ҵ�CinemachineVirtualCamera������Inspector��ָ�����ڳ��������һ����");
                return;
            }
        }
        // ��ȡCinemachineBrain����ѡ��
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

        // ����ԭʼ�������������
        if (virtualCamera != null)
        {
            originalFollow = virtualCamera.Follow;
            originalLookAt = virtualCamera.LookAt;

            // ��ȡTransposer�����ƫ��ֵ
            var transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                originalBodyOffset = transposer.m_FollowOffset;
            }
        }

        // ��ȡTimeManager����
        if (timeManager == null)
            timeManager = TimeManager.Instance;

        // ��ʼ��UI
        InitializeUI();

        // ȷ����������ʼ״̬Ϊ����
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
        // ��ȡʱ�����ڳ����л�
        if (hour == 23 &&minute ==0)
        {
            Debug.Log("���˴���ʱ��");
            string freeActivityTXT = "����Ӫҵ����!";
            
            ShowFreeActivityUI(freeActivityTXT);
            //TriggerDialogueScene();
        }

        // ���ESC��
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleGameMode();
        }

        // ȫ��ģʽ�µĿ�ݼ�
        if (isOverviewMode)
        {
            // �ո����ͣ/����
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }

            // ���ּ������л��ٶ�
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
        // �󶨰�ť�¼�
        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);

        if (normalSpeedButton != null)
            normalSpeedButton.onClick.AddListener(SetNormalSpeed);

        if (fastSpeedButton != null)
            fastSpeedButton.onClick.AddListener(SetFastSpeed);

        if (veryFastSpeedButton != null)
            veryFastSpeedButton.onClick.AddListener(SetVeryFastSpeed);

        // ��ʼ������
        if (speedSlider != null)
        {
            speedSlider.minValue = 0;
            speedSlider.maxValue = veryFastTimeScale;
            speedSlider.onValueChanged.AddListener(OnSpeedSliderChanged);
        }


        // �˵���ʾ��ť
        if (toggleMenuButton != null)
        {
            toggleMenuButton.onClick.AddListener(ToggleMenuDisplay);
        }

        // ȷ����ʼ״̬��ȷ
        if (toggleMenuButton != null)
        {
            toggleMenuButton.gameObject.SetActive(false); // Ĭ������
        }

        if (menuDisplayPanel != null)
        {
            menuDisplayPanel.SetActive(false); // Ĭ������
            isMenuDisplayed = false;
        }



    }

    #region NPC�ճ̵ȴ�+��Ϸ�׶���ʾ
    private IEnumerator InitializeGame()
    {
        isBusinessEnded = false;

        // ȷ��ʱ����ȫ��ͣ
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
            Debug.Log("��Ϸʱ������ͣ���ȴ�����NPC�����ճ̱�...");
        }

        // ��ʾ����UI����ѡ��
        if (loadingUI != null)
        {
            loadingUI.SetActive(true);
            UpdateLoadingUI();
        }

        // ===== ����Աѡ������ =====
        // ��ʾ����Աѡ��UI
        if (waiterSelectionUI != null)
        {
            waiterSelectionUI.SetActive(true);

            // ��ȡѡ��ű���ȷ��ʱ�䱣����ͣ
            WaiterSelectionUI selectionScript = waiterSelectionUI.GetComponent<WaiterSelectionUI>();
            if (selectionScript != null)
            {
                // ȷ��ѡ��UI֪��ʱ��Ӧ����ͣ
                selectionScript.EnsureTimePaused();

                // �ȴ�ѡ�����
                yield return new WaitUntil(() => !waiterSelectionUI.activeSelf);
            }
            else
            {
                // ���û���ҵ��ű���ֱ�ӵȴ�UI�ر�
                yield return new WaitUntil(() => !waiterSelectionUI.activeSelf);
            }
        }
        // ===== ����Աѡ�����̽��� =====

        // �ٴ�ȷ��ʱ����ͣ����ֹ�ڵȴ��ڼ䱻�޸�
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
        }

        // �ȴ�����NPC׼�����
        yield return StartCoroutine(WaitForAllNPCsReady());

        // ���ؼ���UI
        if (loadingUI != null)
        {
            loadingUI.SetActive(false);
        }

        // ��ʾ"���ɻ"UI
        string freeActivityTXT = "��ʼӪҵ!";
        yield return StartCoroutine(ShowFreeActivityUI(freeActivityTXT));

        // �ָ�ʱ��
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(normalTimeScale);
            Debug.Log("����NPC׼����ɣ���Ϸ��ʼ��");
        }

        gameStarted = true;
        StartCoroutine(CheckCustomerStatusRoutine());
        yield return null;
    }
    private IEnumerator EndBusinessSequence()
    {
        // ǿ���˳�ESCģʽ
        ExitOverviewMode();
        UpdateSpeedDisplay();
        // ��ʾ"Ӫҵ����"UI
        yield return StartCoroutine(ShowFreeActivityUI("Ӫҵ����!"));

        // չʾ��������
        yield return StartCoroutine(RestaurantManager.Instance.ShowAllReviews());

        // ���ò���״̬
        RestaurantManager.Instance.ResetDay();
        // ������һ��
        TimeManager.Instance.NextDayResetTime();

        // ���³�ʼ����Ϸ
        StartCoroutine(InitializeGame());
    }
    private IEnumerator CheckCustomerStatusRoutine()
    {
        while (true)
        {
            // ����Ѿ���Ӫҵ����״̬�����ټ��
            if (isBusinessEnded)
            {
                yield break;
            }

            // ���ʱ���Ƿ񳬹�22�������й˿Ͷ����뿪
            if (timeManager != null &&
                (timeManager.CurrentHour >= 22 || AreAllCustomersGone()))
            {
                // ��Ӷ����飬ȷ��������Ӫҵ����������
                if (!isBusinessEnded)
                {
                    Debug.Log("ͨ��EndBusiness����");
                    StartCoroutine(EndBusiness());
                    yield break; // �������Э��
                }
            }

            yield return new WaitForSeconds(0.5f); // ÿ0.5����һ��
        }
    }
    //Ӫҵ������
    public bool isBusinessEnded = false;
    [SerializeField] private GameObject businessEndUI; // �����ڿ�ʼ��Ӫ��UI

    // ����Ƿ����й˿Ͷ����뿪�ķ���
    private bool AreAllCustomersGone()
    {
        return RestaurantManager.ActiveCustomerCount == 0 &&
               RestaurantManager.QueueCustomerCount == 0;
    }

    // ����Ӫҵ��Э�̷���
    private IEnumerator EndBusiness()
    {
        // ȷ��ִֻ��һ��
        if (isBusinessEnded) yield break;
        isBusinessEnded = true;

        Debug.Log("��ʼ����Ӫҵ����");

        // ��ͣʱ��
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
            Debug.Log("Ӫҵ��������Ϸʱ������ͣ");
        }

        // �ȴ�һС��ʱ��ȷ�����в������
        yield return new WaitForSeconds(0.1f);

        // Ӧ����ע�����й˿�
        RestaurantManager.ClearAllCustomers();

        // ��ʾ"Ӫҵ����"UI
        string endBusinessTXT = "Ӫҵ����!";
        yield return StartCoroutine(ShowFreeActivityUI(endBusinessTXT));

        // �ȴ�UI�������
        yield return new WaitForSeconds(1f);
        if (allNPCs.Count == 0)
        {
            Debug.LogWarning("����Ա�ѱ����");
            allNPCs = FindObjectsOfType<NPCBehavior>().ToList();
        }
           
        // ��������NPC�ļ���
        foreach (var npc in allNPCs)
        {
            if (npc != null)
            {
                npc.TriggerDailyReflection();// ��������з�˼
                //npc.SaveMemory(); // ��ȫ�����䱣�浽json
            }
            else
                Debug.LogWarning("NPC�ǿյ�");
        }
        // ��ʾ��������
        yield return StartCoroutine(RestaurantManager.Instance.ShowAllReviews());

        // �ȴ�������ʾ���
        while (RestaurantManager.Instance.IsShowingReviews)
        {
            yield return null;
        }
        
        if (timeManager != null)
        {
            // �����ڼ�1
            timeManager.NextDayResetTime();
            // ����ʱ��Ϊ��ʼӪҵ��ʱ��
            Debug.Log($"�ѽ����{timeManager.GetDayCount()}�죬ʱ������Ϊ��ʼʱ��");
        }

        // ������������������Ӫҵ���߼�����ͳ�Ƶ��������
        Debug.Log("����Ӫҵ�ѽ��������й˿����뿪");

        // ����״̬����ʼ�µ�һ��
        isBusinessEnded = false;
        StartCoroutine(InitializeGame());
    }

    // ���˿�״̬��Э��


    private IEnumerator WaitForAllNPCsReady()
    {
        readyNPCs = 0;

        // ���ÿ��NPC��׼��״̬
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

            // ���¼��ؽ���
            UpdateLoadingUI();

            // ÿ0.5����һ��
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"NPC׼������: {readyNPCs}/{totalNPCs}");
            //string WatingTXT = "Awaiting AI Schedules...";
            //yield return StartCoroutine(ShowFreeActivityUI(WatingTXT));
        }

        //Debug.Log("����NPC�ճ̱�������ɣ�");
    }

    private void UpdateLoadingUI()
    {
        if (loadingText != null)
        {
            loadingText.text = $"���ڳ�ʼ��NPC�ճ̱�... ({readyNPCs}/{totalNPCs})";
        }
    }

    private IEnumerator ShowFreeActivityUI(string showTXT)
    {
        if (freeActivityUI == null || freeActivityText == null)
            yield break;
        freeActivityText.text = showTXT;
        // ���ó�ʼ͸����
        CanvasGroup canvasGroup = freeActivityUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = freeActivityUI.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0f;
        freeActivityUI.SetActive(true);

        // ����
        float elapsedTime = 0f;
        while (elapsedTime < uiFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / uiFadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // ������ʾ
        yield return new WaitForSeconds(uiDisplayDuration);

        // ����
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

    #region ���������ESC���
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

        // ȷ��UI��ȷ����
        UpdateSpeedDisplay();
    }

    private void EnterOverviewMode()
    {
        // ���浱ǰʱ������
        if (timeManager != null)
        {
            previousTimeScale = timeManager.GetCurrentTimeScale();
        }

        // ������ҿ���
        if (playerController != null)
        {
            playerController.enabled = false;

            // ֹͣ��Ҷ���
            Animator animator = playerController.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
        }

        // �л�������ӽ�
        if (cameraTransitionCoroutine != null)
        {
            StopCoroutine(cameraTransitionCoroutine);
        }

        // �����������Ϊȫ��ģʽ
        if (virtualCamera != null)
        {
            // ȡ������Ŀ�꣬ʹ����̶�����������
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;

            cameraTransitionCoroutine = StartCoroutine(TransitionVirtualCamera(
                useOrthographic ? overviewCameraSize : overviewFieldOfView,
                overviewCameraOffset,
                true
            ));
        }

        // ��ʾ�������
        if (controlPanel != null)
        {
            controlPanel.SetActive(true);
            UpdateSpeedDisplay(); // ȷ��UI����
        }

        if (toggleMenuButton != null)
        {
            toggleMenuButton.gameObject.SetActive(true);
            UpdateMenuButtonText();
        }

        if (menuDisplayPanel != null)
        {
            menuDisplayPanel.SetActive(false); // Ĭ�����ز˵�
            isMenuDisplayed = false;
        }

        Debug.Log("����ȫ��ģʽ");
    }

    private void ExitOverviewMode()
    {
        // ������ҿ���
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // �ָ�֮ǰ��ʱ������
        if (timeManager != null)
        {
            // ֻ�е�֮ǰ��ʱ�����Ų�Ϊ0ʱ�Żָ�
            if (previousTimeScale > 0)
            {
                timeManager.SetCustomTimeScale(previousTimeScale);
            }
            else
            {
                // ���֮ǰ����ͣ״̬���ָ�Ϊ�����ٶ�
                timeManager.SetCustomTimeScale(normalTimeScale);
            }
        }

        // �ָ������������
        if (virtualCamera != null)
        {
            // �ָ�����Ŀ��
            virtualCamera.Follow = originalFollow;
            virtualCamera.LookAt = originalLookAt;

            cameraTransitionCoroutine = StartCoroutine(TransitionVirtualCamera(
                useOrthographic ? normalCameraSize : normalFieldOfView,
                normalCameraOffset,
                false
            ));
        }

        // ���ؿ������
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
        Debug.Log("�˳�ȫ��ģʽ");
    }


    // �л��˵���ʾ/����
    public void ToggleMenuDisplay()
    {
        isMenuDisplayed = !isMenuDisplayed;

        if (menuDisplayPanel != null)
        {
            menuDisplayPanel.SetActive(isMenuDisplayed);

            // �����ʾ�˵��������˵���
            if (isMenuDisplayed)
            {
                PopulateMenuItems();
            }
        }

        UpdateMenuButtonText();
    }

    // ���°�ť�ı�
    private void UpdateMenuButtonText()
    {
        if (toggleMenuButtonText != null)
        {
            toggleMenuButtonText.text = isMenuDisplayed ? "���ز˵�" : "��ʾ�˵�";
        }
    }



    private IEnumerator TransitionVirtualCamera(float targetSizeOrFOV, Vector3 targetOffset, bool isOverview)
    {
        if (virtualCamera == null) yield break;

        // ��ȡ��ǰֵ
        float startValue = useOrthographic ? virtualCamera.m_Lens.OrthographicSize : virtualCamera.m_Lens.FieldOfView;
        Vector3 startPosition = virtualCamera.transform.position;

        // ��ȡTransposer���
        var transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        Vector3 startOffset = transposer != null ? transposer.m_FollowOffset : Vector3.zero;

        // ����Ŀ��λ��
        Vector3 targetPosition = CalculateCameraPosition(targetOffset, isOverview);

        float elapsedTime = 0;

        while (elapsedTime < cameraTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / cameraTransitionDuration;
            t = Mathf.SmoothStep(0, 1, t);

            // ƽ�������������
            if (useOrthographic)
            {
                virtualCamera.m_Lens.OrthographicSize = Mathf.Lerp(startValue, targetSizeOrFOV, t);
            }
            else
            {
                virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(startValue, targetSizeOrFOV, t);
            }

            // �����ȫ��ģʽ��ֱ���������λ��
            if (isOverview)
            {
                virtualCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            }
            else if (transposer != null)
            {
                // ���������ģʽ��ͨ��Transposer��ƫ��������
                transposer.m_FollowOffset = Vector3.Lerp(startOffset, targetOffset, t);
            }

            yield return null;
        }

        // ��������ֵ
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
            // ȫ��ģʽ�����λ�����������Ϸ�
            return new Vector3(-4.5f, -5.5f, 0) + offset; //��������
        }
        else
        {
            // ����ģʽ�����������ң�ͨ��Cinemachine��Follow���ƣ�
            return virtualCamera.transform.position; // ���ֵ�ǰλ�ã���Cinemachine�������
        }
    }
    public bool IsOverviewMode()
    {
        return isOverviewMode;
    }

    private void PopulateMenuItems()
    {
        // ������в˵���
        foreach (Transform child in menuContentParent)
        {
            Destroy(child.gameObject);
        }

        // ��ȡ�˵�����
        if (RestaurantManager.Instance == null || RestaurantManager.menuItems == null)
        {
            Debug.LogWarning("�޷���ȡ�˵�����");
            return;
        }

        // ���ò˵�����
        if (menuTitleText != null)
        {
            menuTitleText.text = $"�����˵� (��{RestaurantManager.menuItems.Length}����)";
        }

        // �����˵���
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

    #endregion �������
    #region ʱ�����ٿ���
    // ʱ����Ʒ������ֲ���
    private void TogglePause()
    {
        if (TimeManager.Instance.GetCurrentTimeScale() > 0)
        {
            TimeManager.Instance.PauseTime();
            if (pauseButton != null) pauseButton.GetComponentInChildren<TMP_Text>().text = "����";
        }
        else
        {
            TimeManager.Instance.ResumeTime();
            if (pauseButton != null) pauseButton.GetComponentInChildren<TMP_Text>().text = "��ͣ";
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
            currentSpeedText.text = $"����: x{currentScale:F1}";

            // ���»���λ��
            if (speedSlider != null)
            {
                speedSlider.SetValueWithoutNotify(currentScale);
            }

            // ������ͣ��ť�ı�
            if (pauseButton != null)
            {
                pauseButton.GetComponentInChildren<TMP_Text>().text = currentScale > 0 ? "��ͣ" : "��ԭ";
            }
        }
    }


    #endregion


    #region ��������
    private IEnumerator ShowDialogueSequence()
    {

        // 1. ��ͣʱ��
        timeManager.SetCustomTimeScale(0f);
        string freeActivityTXT = "Negotiation Period!";
        ShowFreeActivityUI(freeActivityTXT);
        yield return new WaitForSeconds(0.1f);
        // 2. ��ʾ�Ի�UI
        DialogueUIManager.Instance.Show();
        //yield return null;
        yield return new WaitForSeconds(0.1f);
        // 3. ��ʾ��һ��Ի�
        bool continued = false;
        DialogueUIManager.Instance.ShowDialogue(
            "Merchant",
            "What do you need",
            () => continued = true
        );
        yield return new WaitUntil(() => continued);
        yield return new WaitForSeconds(0.1f);
        // 4. �ȴ��������
        string playerInput = "";
        bool inputReceived = false;
        DialogueUIManager.Instance.ShowPlayerInput(
            "������ظ�...",
            (input) => {
                playerInput = input;
                inputReceived = true;
            }
        );
        yield return new WaitUntil(() => inputReceived);
        yield return new WaitForSeconds(0.1f);
        // 5. ��ʾ����Ի�...

        // 6. ����ʱ����UI���ָ�ʱ��
        DialogueUIManager.Instance.Hide();
        timeManager.ResumeNormalTime();
    }

    private void TriggerDialogueScene()
    {
        // ���öԻ�����
        DialogueSceneManager.NextNPCName = "Merchant";
        DialogueSceneManager.NextDialogue = "What do you need?";
        DialogueSceneManager.ReturnSceneName = SceneManager.GetActiveScene().name;

        // �������ʾ
        //hasShownDay3Dialogue = true;

        // ���ضԻ�����
        //SceneManager.LoadScene("NegotiationScene");
    }


    #endregion
}