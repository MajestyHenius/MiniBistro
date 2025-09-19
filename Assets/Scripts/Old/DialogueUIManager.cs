using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System;
using UnityEngine.EventSystems;

public class DialogueUIManager : MonoBehaviour
{
    public static DialogueUIManager Instance;

    [Header("UI组件")]
    [SerializeField] private GameObject backgroundOverlay;
    [SerializeField] private GameObject dialoguePanel;

    [Header("对话框内容")]
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueContentText;
    [SerializeField] private GameObject continueIndicator;

    [Header("玩家输入")]
    [SerializeField] private TMP_InputField playerInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button dealButton;
    [Header("设置")]
    [SerializeField] private float textSpeed = 0.03f;

    private Coroutine currentTypingCoroutine;
    private bool isTyping = false;
    private bool isWaitingForContinue = false;
    private bool isWaitingForPlayerInput = false;
    private string currentFullText = "";

    private Action currentContinueCallback;
    private Action<string> currentInputCallback;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 设置输入框事件
        if (playerInputField != null)
        {
            playerInputField.onSubmit.AddListener(OnInputSubmit);
            playerInputField.gameObject.SetActive(false);
        }

        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendButtonClick);
            sendButton.gameObject.SetActive(false);
        }

        // 确保初始状态是隐藏的
        if (backgroundOverlay != null) backgroundOverlay.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (continueIndicator != null) continueIndicator.SetActive(false);
    }

    void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // 点击继续对话
        if (isWaitingForContinue && !isWaitingForPlayerInput)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("调用 ContinueDialogue");
                ContinueDialogue();
            }
        }
    }

    /// <summary>
    /// 显示对话UI
    /// </summary>
    public void Show(Action onComplete = null)
    {
        if (backgroundOverlay != null) backgroundOverlay.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        onComplete?.Invoke();
    }

    /// <summary>
    /// 隐藏对话UI
    /// </summary>
    public void Hide(Action onComplete = null)
    {
        if (backgroundOverlay != null) backgroundOverlay.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (playerInputField != null) playerInputField.gameObject.SetActive(false);
        if (sendButton != null) sendButton.gameObject.SetActive(false);

        isWaitingForContinue = false;
        isWaitingForPlayerInput = false;
        isTyping = false;

        onComplete?.Invoke();
    }

    /// <summary>
    /// 显示一句对话
    /// </summary>
    public void ShowDialogue(string speakerName, string content, Action onContinue = null)
    {
        currentContinueCallback = onContinue;
        isWaitingForPlayerInput= false;
        if (speakerNameText != null)
            speakerNameText.text = speakerName;
        if(dialogueContentText != null)     
            dialogueContentText.text = content;
        if(playerInputField!=null)
            playerInputField.gameObject.SetActive(false);
        if(sendButton!=null) sendButton.gameObject.SetActive(false);
        isWaitingForContinue = true;
        if(continueIndicator!=null)
            continueIndicator.SetActive(true);
        //StartCoroutine(ShowDialogueCoroutine(speakerName, content));
    }

    /// <summary>
    /// 显示玩家输入框
    /// </summary>
    public void ShowPlayerInput(string placeholder = "请输入...", Action<string> onInputComplete = null)
    {
        Debug.Log("ShowPlayerInput 被调用");
        currentInputCallback = onInputComplete;

        // 设置状态标志
        isWaitingForContinue = false;
        isWaitingForPlayerInput = true;

        // 清空对话内容
        if (dialogueContentText != null)
            dialogueContentText.text = "";
        if (speakerNameText != null)
            speakerNameText.text = "Player";
        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        // 显示输入框
        if (playerInputField != null)
        {
            playerInputField.gameObject.SetActive(true);

            // 设置占位符
            var placeholderComponent = playerInputField.placeholder;
            if (placeholderComponent != null && placeholderComponent is TMP_Text)
            {
                ((TMP_Text)placeholderComponent).text = placeholder;
            }

            playerInputField.text = "";

            // 延迟一帧后激活输入
            StartCoroutine(ActivateInputFieldNextFrame());
        }

        if (sendButton != null)
            sendButton.gameObject.SetActive(true);

        StartCoroutine(ShowPlayerInputCoroutine(placeholder));
    }
    private IEnumerator ActivateInputFieldNextFrame()
    {
        yield return null;
        if (playerInputField != null)
        {
            playerInputField.ActivateInputField();
            playerInputField.Select();
            Debug.Log("InputField 已激活");
        }
    }
    private IEnumerator ShowDialogueCoroutine(string speakerName, string content)
    {
        // 隐藏输入框
        if (playerInputField != null) playerInputField.gameObject.SetActive(false);
        if (sendButton != null) sendButton.gameObject.SetActive(false);

        // 设置说话者
        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        // 隐藏继续指示器
        if (continueIndicator != null)
            continueIndicator.SetActive(false);
        
        // 开始打字效果
        currentFullText = content;
        dialogueContentText.text = content;
        isTyping = false;
        isWaitingForContinue = true;

        if (continueIndicator != null)
            continueIndicator.SetActive(true);
        //if (currentTypingCoroutine != null)
        //    StopCoroutine(currentTypingCoroutine);

        
        //currentTypingCoroutine = StartCoroutine(TypewriterEffect(content));
        
        yield return null;
    }

    private IEnumerator TypewriterEffect(string text)
    {
        dialogueContentText.text = text;
        /*dialogueContentText.text = "";

        for (int i = 0; i < text.Length; i++)
        {
            dialogueContentText.text = text.Substring(0, i + 1);
            //yield return new WaitForSeconds(textSpeed);
            yield return new WaitForSecondsRealtime(textSpeed);
        }
        */
        OnTypingComplete();
        yield return null;
    }

    private void CompleteTyping()
    {
        if (currentTypingCoroutine != null)
        {
            StopCoroutine(currentTypingCoroutine);
        }

        dialogueContentText.text = currentFullText;
        OnTypingComplete();
    }

    private void OnTypingComplete()
    {
        isTyping = false;

        // 显示继续指示器
        if (continueIndicator != null && !isWaitingForPlayerInput)
            continueIndicator.SetActive(true);
    }

    private void ContinueDialogue()
    {
        if (!isWaitingForContinue)
            return;

        Debug.Log("ContinueDialogue 执行");
        isWaitingForContinue = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        currentContinueCallback?.Invoke();
    }

    private IEnumerator ShowPlayerInputCoroutine(string placeholder)
    {
        // 清空对话文本
        if (dialogueContentText != null)
            dialogueContentText.text = "";
        if (speakerNameText != null)
            speakerNameText.text = "Player";
        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        // 显示输入框
        isWaitingForPlayerInput = true;

        if (playerInputField != null)
        {
            playerInputField.gameObject.SetActive(true);

            // 设置占位符
            var placeholderComponent = playerInputField.placeholder;
            if (placeholderComponent != null && placeholderComponent is TMP_Text)
            {
                ((TMP_Text)placeholderComponent).text = placeholder;
            }

            playerInputField.text = "";
            playerInputField.ActivateInputField();
            playerInputField.Select();
        }

        if (sendButton != null)
            sendButton.gameObject.SetActive(true);

        yield return null;
    }

    private void OnInputSubmit(string text)
    {
        // Enter键提交
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            OnSendButtonClick();
        }
    }

    private void OnSendButtonClick()
    {
        if (!isWaitingForPlayerInput || playerInputField == null)
            return;

        string inputText = playerInputField.text.Trim();

        if (string.IsNullOrEmpty(inputText))
            return;

        Debug.Log($"玩家输入: {inputText}");

        // 隐藏输入框
        playerInputField.gameObject.SetActive(false);
        if (sendButton != null)
            sendButton.gameObject.SetActive(false);

        isWaitingForPlayerInput = false;

        // 调用回调
        currentInputCallback?.Invoke(inputText);
    }
}
