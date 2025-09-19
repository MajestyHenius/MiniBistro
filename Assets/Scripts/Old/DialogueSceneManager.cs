using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueSceneManager : MonoBehaviour
{
    [Header("�Ի�����")]
    public string npcName = "Merchant";
    public string npcRole = "��Դ�м���";

    // ���ڴ������ݵľ�̬����
    public static string NextNPCName;
    public static string NextNPCRole;
    public static string NextDialogue;
    public static string ReturnSceneName;

    // �Ի���ʷ
    private List<ChatMessage> conversationHistory = new List<ChatMessage>();

    void Start()
    {
        // �Ӿ�̬������ȡ����
        if (!string.IsNullOrEmpty(NextNPCName))
        {
            npcName = NextNPCName;
            npcRole = NextNPCRole;
        }

        StartCoroutine(RunDialogue());
    }

    private IEnumerator RunDialogue()
    {
        // �ȴ������������
        yield return new WaitForSeconds(0.5f);

        // ��ʾ�Ի�UI
        DialogueUIManager.Instance.Show();

        // ��ʾ˼������ʾ
        DialogueUIManager.Instance.ShowDialogue(
            npcName,
            "���ҿ���...������׼�����ۣ�",
            () => { }
        );

        // ����AI��ȡ��ʼ����
        string initialRequest = "����ұ����ۣ�����ɹ�һЩ��Դ"; // ����AI���ɱ��۵ĳ�ʼ��Ϣ
        string initialQuote = "";
        bool quoteReceived = false;

        StartCoroutine(AzureOpenAIManager.Instance.GetPPDialogueResponse(
            npcName,
            npcRole,
            initialRequest,
            conversationHistory,
            (response) => {
                initialQuote = response;
                quoteReceived = true;
            }
        ));

        yield return new WaitUntil(() => quoteReceived);

        // ���¶Ի���ʷ
        conversationHistory.Add(new ChatMessage("user", initialRequest));
        conversationHistory.Add(new ChatMessage("assistant", initialQuote));

        // ��ʾAI���ɵĳ�ʼ����
        bool priceShown = false;
        DialogueUIManager.Instance.ShowDialogue(
            npcName,
            initialQuote,  // AI���ɵĳ�ʼ����
            () => priceShown = true
        );

        yield return new WaitUntil(() => priceShown);

        // ��ʼ�Ի�ѭ��
        bool negotiationComplete = false;
        while (!negotiationComplete)
        {
            // �ȴ���������Ӧ
            string playerInput = "";
            bool inputReceived = false;

            DialogueUIManager.Instance.ShowPlayerInput(
                "��������Ļظ�������'�ɽ�'����̸�У�...",
                (input) => {
                    playerInput = input;
                    inputReceived = true;
                }
            );

            yield return new WaitUntil(() => inputReceived);

            Debug.Log($"��һ�Ӧ: {playerInput}");

            // ����Ƿ����̸��
            if (playerInput.Contains("�ɽ�") || playerInput.Contains("ͬ��") || playerInput.Contains("�õ�"))
            {
                DialogueUIManager.Instance.ShowDialogue(
                    npcName,
                    "�ܺã����״�ɣ��ڴ��´κ�����",
                    () => negotiationComplete = true
                );
                yield return new WaitUntil(() => negotiationComplete);
                break;
            }

            // ��ʾ˼����
            DialogueUIManager.Instance.ShowDialogue(
                npcName,
                "���ҿ���һ��...",
                () => { }
            );

            // ��ȡAI��Ӧ
            string npcResponse = "";
            bool responseReceived = false;

            StartCoroutine(AzureOpenAIManager.Instance.GetPPDialogueResponse(
                npcName,
                npcRole,
                playerInput,
                conversationHistory,
                (response) => {
                    npcResponse = response;
                    responseReceived = true;
                }
            ));

            yield return new WaitUntil(() => responseReceived);

            // ���¶Ի���ʷ
            conversationHistory.Add(new ChatMessage("user", playerInput));
            conversationHistory.Add(new ChatMessage("assistant", npcResponse));

            // ��ʾNPC��Ӧ
            bool npcResponseShown = false;
            DialogueUIManager.Instance.ShowDialogue(
                npcName,
                npcResponse,
                () => npcResponseShown = true
            );

            yield return new WaitUntil(() => npcResponseShown);
        }

        // ̸�н������ȴ�һ��Ȼ�󷵻�
        yield return new WaitForSeconds(1.5f);
        ReturnToGameScene();
    }

    private void ReturnToGameScene()
    {
        DialogueUIManager.Instance.Hide();

        if (!string.IsNullOrEmpty(ReturnSceneName))
        {
            SceneManager.LoadScene(ReturnSceneName);
        }
        else
        {
            SceneManager.LoadScene("NegotiationScene");
        }
    }
}