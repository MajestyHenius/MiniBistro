using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueSceneManager : MonoBehaviour
{
    [Header("对话数据")]
    public string npcName = "Merchant";
    public string npcRole = "资源中间商";

    // 用于传递数据的静态变量
    public static string NextNPCName;
    public static string NextNPCRole;
    public static string NextDialogue;
    public static string ReturnSceneName;

    // 对话历史
    private List<ChatMessage> conversationHistory = new List<ChatMessage>();

    void Start()
    {
        // 从静态变量获取数据
        if (!string.IsNullOrEmpty(NextNPCName))
        {
            npcName = NextNPCName;
            npcRole = NextNPCRole;
        }

        StartCoroutine(RunDialogue());
    }

    private IEnumerator RunDialogue()
    {
        // 等待场景加载完成
        yield return new WaitForSeconds(0.5f);

        // 显示对话UI
        DialogueUIManager.Instance.Show();

        // 显示思考中提示
        DialogueUIManager.Instance.ShowDialogue(
            npcName,
            "让我看看...（正在准备报价）",
            () => { }
        );

        // 调用AI获取初始报价
        string initialRequest = "请给我报个价，我想采购一些资源"; // 触发AI生成报价的初始消息
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

        // 更新对话历史
        conversationHistory.Add(new ChatMessage("user", initialRequest));
        conversationHistory.Add(new ChatMessage("assistant", initialQuote));

        // 显示AI生成的初始报价
        bool priceShown = false;
        DialogueUIManager.Instance.ShowDialogue(
            npcName,
            initialQuote,  // AI生成的初始报价
            () => priceShown = true
        );

        yield return new WaitUntil(() => priceShown);

        // 开始对话循环
        bool negotiationComplete = false;
        while (!negotiationComplete)
        {
            // 等待玩家输入回应
            string playerInput = "";
            bool inputReceived = false;

            DialogueUIManager.Instance.ShowPlayerInput(
                "请输入你的回复（输入'成交'结束谈判）...",
                (input) => {
                    playerInput = input;
                    inputReceived = true;
                }
            );

            yield return new WaitUntil(() => inputReceived);

            Debug.Log($"玩家回应: {playerInput}");

            // 检查是否结束谈判
            if (playerInput.Contains("成交") || playerInput.Contains("同意") || playerInput.Contains("好的"))
            {
                DialogueUIManager.Instance.ShowDialogue(
                    npcName,
                    "很好，交易达成！期待下次合作。",
                    () => negotiationComplete = true
                );
                yield return new WaitUntil(() => negotiationComplete);
                break;
            }

            // 显示思考中
            DialogueUIManager.Instance.ShowDialogue(
                npcName,
                "让我考虑一下...",
                () => { }
            );

            // 获取AI回应
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

            // 更新对话历史
            conversationHistory.Add(new ChatMessage("user", playerInput));
            conversationHistory.Add(new ChatMessage("assistant", npcResponse));

            // 显示NPC回应
            bool npcResponseShown = false;
            DialogueUIManager.Instance.ShowDialogue(
                npcName,
                npcResponse,
                () => npcResponseShown = true
            );

            yield return new WaitUntil(() => npcResponseShown);
        }

        // 谈判结束，等待一下然后返回
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