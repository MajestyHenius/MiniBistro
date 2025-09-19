using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System;
using UnityEngine.EventSystems;

public class DialogueUIManager : MonoBehaviour
{
    public static DialogueUIManager Instance;

    [Header("UI���")]
    [SerializeField] private GameObject backgroundOverlay;
    [SerializeField] private GameObject dialoguePanel;

    [Header("�Ի�������")]
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueContentText;
    [SerializeField] private GameObject continueIndicator;

    [Header("�������")]
    [SerializeField] private TMP_InputField playerInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button dealButton;
    [Header("����")]
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
        // ����������¼�
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

        // ȷ����ʼ״̬�����ص�
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

        // ��������Ի�
        if (isWaitingForContinue && !isWaitingForPlayerInput)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("���� ContinueDialogue");
                ContinueDialogue();
            }
        }
    }

    /// <summary>
    /// ��ʾ�Ի�UI
    /// </summary>
    public void Show(Action onComplete = null)
    {
        if (backgroundOverlay != null) backgroundOverlay.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        onComplete?.Invoke();
    }

    /// <summary>
    /// ���ضԻ�UI
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
    /// ��ʾһ��Ի�
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
    /// ��ʾ��������
    /// </summary>
    public void ShowPlayerInput(string placeholder = "������...", Action<string> onInputComplete = null)
    {
        Debug.Log("ShowPlayerInput ������");
        currentInputCallback = onInputComplete;

        // ����״̬��־
        isWaitingForContinue = false;
        isWaitingForPlayerInput = true;

        // ��նԻ�����
        if (dialogueContentText != null)
            dialogueContentText.text = "";
        if (speakerNameText != null)
            speakerNameText.text = "Player";
        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        // ��ʾ�����
        if (playerInputField != null)
        {
            playerInputField.gameObject.SetActive(true);

            // ����ռλ��
            var placeholderComponent = playerInputField.placeholder;
            if (placeholderComponent != null && placeholderComponent is TMP_Text)
            {
                ((TMP_Text)placeholderComponent).text = placeholder;
            }

            playerInputField.text = "";

            // �ӳ�һ֡�󼤻�����
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
            Debug.Log("InputField �Ѽ���");
        }
    }
    private IEnumerator ShowDialogueCoroutine(string speakerName, string content)
    {
        // ���������
        if (playerInputField != null) playerInputField.gameObject.SetActive(false);
        if (sendButton != null) sendButton.gameObject.SetActive(false);

        // ����˵����
        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        // ���ؼ���ָʾ��
        if (continueIndicator != null)
            continueIndicator.SetActive(false);
        
        // ��ʼ����Ч��
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

        // ��ʾ����ָʾ��
        if (continueIndicator != null && !isWaitingForPlayerInput)
            continueIndicator.SetActive(true);
    }

    private void ContinueDialogue()
    {
        if (!isWaitingForContinue)
            return;

        Debug.Log("ContinueDialogue ִ��");
        isWaitingForContinue = false;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        currentContinueCallback?.Invoke();
    }

    private IEnumerator ShowPlayerInputCoroutine(string placeholder)
    {
        // ��նԻ��ı�
        if (dialogueContentText != null)
            dialogueContentText.text = "";
        if (speakerNameText != null)
            speakerNameText.text = "Player";
        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        // ��ʾ�����
        isWaitingForPlayerInput = true;

        if (playerInputField != null)
        {
            playerInputField.gameObject.SetActive(true);

            // ����ռλ��
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
        // Enter���ύ
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

        Debug.Log($"�������: {inputText}");

        // ���������
        playerInputField.gameObject.SetActive(false);
        if (sendButton != null)
            sendButton.gameObject.SetActive(false);

        isWaitingForPlayerInput = false;

        // ���ûص�
        currentInputCallback?.Invoke(inputText);
    }
}
