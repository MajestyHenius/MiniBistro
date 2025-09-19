using System.Collections;  
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;  
public class PlayerInteraction : MonoBehaviour
{
    [Header("��������")]
    //public KeyCode interactKey = KeyCode.Space;
    public float interactionRange = 2f;
    public float replyDisplayTime = 2f; // �ظ���ʾ��ʱ�䣨�룩
    public string npcTag = "NPC"; // ����ʶ��NPC
    private InteractionType currentInteractionType = InteractionType.None;
    // ��������ö��
    private enum InteractionType
    {
        None,
        NPC,
        Customer
    }
    [Header("UI����")]
    public GameObject inputPanel; // �������
    public GameObject dialogueBubble; // �Ի���
    public TMP_Text dialogueNameText; // ������ʾ�ı�
    public TMP_Text dialogueContentText; // ������ʾ�ı�
    public Image dialogueBackground; // �Ի�UI����
    public float dialogueDisplayTime = 4f; // UI�Ի���ʾʱ��
    public TMP_InputField inputField; // �����
    public Button sendButton; // ���Ͱ�ť
    public Button cancelButton; // ȡ����ť
    public TMP_Text toWhom; //��ʾһ�¶�˭˵�����֡�
    private NPCBehavior currentNPC;
    private CustomerNPC currentCustomer;
    private bool canInteract = false;
    private float previousTimeScale;
    private Coroutine dialogueCoroutine; // ���ڿ��ƶԻ���ʾʱ���Э��
    void Start()
    {
        Debug.Log($"PlayerInteraction ���� - GameObject: {gameObject.name}");

        // ���û������UI�������򵥵�UI
        if (inputPanel == null)
        {
            Debug.Log("����UI���");
            CreateSimpleUI();
        }
        else
        {
            // ����Ѿ���UI��ȷ���������ص�
            inputPanel.SetActive(false);
        }
        if (dialogueBubble != null)
        {
            dialogueBubble.SetActive(false);
        }
        // �󶨰�ť�¼�
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendMessage);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        // �������Ļس��¼�
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnInputSubmit);
        }
        CustomerNPC.OnCustomerReply += HandleCustomerReply;
    }

    void CreateSimpleUI()
    {
        // ����Canvas
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // ��鲢����EventSystem
        if (GameObject.Find("EventSystem") == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // �����������
        inputPanel = new GameObject("InputPanel");
        inputPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = inputPanel.AddComponent<RectTransform>();
        Image bg = inputPanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);

        panelRect.sizeDelta = new Vector2(400, 200);
        panelRect.anchoredPosition = Vector2.zero;

        // ��������
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(inputPanel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        TMP_Text titleText = titleObj.AddComponent<TextMeshProUGUI>();

        titleText.text = "Send Message to NPC";
        titleText.fontSize = 24;
        titleText.alignment = TextAlignmentOptions.Center;

        titleRect.anchorMin = new Vector2(0.5f, 1);
        titleRect.anchorMax = new Vector2(0.5f, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(300, 40);
        titleRect.anchoredPosition = new Vector2(0, -20);

        // �������������
        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(inputPanel.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();

        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(350, 40);
        inputRect.anchoredPosition = new Vector2(0, 0);

        // �����������
        inputField = inputObj.AddComponent<TMP_InputField>();

        // ��������򱳾�
        GameObject inputBg = new GameObject("Background");
        inputBg.transform.SetParent(inputObj.transform, false);
        RectTransform inputBgRect = inputBg.AddComponent<RectTransform>();
        Image inputBgImage = inputBg.AddComponent<Image>();
        inputBgImage.color = Color.white;

        inputBgRect.anchorMin = Vector2.zero;
        inputBgRect.anchorMax = Vector2.one;
        inputBgRect.sizeDelta = Vector2.zero;
        inputBgRect.offsetMin = Vector2.zero;
        inputBgRect.offsetMax = Vector2.zero;

        // �����ı�����
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textArea.AddComponent<RectMask2D>();

        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.sizeDelta = Vector2.zero;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);

        // �����ı�
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(textArea.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 18;
        text.color = Color.black;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // �������������
        inputField.textComponent = text;
        inputField.textViewport = textAreaRect;

        // �������Ͱ�ť
        GameObject sendObj = new GameObject("SendButton");
        sendObj.transform.SetParent(inputPanel.transform, false);
        RectTransform sendRect = sendObj.AddComponent<RectTransform>();
        sendButton = sendObj.AddComponent<Button>();
        Image sendImage = sendObj.AddComponent<Image>();
        sendImage.color = new Color(0.2f, 0.7f, 0.2f);

        sendRect.anchorMin = new Vector2(0.5f, 0);
        sendRect.anchorMax = new Vector2(0.5f, 0);
        sendRect.pivot = new Vector2(0.5f, 0);
        sendRect.sizeDelta = new Vector2(80, 35);
        sendRect.anchoredPosition = new Vector2(-50, 20);

        // ���Ͱ�ť�ı�
        GameObject sendTextObj = new GameObject("Text");
        sendTextObj.transform.SetParent(sendObj.transform, false);
        RectTransform sendTextRect = sendTextObj.AddComponent<RectTransform>();
        TMP_Text sendText = sendTextObj.AddComponent<TextMeshProUGUI>();
        sendText.text = "Send";
        sendText.fontSize = 18;
        sendText.alignment = TextAlignmentOptions.Center;

        sendTextRect.anchorMin = Vector2.zero;
        sendTextRect.anchorMax = Vector2.one;
        sendTextRect.sizeDelta = Vector2.zero;
        sendTextRect.offsetMin = Vector2.zero;
        sendTextRect.offsetMax = Vector2.zero;

        // ����ȡ����ť
        GameObject cancelObj = new GameObject("CancelButton");
        cancelObj.transform.SetParent(inputPanel.transform, false);
        RectTransform cancelRect = cancelObj.AddComponent<RectTransform>();
        cancelButton = cancelObj.AddComponent<Button>();
        Image cancelImage = cancelObj.AddComponent<Image>();
        cancelImage.color = new Color(0.7f, 0.2f, 0.2f);

        cancelRect.anchorMin = new Vector2(0.5f, 0);
        cancelRect.anchorMax = new Vector2(0.5f, 0);
        cancelRect.pivot = new Vector2(0.5f, 0);
        cancelRect.sizeDelta = new Vector2(80, 35);
        cancelRect.anchoredPosition = new Vector2(50, 20);

        // ȡ����ť�ı�
        GameObject cancelTextObj = new GameObject("Text");
        cancelTextObj.transform.SetParent(cancelObj.transform, false);
        RectTransform cancelTextRect = cancelTextObj.AddComponent<RectTransform>();
        TMP_Text cancelText = cancelTextObj.AddComponent<TextMeshProUGUI>();
        cancelText.text = "Cancel";
        cancelText.fontSize = 18;
        cancelText.alignment = TextAlignmentOptions.Center;

        cancelTextRect.anchorMin = Vector2.zero;
        cancelTextRect.anchorMax = Vector2.one;
        cancelTextRect.sizeDelta = Vector2.zero;
        cancelTextRect.offsetMin = Vector2.zero;
        cancelTextRect.offsetMax = Vector2.zero;

        // ��������������
        inputPanel.SetActive(false);
        Debug.Log("UI������ɲ�������");
    }

    void Update()
    {
        // ��鸽����NPC�͹˿�
        CheckNearbyInteractables();

        // ��齻������ - ֻ�е��пɽ������������δ����ʱ
        if (canInteract && Input.GetButtonDown("Jump") && !inputPanel.activeSelf)
        {
            OpenInputPanel();
        }
    }

    void CheckNearbyNPC()
    {
        // ��ȡ��Χ�ڵ�������ײ�壬�������npc
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange);

        NPCBehavior nearestNPC = null;
        float nearestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            NPCBehavior npc = collider.GetComponent<NPCBehavior>();
            if (npc != null && npc.playerInRange)
            {
                float distance = Vector2.Distance(transform.position, npc.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestNPC = npc;
                }
            }
        }

        currentNPC = nearestNPC;
        canInteract = currentNPC != null;
    }

    void OpenInputPanel()
    {
        if (currentInteractionType == InteractionType.None)
        {
            Debug.LogWarning("û�пɽ��������޷������");
            return;
        }
        if (currentInteractionType == InteractionType.NPC && currentNPC != null)
        {
            toWhom.text = $"������Ϣ��{currentNPC.occupation} {currentNPC.npcName}\n" +
                $"Sending message to waiter {currentNPC.npcName_eng}";  
        }
        else if (currentInteractionType == InteractionType.Customer && currentCustomer != null)
        {
            toWhom.text = $"������Ϣ���˿�{currentCustomer.customerName}\n" +
                $"Sending message to customer{currentCustomer.customerName_eng}";
        }

            Debug.Log($"��������壬��ǰ��������: {currentInteractionType}");
        // ���浱ǰ��Ϸʱ�����ٱ���
        if (TimeManager.Instance != null)
        {
            previousTimeScale = TimeManager.Instance.GetCurrentTimeScale();
            // ������Ϸʱ������
            TimeManager.Instance.SetCustomTimeScale(previousTimeScale * 0.0f);
            Debug.Log($"��Ϸʱ�������Ѽ���: {previousTimeScale} -> {TimeManager.Instance.GetCurrentTimeScale()}");
        }
        else
        {
            Debug.LogError("TimeManager.Instance �� null!");
        }

        // ��ʾ�������
        inputPanel.SetActive(true);
        inputField.text = "";
        inputField.Select();
        inputField.ActivateInputField();

        // ��ӵ���
        Debug.Log($"=== UI״̬��� ===");
        Debug.Log($"InputPanel����: {inputPanel.activeSelf}");
        Debug.Log($"SendButton�ɽ���: {sendButton.interactable}");
        Debug.Log($"CancelButton�ɽ���: {cancelButton.interactable}");
        Debug.Log($"InputField�ɽ���: {inputField.interactable}");

        // ���EventSystem
        if (EventSystem.current != null)
        {
            Debug.Log("EventSystem�����Ҽ���");
        }
        else
        {
            Debug.LogError("EventSystem�����ڣ�");
        }
    }

    void OnSendMessage()
    {
        if (string.IsNullOrEmpty(inputField.text)) return;

        string message = inputField.text;
        Debug.Log($"��ҷ�����Ϣ: {message}");
        ShowDialogue("�ң�����", message);
        // ���ݽ������ͷ�����Ϣ
        if (currentInteractionType == InteractionType.NPC && currentNPC != null)
        {
            // ������Ϣ��NPC
            currentNPC.ReceivePlayerMessage(message);

            // ���÷��Ͱ�ť����ֹ�ظ�����
            if (sendButton != null) sendButton.interactable = false;

            // ��ȡAI�ظ�
            currentNPC.GetAIResponse(message, (response) =>
            {
                Debug.Log($"�յ�AI�ظ�: {response}");
                // ������Դ���NPC�Ļظ���������ʾ���������
                ShowDialogue($"{currentNPC.occupation} {currentNPC.npcName}", response);
            });

        }
        else if (currentInteractionType == InteractionType.Customer && currentCustomer != null)
        {
            // ������Ϣ���˿�
            currentCustomer.ReceivePlayerMessage(message);
            
            // ���÷��Ͱ�ť����ֹ�ظ�����
            if (sendButton != null) sendButton.interactable = false;

            // ��ʾ"˼����"��ʾ
            ShowReplyInInputField("(�˿�˼����...)");
            // ����Ҫ�ȴ��˿ͻظ�����Ϊ�˿ͻ����д�����ʾ�Ի�����

            ShowDialogue($"�˿�{currentCustomer.customerName}",currentCustomer.customerReplyPlayer);
            StartCoroutine(CloseInputPanelAfterDelay(2f));
        }
    }
    void ShowReplyInInputField(string reply)
    {
        if (inputField != null)
        {
            inputField.text = reply;
            inputField.interactable = false;

            // �����ı���ɫΪ��ɫ����ʾ���ǻظ�
            TMP_Text textComponent = inputField.textComponent;
            if (textComponent != null)
            {
                textComponent.color = Color.gray;
            }
        }
    }
    // ��ȡNPC�Ļظ�

    // �����������ʾ�ظ�
    // �� ShowReplyInInputField ��������ӵ������

    // �µ�Э�̣���ʾ�ظ�һ��ʱ���ر����
    IEnumerator CloseInputPanelAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        // �ָ������Ͱ�ť��״̬
        inputField.interactable = true;
        if (sendButton != null) sendButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = true;

        // �ָ��ı���ɫ
        TMP_Text textComponent = inputField.textComponent;
        if (textComponent != null)
        {
            textComponent.color = Color.black; // �ָ�Ĭ����ɫ
        }

        // �ر����
        CloseInputPanel();
    }


    // �˿ͣ�
    public void OpenInputPanelForCustomer(CustomerNPC customer)
    {
        currentCustomer = customer; // ��Ҫ���currentCustomer�ֶ�
        OpenInputPanel(); // ʹ��ԭ�еĴ���巽��
    }
    void OnInputSubmit(string text)
    {
        OnSendMessage();
    }

    void OnCancel()
    {
        CloseInputPanel();
    }

    void CloseInputPanel()
    {
        inputPanel.SetActive(false);

        // ��������
        inputField.text = "";
        inputField.interactable = true;

        // �ָ��ı���ɫ
        TMP_Text textComponent = inputField.textComponent;
        if (textComponent != null)
        {
            textComponent.color = Color.black;
        }

        // �ָ���ť״̬
        if (sendButton != null) sendButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = true;

        // �ָ�ʱ������
        StartCoroutine(RestoreTimeScaleAfterDelay(0.5f));

        // ���ý�������
        currentInteractionType = InteractionType.None;
    }

    IEnumerator RestoreTimeScaleAfterDelay(float delay)
    {
        // �ȴ���ʵʱ��
        yield return new WaitForSecondsRealtime(delay);

        // �ָ���Ϸʱ������
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetCustomTimeScale(previousTimeScale);  // ʹ��SetCustomTimeScale�ָ�,
            Debug.Log($"��Ϸʱ�������ѻָ�: {TimeManager.Instance.GetCurrentTimeScale()}");
        }
    }




    void CheckNearbyInteractables()
    {
        // ��ȡ��Χ�ڵ�������ײ��
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange);

        NPCBehavior nearestNPC = null;
        CustomerNPC nearestCustomer = null;
        float nearestNPCDistance = float.MaxValue;
        float nearestCustomerDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            // ���NPC
            NPCBehavior npc = collider.GetComponent<NPCBehavior>();
            if (npc != null && npc.playerInRange)
            {
                float distance = Vector2.Distance(transform.position, npc.transform.position);
                if (distance < nearestNPCDistance)
                {
                    nearestNPCDistance = distance;
                    nearestNPC = npc;
                }
            }

            // ���˿ͣ��ر��Ǵ���Emergency״̬�ģ�
            CustomerNPC customer = collider.GetComponent<CustomerNPC>();
            if (customer != null && customer.GetCurrentState() == CustomerNPC.CustomerState.Emergency)
            {
                float distance = Vector2.Distance(transform.position, customer.transform.position);
                if (distance < nearestCustomerDistance)
                {
                    nearestCustomerDistance = distance;
                    nearestCustomer = customer;
                }
            }
        }

        // ���ȴ���Emergency״̬�Ĺ˿�
        if (nearestCustomer != null)
        {
            currentCustomer = nearestCustomer;
            currentNPC = null;
            currentInteractionType = InteractionType.Customer;
            canInteract = true;
            Debug.Log($"����Emergency״̬�˿�: {currentCustomer.customerName}");
        }
        else if (nearestNPC != null)
        {
            currentNPC = nearestNPC;
            currentCustomer = null;
            currentInteractionType = InteractionType.NPC;
            canInteract = true;
            Debug.Log($"���ֿɽ���NPC: {currentNPC.npcName}");
        }
        else
        {
            currentNPC = null;
            currentCustomer = null;
            currentInteractionType = InteractionType.None;
            canInteract = false;
        }
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }


    // �Ի����ݣ�
    void ShowDialogue(string speaker, string content)
    {
        // ����Ƿ��жԻ�UI����
        if (dialogueNameText == null || dialogueContentText == null || dialogueBubble == null)
        {
            Debug.LogError("û����ȷ���öԻ�UI�����");
            return;
        }

        // �������ֺ�����
        dialogueNameText.text = speaker;
        dialogueContentText.text = content;

        // ��ʾ�Ի�����
        dialogueBubble.SetActive(true);

        // ֹͣ��ǰ����ʾʱ��Э�̣�����У�
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }

        // ��ʼ�µ���ʾʱ��Э��
        dialogueCoroutine = StartCoroutine(HideDialogueAfterDelay(dialogueDisplayTime));
    }

    // ��ʾһ��ʱ������ضԻ�
    IEnumerator HideDialogueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        dialogueBubble.SetActive(false);
        dialogueCoroutine = null;
    }
    private void HandleCustomerReply(string customerName, string reply)
    {
        Debug.Log($"�յ��˿ͻظ�: {customerName} - {reply}");

        // ֻ�е�����˿��ǵ�ǰ�����Ĺ˿�ʱ����ʾ�ظ�
        if (currentCustomer != null && currentCustomer.customerName == customerName)
        {
            ShowDialogue(customerName, reply);
            if (inputPanel.activeSelf)
            {
                StartCoroutine(CloseInputPanelAfterDelay(2f));
            }
        }
    }


}