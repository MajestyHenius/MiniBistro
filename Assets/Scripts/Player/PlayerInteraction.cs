using System.Collections;  
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;  
public class PlayerInteraction : MonoBehaviour
{
    [Header("交互设置")]
    //public KeyCode interactKey = KeyCode.Space;
    public float interactionRange = 2f;
    public float replyDisplayTime = 2f; // 回复显示的时间（秒）
    public string npcTag = "NPC"; // 用于识别NPC
    private InteractionType currentInteractionType = InteractionType.None;
    // 交互类型枚举
    private enum InteractionType
    {
        None,
        NPC,
        Customer
    }
    [Header("UI引用")]
    public GameObject inputPanel; // 输入面板
    public GameObject dialogueBubble; // 对话泡
    public TMP_Text dialogueNameText; // 名字显示文本
    public TMP_Text dialogueContentText; // 内容显示文本
    public Image dialogueBackground; // 对话UI背景
    public float dialogueDisplayTime = 4f; // UI对话显示时间
    public TMP_InputField inputField; // 输入框
    public Button sendButton; // 发送按钮
    public Button cancelButton; // 取消按钮
    public TMP_Text toWhom; //显示一下对谁说的文字。
    private NPCBehavior currentNPC;
    private CustomerNPC currentCustomer;
    private bool canInteract = false;
    private float previousTimeScale;
    private Coroutine dialogueCoroutine; // 用于控制对话显示时间的协程
    void Start()
    {
        Debug.Log($"PlayerInteraction 启动 - GameObject: {gameObject.name}");

        // 如果没有设置UI，创建简单的UI
        if (inputPanel == null)
        {
            Debug.Log("创建UI面板");
            CreateSimpleUI();
        }
        else
        {
            // 如果已经有UI，确保它是隐藏的
            inputPanel.SetActive(false);
        }
        if (dialogueBubble != null)
        {
            dialogueBubble.SetActive(false);
        }
        // 绑定按钮事件
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendMessage);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);

        // 绑定输入框的回车事件
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnInputSubmit);
        }
        CustomerNPC.OnCustomerReply += HandleCustomerReply;
    }

    void CreateSimpleUI()
    {
        // 创建Canvas
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 检查并创建EventSystem
        if (GameObject.Find("EventSystem") == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 创建输入面板
        inputPanel = new GameObject("InputPanel");
        inputPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = inputPanel.AddComponent<RectTransform>();
        Image bg = inputPanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);

        panelRect.sizeDelta = new Vector2(400, 200);
        panelRect.anchoredPosition = Vector2.zero;

        // 创建标题
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

        // 创建输入框容器
        GameObject inputObj = new GameObject("InputField");
        inputObj.transform.SetParent(inputPanel.transform, false);
        RectTransform inputRect = inputObj.AddComponent<RectTransform>();

        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(350, 40);
        inputRect.anchoredPosition = new Vector2(0, 0);

        // 添加输入框组件
        inputField = inputObj.AddComponent<TMP_InputField>();

        // 创建输入框背景
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

        // 创建文本区域
        GameObject textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputObj.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textArea.AddComponent<RectMask2D>();

        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.sizeDelta = Vector2.zero;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);

        // 创建文本
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

        // 设置输入框引用
        inputField.textComponent = text;
        inputField.textViewport = textAreaRect;

        // 创建发送按钮
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

        // 发送按钮文本
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

        // 创建取消按钮
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

        // 取消按钮文本
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

        // 最后隐藏整个面板
        inputPanel.SetActive(false);
        Debug.Log("UI创建完成并已隐藏");
    }

    void Update()
    {
        // 检查附近的NPC和顾客
        CheckNearbyInteractables();

        // 检查交互输入 - 只有当有可交互对象且面板未激活时
        if (canInteract && Input.GetButtonDown("Jump") && !inputPanel.activeSelf)
        {
            OpenInputPanel();
        }
    }

    void CheckNearbyNPC()
    {
        // 获取范围内的所有碰撞体，检测其他npc
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
            Debug.LogWarning("没有可交互对象，无法打开面板");
            return;
        }
        if (currentInteractionType == InteractionType.NPC && currentNPC != null)
        {
            toWhom.text = $"发送消息给{currentNPC.occupation} {currentNPC.npcName}\n" +
                $"Sending message to waiter {currentNPC.npcName_eng}";  
        }
        else if (currentInteractionType == InteractionType.Customer && currentCustomer != null)
        {
            toWhom.text = $"发送消息给顾客{currentCustomer.customerName}\n" +
                $"Sending message to customer{currentCustomer.customerName_eng}";
        }

            Debug.Log($"打开输入面板，当前交互类型: {currentInteractionType}");
        // 保存当前游戏时间流速比例
        if (TimeManager.Instance != null)
        {
            previousTimeScale = TimeManager.Instance.GetCurrentTimeScale();
            // 减慢游戏时间流速
            TimeManager.Instance.SetCustomTimeScale(previousTimeScale * 0.0f);
            Debug.Log($"游戏时间流速已减慢: {previousTimeScale} -> {TimeManager.Instance.GetCurrentTimeScale()}");
        }
        else
        {
            Debug.LogError("TimeManager.Instance 是 null!");
        }

        // 显示输入面板
        inputPanel.SetActive(true);
        inputField.text = "";
        inputField.Select();
        inputField.ActivateInputField();

        // 添加调试
        Debug.Log($"=== UI状态检查 ===");
        Debug.Log($"InputPanel激活: {inputPanel.activeSelf}");
        Debug.Log($"SendButton可交互: {sendButton.interactable}");
        Debug.Log($"CancelButton可交互: {cancelButton.interactable}");
        Debug.Log($"InputField可交互: {inputField.interactable}");

        // 检查EventSystem
        if (EventSystem.current != null)
        {
            Debug.Log("EventSystem存在且激活");
        }
        else
        {
            Debug.LogError("EventSystem不存在！");
        }
    }

    void OnSendMessage()
    {
        if (string.IsNullOrEmpty(inputField.text)) return;

        string message = inputField.text;
        Debug.Log($"玩家发送消息: {message}");
        ShowDialogue("我（经理）", message);
        // 根据交互类型发送消息
        if (currentInteractionType == InteractionType.NPC && currentNPC != null)
        {
            // 发送消息给NPC
            currentNPC.ReceivePlayerMessage(message);

            // 禁用发送按钮，防止重复发送
            if (sendButton != null) sendButton.interactable = false;

            // 获取AI回复
            currentNPC.GetAIResponse(message, (response) =>
            {
                Debug.Log($"收到AI回复: {response}");
                // 这里可以处理NPC的回复，比如显示在输入框中
                ShowDialogue($"{currentNPC.occupation} {currentNPC.npcName}", response);
            });

        }
        else if (currentInteractionType == InteractionType.Customer && currentCustomer != null)
        {
            // 发送消息给顾客
            currentCustomer.ReceivePlayerMessage(message);
            
            // 禁用发送按钮，防止重复发送
            if (sendButton != null) sendButton.interactable = false;

            // 显示"思考中"提示
            ShowReplyInInputField("(顾客思考中...)");
            // 不需要等待顾客回复，因为顾客会自行处理并显示对话气泡

            ShowDialogue($"顾客{currentCustomer.customerName}",currentCustomer.customerReplyPlayer);
            StartCoroutine(CloseInputPanelAfterDelay(2f));
        }
    }
    void ShowReplyInInputField(string reply)
    {
        if (inputField != null)
        {
            inputField.text = reply;
            inputField.interactable = false;

            // 更改文本颜色为灰色，表示这是回复
            TMP_Text textComponent = inputField.textComponent;
            if (textComponent != null)
            {
                textComponent.color = Color.gray;
            }
        }
    }
    // 获取NPC的回复

    // 在输入框中显示回复
    // 在 ShowReplyInInputField 方法中添加调试输出

    // 新的协程：显示回复一段时间后关闭面板
    IEnumerator CloseInputPanelAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        // 恢复输入框和按钮的状态
        inputField.interactable = true;
        if (sendButton != null) sendButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = true;

        // 恢复文本颜色
        TMP_Text textComponent = inputField.textComponent;
        if (textComponent != null)
        {
            textComponent.color = Color.black; // 恢复默认颜色
        }

        // 关闭面板
        CloseInputPanel();
    }


    // 顾客：
    public void OpenInputPanelForCustomer(CustomerNPC customer)
    {
        currentCustomer = customer; // 需要添加currentCustomer字段
        OpenInputPanel(); // 使用原有的打开面板方法
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

        // 清空输入框
        inputField.text = "";
        inputField.interactable = true;

        // 恢复文本颜色
        TMP_Text textComponent = inputField.textComponent;
        if (textComponent != null)
        {
            textComponent.color = Color.black;
        }

        // 恢复按钮状态
        if (sendButton != null) sendButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = true;

        // 恢复时间流速
        StartCoroutine(RestoreTimeScaleAfterDelay(0.5f));

        // 重置交互类型
        currentInteractionType = InteractionType.None;
    }

    IEnumerator RestoreTimeScaleAfterDelay(float delay)
    {
        // 等待真实时间
        yield return new WaitForSecondsRealtime(delay);

        // 恢复游戏时间流速
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetCustomTimeScale(previousTimeScale);  // 使用SetCustomTimeScale恢复,
            Debug.Log($"游戏时间流速已恢复: {TimeManager.Instance.GetCurrentTimeScale()}");
        }
    }




    void CheckNearbyInteractables()
    {
        // 获取范围内的所有碰撞体
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange);

        NPCBehavior nearestNPC = null;
        CustomerNPC nearestCustomer = null;
        float nearestNPCDistance = float.MaxValue;
        float nearestCustomerDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            // 检查NPC
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

            // 检查顾客（特别是处于Emergency状态的）
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

        // 优先处理Emergency状态的顾客
        if (nearestCustomer != null)
        {
            currentCustomer = nearestCustomer;
            currentNPC = null;
            currentInteractionType = InteractionType.Customer;
            canInteract = true;
            Debug.Log($"发现Emergency状态顾客: {currentCustomer.customerName}");
        }
        else if (nearestNPC != null)
        {
            currentNPC = nearestNPC;
            currentCustomer = null;
            currentInteractionType = InteractionType.NPC;
            canInteract = true;
            Debug.Log($"发现可交互NPC: {currentNPC.npcName}");
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


    // 对话泡泡：
    void ShowDialogue(string speaker, string content)
    {
        // 检查是否有对话UI引用
        if (dialogueNameText == null || dialogueContentText == null || dialogueBubble == null)
        {
            Debug.LogError("没有正确引用对话UI组件！");
            return;
        }

        // 设置名字和内容
        dialogueNameText.text = speaker;
        dialogueContentText.text = content;

        // 显示对话气泡
        dialogueBubble.SetActive(true);

        // 停止以前的显示时间协程（如果有）
        if (dialogueCoroutine != null)
        {
            StopCoroutine(dialogueCoroutine);
        }

        // 开始新的显示时间协程
        dialogueCoroutine = StartCoroutine(HideDialogueAfterDelay(dialogueDisplayTime));
    }

    // 显示一段时间后隐藏对话
    IEnumerator HideDialogueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        dialogueBubble.SetActive(false);
        dialogueCoroutine = null;
    }
    private void HandleCustomerReply(string customerName, string reply)
    {
        Debug.Log($"收到顾客回复: {customerName} - {reply}");

        // 只有当这个顾客是当前交互的顾客时才显示回复
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