using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Networking;


[System.Serializable]
public class AzureOpenAIRequest
{
    public List<ChatMessage> messages;
    public int max_tokens = 4096;
    public float temperature = 1.0f;
    public float top_p = 1.0f;
}

[System.Serializable]
public class ChatMessage
{
    public string role;
    public string content;

    public ChatMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
}

[System.Serializable]
public class AzureOpenAIResponse
{
    public List<Choice> choices;
}

[System.Serializable]
public class Choice
{
    public Message message;
}

[System.Serializable]
public class Message
{
    public string role;
    public string content;
}

// 对话参与者信息类
public class DialogueParticipant
{
    public string name;
    public string role;
    public string personality;
    public string currentState;
    public Dictionary<string, object> additionalInfo;

    public DialogueParticipant(string name, string role, string personality = "", string currentState = "")
    {
        this.name = name;
        this.role = role;
        this.personality = personality;
        this.currentState = currentState;
        this.additionalInfo = new Dictionary<string, object>();
    }

    // 从GameObject自动构建参与者信息
    public static DialogueParticipant FromGameObject(GameObject obj)
    {
        if (obj.CompareTag("NPC"))
        {
            var npcBehavior = obj.GetComponent<NPCBehavior>();
            if (npcBehavior != null)
            {
                var participant = new DialogueParticipant(
                    npcBehavior.npcName,
                    "服务员",
                    npcBehavior.personality,
                    npcBehavior.GetWaiterState()
                );
                participant.additionalInfo["health"] = npcBehavior.health;
                participant.additionalInfo["energy"] = npcBehavior.energy;
                participant.additionalInfo["mood"] = npcBehavior.mood;
                return participant;
            }
        }
        else if (obj.CompareTag("Customer"))
        {
            var customer = obj.GetComponent<CustomerNPC>();
            if (customer != null)
            {
                var participant = new DialogueParticipant(
                    customer.customerName,
                    "顾客",
                    customer.personality,
                    customer.GetCustomerState()
                );
                participant.additionalInfo["satisfaction"] = customer.satisfaction;
                return participant;
            }
        }
        else if (obj.CompareTag("Player"))
        {
            return new DialogueParticipant("老板", "餐厅老板", "专业、友善", "管理中");
        }

        return new DialogueParticipant("未知", "路人", "", "");
    }
}

public class AzureOpenAIManager : MonoBehaviour
{
    private static AzureOpenAIManager instance;
    public static AzureOpenAIManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("AzureOpenAIManager");
                instance = go.AddComponent<AzureOpenAIManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    private string apiUrl => ConfigManager.Instance.Config?.GetApiUrl() ?? "";
    private string subscriptionKey => ConfigManager.Instance.Config?.subscriptionKey ?? "";


    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log($"[AzureOpenAI] Awake called.");

            // 等待ConfigManager初始化
            if (!ConfigManager.Instance.IsConfigValid())
            {
                Debug.LogError("[AzureOpenAI] API配置无效或缺失！请检查Config/AzureOpenAIConfig.json文件");
            }
            else
            {
                Debug.Log("[AzureOpenAI] API配置加载成功");
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    #region 顾客-服务员
    // 顾客的决定
    public IEnumerator GetCustomerDecision(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {

        List<string> temp = RestaurantManager.GetNearbyCustomerStatus(customer,10f);  //读取周围顾客状态，只有点菜前触发，用于辅助点菜
        string otherTableInformation = string.Join(" ", temp);
        //Debug.Log($"周边桌信息：{otherTableInformation}");
        Debug.Log($"决策时的对话记录：{customer.dialogueHistory}");
        string prompt = $@"顾客{customer.customerName}正在餐厅，服务员说：'{waiterMessage}'

顾客信息：
- 性格：{customer.personality}（积极/消极/普通）。
- 背景：{customer.story}
- 喜爱菜品：{string.Join(",", customer.favoriteDishes)}
- 以往用餐记录：{string.Join("\n", customer.memoryList)}
- 对话记录：{string.Join("\n", customer.dialogueHistory)}
- 周围餐桌信息：{otherTableInformation}
- 已发生对话轮数：{customer.orderDialogueRound}
- 菜单：{RestaurantManager.menuItems}
你扮演顾客，请根据顾客的性格、背景故事和当前情况决定如何回应：
1.如果背景故事比较丰富，可能倾向于先闲聊而后点菜。但闲聊会增加对话轮数，已发生的对话轮数不要超过3。
2. 如果选择点餐，必须选择菜单中有的菜品。回复格式：ORDER|菜品名称|点菜对话
例如：""ORDER|清蒸鲈鱼|今天想吃鱼了，要一份清蒸鲈鱼吧。""
3. 如果选择闲聊，回复格式：CHAT|闲聊内容
例如：""CHAT|你们今天生意看上去不错啊。""
4. 喜爱菜品可能不在菜单中，但如果顾客性格执着或者执意挑刺儿找茬，可以选择以闲聊的方式回复，回复格式：CHAT|闲聊内容
例如：""CHAT|我想要大闸蟹，你们怎么连大闸蟹都没有啊？""
5. 结合历史聊天对话，如果你不满意可以选择离开，回复格式：EXIT|离开对话
例如：""EXIT|服务员态度那么差，我不在你们这吃了！""
6. 结合历史聊天对话，如果你不满意还可以选择叫经理，回复格式：ANGER|呼叫对话
例如：""ANGER|你这服务员什么态度？叫你们经理过来！""
请确保回复符合顾客的性格：
- 积极性格：友好、热情
- 消极性格：可能不耐烦、愤怒、挑剔甚至故意找茬（如果背景中有的话）
- 普通性格：中性、礼貌";
        yield return SendDialogueRequest(prompt, "请生成顾客回应", 100, 0.7f, onResponse);
    }

    // 顾客进门说话
    public IEnumerator GetCustomerEnteringDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {
        string prompt = $@"顾客{customer.customerName}在餐厅排到位置了
顾客信息：
- 性格：{customer.personality}（积极/消极/普通）。
- 背景：{customer.story}
- 喜爱菜品：{string.Join(",", customer.favoriteDishes)}
- 以往用餐记录：{string.Join("\n", customer.memoryList)}
你扮演顾客，刚进入餐厅，请根据顾客的性格和背景故事和当前情况说一句话，20字以内，例如""你好，我想用餐""";
        yield return SendDialogueRequest(prompt, "请生成顾客回应", 100, 0.7f, onResponse);
    }


    public IEnumerator GetCustomerOrderReadyDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {
        string prompt = $@"顾客{customer.customerName}在餐厅点餐后等待了{customer.waitTime_order}后，上菜了。
顾客信息：
- 性格：{customer.personality}（积极/消极/普通）。
- 背景：{customer.story}
- 喜爱菜品：{string.Join(",", customer.favoriteDishes)}
- 以往用餐记录：{string.Join("\n", customer.memoryList)}
- 对话记录：{string.Join("\n", customer.orderDialogueRound)}
你扮演顾客，服务员上菜了，请根据顾客的性格和背景故事和当前情况说一句话，20字以内，例如""看上去不错，我开吃了。""、""才上菜啊，我都快饿昏了""";
        yield return SendDialogueRequest(prompt, "请生成顾客回应", 100, 0.7f, onResponse);
    }



    public IEnumerator GetCustomerGettingSeatDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {
        string prompt = $@"顾客{customer.customerName}在餐厅排到位置了，服务员刚才说:{waiterMessage}
顾客信息：
- 性格：{customer.personality}（积极/消极/普通）。
- 背景：{customer.story}
- 喜爱菜品：{string.Join(",", customer.favoriteDishes)}
- 以往用餐记录：{string.Join("\n", customer.memoryList)}
你扮演顾客，刚进入餐厅，被服务员领入座位。请根据顾客的性格和背景故事和当前情况说一句话，20字以内，例如服务员问你几个人时，回答""就我一个""，服务员单纯说里边请，你可以返回""...""或者""好。""";
        yield return SendDialogueRequest(prompt, "请生成顾客回应", 100, 0.7f, onResponse);
    }


    // 服务员先打招呼
    public IEnumerator GenerateWaiterGreetingToCustomer(NPCData waiter, CustomerNPC customer, System.Action<string> onResponse = null)
    {
        string stateDescription = GetStateDescription(waiter.currentState);

        string systemPrompt = $@"你是餐厅服务员{waiter.npcName}，正在接待顾客{customer.customerName}。
由于你是服务员，你不认识顾客，你不能称呼顾客姓名，你只能在后台通过顾客姓名判断顾客性别，称呼其为先生或者女士。
【你的当前状态】
- 正在：{stateDescription}
- 体力值：{waiter.energy}/{waiter.maxEnergy}
- 心情值：{waiter.mood}/{waiter.maxMood}
- 性格：{waiter.personality}
【顾客信息】
- 性格：{customer.personality}
- {(customer.returnIndex > 0 ? $"回头客指数：{customer.returnIndex}" : "新顾客")}

【决策规则】
1. 根据你的性格和当前状态选择问候方式
2. 如果体力值很低（低于30%），语气可能略显疲惫
3. 如果心情值很低（低于30%），语气可能比较低落
4. 如果顾客是回头客，可以更热情一些
5. 根据顾客性格调整问候方式（对急躁顾客更直接，对耐心顾客更友好）
6. 如果顾客不知道点什么或要求你推荐菜品，你可以从菜单中推荐popularity高的菜
【回复要求】
- 问候语要自然，不超过20个字
- 用第一人称，直接说出问候语
- 示例：'您好，请问今天想点什么？'或'欢迎光临！今天有什么特别想吃的吗？'";

        yield return SendDialogueRequest(systemPrompt, "请生成问候语", 50, 0.7f, onResponse);
    }

    public IEnumerator GenerateWaiterResponseToCall(NPCData waiter, CustomerNPC customer, string callContent, System.Action<string> onResponse = null)
    {
        string stateDescription = GetStateDescription(waiter.currentState);

        string systemPrompt = $@"你是餐厅服务员{waiter.npcName}，顾客{customer.customerName}叫你：'{callContent}'

【你的当前状态】
- 正在：{stateDescription}
- 体力值：{waiter.energy}/{waiter.maxEnergy}
- 心情值：{waiter.mood}/{waiter.maxMood}
- 性格：{waiter.personality}
【顾客信息】
- 性格：{customer.personality}
- 等待时间：{customer.waitTime_order:F0}分钟
- {(customer.returnIndex > 0 ? $"回头客指数：{customer.returnIndex}" : "新顾客")}

【回应规则】
1. 根据顾客的叫唤内容和你的当前状态做出适当回应
2. 如果顾客在催菜，请安抚并说明情况
3. 如果顾客有其他需求，请询问具体需要什么帮助
4. 如果体力值很低（低于30%），语气可能略显疲惫
5. 如果心情值很低（低于30%），语气可能比较低落
6. 根据顾客性格调整回应方式（对急躁顾客更安抚，对耐心顾客更友好）

【回复要求】
- 回应要自然专业，不超过20个字
- 用第一人称，直接说出回应内容
- 示例：'马上为您查看菜品进度' 或 '请问有什么需要帮助的吗？'";

        yield return SendDialogueRequest(systemPrompt, "请生成服务员回应", 60, 0.7f, onResponse);
    }


    #endregion

    #region 顾客-玩家
    // 顾客向经理抱怨
    public IEnumerator GetCustomerComplaint(CustomerNPC customer, string previousEvent, System.Action<string> onResponse = null)
    {
        string systemPrompt = $@"你是餐厅顾客{customer.customerName}。
性格：{customer.personality}
背景：{customer.story}
当前状态：{customer.GetCustomerState()}
之前发生的问题：{previousEvent}

你正在向餐厅经理抱怨你遇到的问题。
请根据你的性格和背景，用第一人称向经理表达你的不满和问题。
要求：
1. 表达要符合你的性格
2. 直接说出你的抱怨，不要有前缀（如'我会说'）
3. 简短，不超过30个字";

        yield return SendDialogueRequest(systemPrompt, "请生成抱怨内容", 100, 0.8f, onResponse);
    }

    // 顾客回复经理的回应
    public IEnumerator GetCustomerReplyToManager(CustomerNPC customer, string managerMessage, string previousEvent, System.Action<string> onResponse = null)
    {
        string systemPrompt = $@"你是餐厅顾客{customer.customerName}。
性格：{customer.personality}
背景：{customer.story}
当前状态：{customer.GetCustomerState()}
之前发生的问题：{previousEvent}

经理对你说：'{managerMessage}'

请根据你的性格和背景，用第一人称回复经理。
要求：
1. 表达要符合你的性格
2. 直接说出你的回应，不要有前缀（如'我会说'）
3. 简短，不超过30个字";

        yield return SendDialogueRequest(systemPrompt, $"经理说：{managerMessage}", 100, 0.8f, onResponse);
    }

    // 顾客在紧急状态下的决策
    public IEnumerator GetCustomerEmergencyDecision(CustomerNPC customer, string previousEvent, System.Action<string> onResponse = null)
    {
        string systemPrompt = $@"你是餐厅顾客{customer.customerName}。
性格：{customer.personality}
背景：{customer.story}
当前状态：紧急状态（需要经理处理）
之前发生的问题：{previousEvent}
与经理的对话记录：{string.Join("\n", customer.dialogueHistory)}

请根据当前情况和你的性格决定下一步行动：
1. 如果问题已解决，愿意继续用餐：RESUME|继续用餐的理由
2. 如果问题未解决，决定离开：EXIT|离开时说的话
3. 如果还需要更多沟通：CONTINUE|需要进一步沟通的内容

请确保决策符合你的性格：
- 积极性格：可能更容易原谅，语气更友好
- 消极性格：可能更固执，语气更强硬
- 普通性格：中性、理性";

        yield return SendDialogueRequest(systemPrompt, "请生成决策", 100, 0.7f, onResponse);
    }
    #endregion

    #region 新的统一对话接口
    //特殊的：服务员主动向老板问好。
    public IEnumerator GenerateWaiterToPlayerGreeting(object waiter, string situation = "", System.Action<string> onResponse = null)
    {
        // 获取服务员信息
        NPCData npcData = null;
        if (waiter is NPCBehavior)
        {
            npcData = new NPCData((NPCBehavior)waiter);
        }
        else if (waiter is NPCData)
        {
            npcData = (NPCData)waiter;
        }
        else
        {
            Debug.LogError("[Azure AI] 无效的服务员对象类型");
            onResponse?.Invoke("老板好！");
            yield break;
        }

        // 构建服务员主动问候老板的系统提示词
        string systemPrompt = BuildWaiterToPlayerGreetingPrompt(npcData, situation);

        // 创建请求
        AzureOpenAIRequest request = new AzureOpenAIRequest
        {
            messages = new List<ChatMessage>
        {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", "请根据当前情况，决定是否要和老板打招呼，如果要打招呼请说一句话，以第一人称口吻，不要说'我会回复'、'我会说'等前置描述。如果不打招呼就回复'...'")
        },
            max_tokens = 50,
            temperature = 0.8f
        };

        string jsonData = JsonUtility.ToJson(request);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            webRequest.SetRequestHeader("api-key", subscriptionKey);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    byte[] bytes = webRequest.downloadHandler.data;
                    string utf8Response = Encoding.UTF8.GetString(bytes);

                    AzureOpenAIResponse response = JsonUtility.FromJson<AzureOpenAIResponse>(utf8Response);
                    if (response.choices != null && response.choices.Count > 0)
                    {
                        string waiterGreeting = response.choices[0].message.content;
                        onResponse?.Invoke(waiterGreeting);
                    }
                    else
                    {
                        onResponse?.Invoke("老板好！");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Azure AI] 解析响应失败: {e.Message}");
                    onResponse?.Invoke("老板好！");
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] 请求失败: {webRequest.error}");
                onResponse?.Invoke("老板好！");
            }
        }
    }

    // 构建服务员主动向老板问候的系统提示词
    private string BuildWaiterToPlayerGreetingPrompt(NPCData npcData, string situation)
    {
        string stateDescription = GetStateDescription(npcData.currentState);

        return $@"你是餐厅服务员{npcData.npcName}，你的老板刚刚走到你附近。

【你的当前状态】
- 正在：{stateDescription}
- 体力值：{npcData.energy}/{npcData.maxEnergy}
- 心情值：{npcData.mood}/{npcData.maxMood}
- 性格：{npcData.personality}
- 今日小费：{npcData.money}金币

【具体情况】
{(string.IsNullOrEmpty(situation) ? "老板经过你身边" : situation)}

【决策规则】
1. 如果你正在忙碌（如正在上菜、点菜、引导顾客入座），可能只是点头示意或不打招呼（回复'...'）
2. 如果你比较空闲，可以根据性格选择问候老板，或者不问候（回复'...'）
3. 如果体力值很低（低于30%），表现出疲惫
4. 如果心情值很低（低于30%），语气可能比较低落
5. 如果今天小费不错，可以向老板汇报好消息
6. 根据你的性格特点来决定问候方式

【回复要求】
- 如果决定打招呼：简短自然，不超过15个字，符合你的性格
- 如果决定不打招呼：只回复'...'
- 不要太正式，要符合实际工作场景";
    }

    public IEnumerator GenerateGreeting(object speaker, object listener, string situation = "", System.Action<string> onResponse = null)
    {
        DialogueParticipant speakerInfo = GetParticipantInfo(speaker);
        DialogueParticipant listenerInfo = GetParticipantInfo(listener);

        string systemPrompt = $@"你是{speakerInfo.role}{speakerInfo.name}。
性格：{speakerInfo.personality}
当前状态：{speakerInfo.currentState}

你遇到了{listenerInfo.role}{listenerInfo.name}。
场景：{(string.IsNullOrEmpty(situation) ? "日常工作中" : situation)}

请生成一句符合你身份和性格的问候或主动对话。
要求：
1. 简短自然，不超过20个字,用第一人称，不要说'我会回复'、'我会说'等前置描述
2. 符合当前场景和状态
3. 体现角色关系（如服务员对老板要恭敬，对顾客要热情）";

        yield return SendDialogueRequest(systemPrompt, "请说一句话", 50, 0.8f, onResponse);
    }


    public IEnumerator GenerateReply(object speaker, object listener, string previousMessage, System.Action<string> onResponse = null)
    {
        DialogueParticipant speakerInfo = GetParticipantInfo(speaker);
        DialogueParticipant listenerInfo = GetParticipantInfo(listener);

        string systemPrompt = $@"你是{speakerInfo.role}{speakerInfo.name}。
性格：{speakerInfo.personality}
当前状态：{speakerInfo.currentState}

{listenerInfo.role}{listenerInfo.name}刚刚对你说：‘{ previousMessage}
        ’
请根据你的身份和性格回复。
要求：
1.回复简短自然，不超过30个字，用第一人称，不要说'我会回复'、'我会说'等前置描述
2.符合角色关系和当前状态
3.如果涉及具体业务（如点餐、结账），要给出明确回应";
        yield return SendDialogueRequest(systemPrompt, $"回复：{previousMessage}", 100, 0.8f, onResponse);
    }

    public IEnumerator GenerateWaiterTakingOrderReply(CustomerNPC customer,NPCBehavior waiter, string previousMessage, System.Action<string> onResponse = null)
    {
        //专门用于回复非点菜闲聊的
        //DialogueParticipant speakerInfo = GetParticipantInfo(speaker);
        //DialogueParticipant listenerInfo = GetParticipantInfo(listener);

        string systemPrompt = $@"你是{waiter.occupation}{waiter.npcName}。
性格：{customer.personality}
你正在为顾客点单，
饭店的菜单是{RestaurantManager.Instance.RestaurantMenu}
顾客刚刚对你说：‘{previousMessage}’
历史对话：{customer.dialogueHistory}
他有可能在闲聊或者想点菜单上不存在的菜品，
请根据你服务员的身份和您的性格回复。
要求：
1.回复简短自然，不超过30个字，用第一人称，不要说'我会回复'、'我会说'等前置描述
2.符合角色关系和当前状态
3.如果你的性格是积极则倾向于顺着对方说；如果是你和对方都是消极性格，则有可能受不了对方的语气，从而可能产生冲突对话";
        yield return SendDialogueRequest(systemPrompt, $"回复：{previousMessage}", 100, 0.8f, onResponse);
    }


    public IEnumerator GenerateWaiterFinshTakingOrderReply(CustomerNPC customer, NPCBehavior waiter, string previousMessage, System.Action<string> onResponse = null)
    {
        //专门用于回复点完菜品的
        string systemPrompt = $@"你是{waiter.occupation}{waiter.npcName}。
性格：{customer.personality}
你正在为顾客点单，
顾客刚刚对你说：‘{previousMessage}’
顾客选择的菜品是{customer.GetOrderedFood()}
历史对话：{customer.dialogueHistory}
请根据你服务员的身份和性格回复。
要求：
1.回复简短自然，不超过30个字，第一人称，不要说'我会回复'、'我会说'等前置描述，不要疑问句。
2.顾客的回复已经是最终确定的点餐，不需要再添加新菜，回复类似""好的""即可。
3.符合角色关系和当前状态";
        yield return SendDialogueRequest(systemPrompt, $"回复：{previousMessage}", 100, 0.8f, onResponse);
    }

    private DialogueParticipant GetParticipantInfo(object participant)
    {
        if (participant is DialogueParticipant)
        {
            return (DialogueParticipant)participant;
        }
        else if (participant is GameObject)
        {
            return DialogueParticipant.FromGameObject((GameObject)participant);
        }
        else if (participant is NPCData)
        {
            NPCData npc = (NPCData)participant;
            return new DialogueParticipant(npc.npcName, "服务员", npc.personality, npc.currentState);
        }
        else if (participant is NPCBehavior)
        {
            NPCBehavior npc = (NPCBehavior)participant;
            return new DialogueParticipant(npc.npcName, "服务员", npc.personality, npc.GetWaiterState());
        }
        else if (participant is CustomerNPC)
        {
            CustomerNPC customer = (CustomerNPC)participant;
            return new DialogueParticipant(customer.customerName, "顾客", customer.personality, customer.GetCustomerState());
        }
        else if (participant is string)
        {
            // 如果传入的是字符串，假设是玩家
            return new DialogueParticipant("老板", "餐厅老板", "专业、友善", "管理中");
        }

        return new DialogueParticipant("未知", "路人", "", "");
    }


    public IEnumerator SendDialogueRequest(string systemPrompt, string userPrompt, int maxTokens, float temperature, System.Action<string> onResponse)
    {
        AzureOpenAIRequest request = new AzureOpenAIRequest
        {
            messages = new List<ChatMessage>
            {
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userPrompt)
            },
            max_tokens = maxTokens,
            temperature = temperature
        };

        string jsonData = JsonUtility.ToJson(request);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            webRequest.SetRequestHeader("api-key", subscriptionKey);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    byte[] bytes = webRequest.downloadHandler.data;
                    string utf8Response = Encoding.UTF8.GetString(bytes);

                    AzureOpenAIResponse response = JsonUtility.FromJson<AzureOpenAIResponse>(utf8Response);
                    if (response.choices != null && response.choices.Count > 0)
                    {
                        string reply = response.choices[0].message.content;
                        onResponse?.Invoke(reply);
                    }
                    else
                    {
                        onResponse?.Invoke(GetDefaultResponse(systemPrompt));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Azure AI] 解析响应失败: {e.Message}");
                    onResponse?.Invoke(GetDefaultResponse(systemPrompt));
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] 请求失败: {webRequest.error}");
                onResponse?.Invoke(GetDefaultResponse(systemPrompt));
            }
        }
    }

    private string GetDefaultResponse(string context)
    {
        if (context.Contains("问候"))
            return "你好！";
        else if (context.Contains("服务员") && context.Contains("顾客"))
            return "欢迎光临！";
        else if (context.Contains("服务员") && context.Contains("老板"))
            return "老板好！";
        else
            return "好的";
    }

    #endregion

    #region 保留原有的特定方法（向后兼容）

    // 保留原有的GetNPCResponse方法
    public IEnumerator GetNPCResponse(NPCData npcData, string playerMessage, System.Action<string> onResponse)
    {
        // 使用新的回复型对话接口
        DialogueParticipant npc = new DialogueParticipant(npcData.npcName, "服务员", npcData.personality, npcData.currentState);
        DialogueParticipant player = new DialogueParticipant("老板", "餐厅老板", "专业、友善", "管理中");

        yield return GenerateReply(npc, player, playerMessage, onResponse);
    }

    // 保留原有的GetCustomerResponse方法，通用的
    public IEnumerator GetCustomerResponse(CustomerNPC customer, string prompt, System.Action<string> onResponse)
    {
        AzureOpenAIRequest request = new AzureOpenAIRequest
        {
            messages = new List<ChatMessage>
            {
                new ChatMessage("user", prompt)
            },
            max_tokens = 1000,
            temperature = 0.7f
        };
        string jsonData = JsonUtility.ToJson(request);
        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            webRequest.SetRequestHeader("api-key", subscriptionKey);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    byte[] bytes = webRequest.downloadHandler.data;
                    string utf8Response = Encoding.UTF8.GetString(bytes);

                    AzureOpenAIResponse response = JsonUtility.FromJson<AzureOpenAIResponse>(utf8Response);
                    if (response.choices != null && response.choices.Count > 0)
                    {
                        string customerReply = response.choices[0].message.content;
                        onResponse?.Invoke(customerReply);
                    }
                    else
                    {
                        onResponse?.Invoke("{}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Azure AI] 解析顾客响应失败: {e.Message}");
                    onResponse?.Invoke("{}");
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] 顾客请求失败: {webRequest.error}");
                onResponse?.Invoke("{}");
            }
        }
    }

    #region 原来的砍价系统
    public IEnumerator GetPPDialogueResponse(string npcName, string npcRole, string playerMessage,
        List<ChatMessage> conversationHistory, System.Action<string> onResponse)
    {
        string systemPrompt = BuildPPDialoguePrompt(npcName, npcRole);

        List<ChatMessage> messages = new List<ChatMessage>();
        messages.Add(new ChatMessage("system", systemPrompt));

        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            messages.AddRange(conversationHistory);
        }

        messages.Add(new ChatMessage("user", playerMessage));

        AzureOpenAIRequest request = new AzureOpenAIRequest
        {
            messages = messages,
            max_tokens = 200,
            temperature = 0.8f
        };

        string jsonData = JsonUtility.ToJson(request);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            webRequest.SetRequestHeader("api-key", subscriptionKey);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    byte[] bytes = webRequest.downloadHandler.data;
                    string utf8Response = Encoding.UTF8.GetString(bytes);

                    AzureOpenAIResponse response = JsonUtility.FromJson<AzureOpenAIResponse>(utf8Response);
                    if (response.choices != null && response.choices.Count > 0)
                    {
                        string npcReply = response.choices[0].message.content;
                        onResponse?.Invoke(npcReply);
                    }
                    else
                    {
                        onResponse?.Invoke("（思考中...）");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Dialogue] 解析响应失败: {e.Message}");
                    onResponse?.Invoke("抱歉，我现在有点迷糊。");
                }
            }
            else
            {
                Debug.LogError($"[Dialogue] 请求失败: {webRequest.error}");
                onResponse?.Invoke("（无法理解你说的话）");
            }
        }
    }

    private string BuildPPDialoguePrompt(string npcName, string npcRole)
    {
        string fileName = $"BuyerRecord.json";
        string projectPath = Path.GetDirectoryName(Application.dataPath);
        string path = Path.Combine(projectPath, "BuyerLogs");

        string fullPath = Path.Combine(path, fileName);
        string buyerlog = LoadLogAsString(fullPath);
        Debug.Log(buyerlog);
        return $@"你是一个游戏中的NPC角色中间商，正在与买家进行重要对话。 
你需要根据市场情况和收购记录给买家报价。
结合你之前的收购日志给买家开一个价格，价格不能低于收购日志的平均价格。 
你的收购日志是：{buyerlog} 
重要规则： 
1. 当买家询问价格或想要采购时，你需要主动报出具体的价格 
2. 报价时要说明价格的单位（比如：每单位木材XX金币） 
3. 可以适当说明价格的合理性 4. 保持友好但专业的商人口吻 
5. 如果买家还价，你可以适当让步，但不能低于成本价
6. 暂时返回英语版本，in English, please";
    }
    #endregion
    // 保留买家砍价的特定方法


    public string LoadLogAsString(string filename)
    {
        string content = File.ReadAllText(filename);
        Debug.Log($"路径是：{filename}");
        Debug.Log(content);
        return content;
    }

    private string GetStateDescription(string state)
    {
        switch (state)
        {
            case "Working": return "工作";
            case "Eating": return "吃饭";
            case "Sleeping": return "睡觉";
            case "MovingToDestination": return "赶路";
            case "Idle": return "";
            case "Shopping": return "交易";
            case "Entertaining": return "休闲活动";
            default: return "休息";
        }
    }

    #endregion


    #region 顾客评价
    // 方法1: 顾客排队没人迎接，愤怒离去
    public IEnumerator GetCustomerReviewNoGreeting(CustomerNPC customer, float waitTime, float benchmarkWaitTime, System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "no_greeting", benchmarkWaitTime);
        //string userPrompt = $"顾客在餐厅入口等待了{waitTime:F1}分钟，而餐厅的标准等待时间是{benchmarkWaitTime:F1}分钟。";
        string userPrompt = $"顾客在餐厅入口等待了{waitTime:F1}分钟。"; // 标准等待时间影响权重可能过大，当前删除。
        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }

    // 方法2: 顾客等上菜超时，愤怒离去
    public IEnumerator GetCustomerReviewFoodDelay(CustomerNPC customer, float waitTime, float waitTimeOrder,
                                                 float benchmarkWaitTime, float benchmarkOrderTime,string orderName, System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "food_delay", benchmarkWaitTime, benchmarkOrderTime);
        //string userPrompt = $"顾客排队等待了{waitTime:F1}分钟（标准：{benchmarkWaitTime:F1}分钟），" +
        //                   $"点了{orderName}，等待上菜{waitTimeOrder:F1}分钟（标准：{benchmarkOrderTime:F1}分钟）。";
        string userPrompt = $"顾客排队等待了{waitTime:F1}分钟，" +
                           $"点了{orderName}，等待上菜{waitTimeOrder:F1}分钟。"; // 标准等待时间影响权重可能过大，当前删除。



        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }

    // 方法3: 顾客顺利完成用餐
    public IEnumerator GetCustomerReviewSuccess(CustomerNPC customer, float waitTime, float waitTimeOrder,
                                               float benchmarkWaitTime, float benchmarkOrderTime, string orderName, System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "success", benchmarkWaitTime, benchmarkOrderTime);
        //string userPrompt = $"顾客排队等待了{waitTime:F1}分钟（标准：{benchmarkWaitTime:F1}分钟），" +
        //                   $"点了{orderName}菜后等待{waitTimeOrder:F1}分钟（标准：{benchmarkOrderTime:F1}分钟）。";
        string userPrompt = $"顾客排队等待了{waitTime:F1}分钟，" +
                           $"点了{orderName}菜后等待{waitTimeOrder:F1}分钟。";  // 标准等待时间影响权重可能过大，当前删除。
        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }
    // 方法4：ANGER状态
    public IEnumerator GetCustomerReviewAnger(CustomerNPC customer, float waitTime, float waitTimeOrder,
                                                 float benchmarkWaitTime, float benchmarkOrderTime, string orderName, string previousEvent,
                                                 System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "food_delay", benchmarkWaitTime, benchmarkOrderTime);
        string userPrompt = $"顾客排队等待了{waitTime:F1}分钟（标准：{benchmarkWaitTime:F1}分钟），" +
                           $"点了{orderName}，等待上菜{waitTimeOrder:F1}分钟（标准：{benchmarkOrderTime:F1}分钟）。之前发生了：{previousEvent}";

        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }
    // 构建评价系统提示
    private string BuildReviewSystemPrompt(CustomerNPC customer, string reviewType,
                                          float benchmarkWaitTime, float benchmarkOrderTime = 0)
    {
        string context = reviewType switch
        {
            "no_greeting" => $"顾客在餐厅入口等待服务员迎接，标准等待时间是{benchmarkWaitTime:F1}分钟。",
            "food_delay" => $"顾客已入座点菜，标准排队等待时间是{benchmarkWaitTime:F1}分钟，标准上菜等待时间是{benchmarkOrderTime:F1}分钟。",
            "success" => $"顾客顺利完成整个用餐过程，标准排队等待时间是{benchmarkWaitTime:F1}分钟，标准上菜等待时间是{benchmarkOrderTime:F1}分钟。",
            _ => "顾客对餐厅体验进行评价。"
        };

        return $@"你是一个餐厅顾客评价系统。

【顾客信息】
- 姓名：{customer.customerName}
- 背景：{customer.story}
- 性格：{customer.personality}
- 当前状态：{customer.currentState}
- 对话历史：{customer.dialogueHistory}
【评价要求】
1. 根据顾客性格、背景、服务态度和等待时间生成真实可信的餐厅评价
2. 评价内容要符合{reviewType}场景
3. 你与服务员、经理历史对话是生成评价分数的主要因素。
4. 必须返回JSON格式，包含两个字段：
   - ""comment"": 评价内容（50字以内）
   - ""rating"": 整数评分(0-10分，10为最高)
【评分指南】
- 对话十分不愉快，想要叫经理、投诉以及当场离去 → 差评(0-3)
- 等待上菜时间、排队时间较长，但与对方交谈愉快 → 中评(4-7)
- 顺利完成点餐用餐，双方对话舒适愉快 → 好评(8-10)
- 先考虑历史对话后再考虑等待时间。
【当前场景】
{context}";
    }

    // 生成评价的核心方法
    public IEnumerator GenerateReview(string systemPrompt, string userPrompt, System.Action<string, int> onResponse)
    {
        AzureOpenAIRequest request = new AzureOpenAIRequest
        {
            messages = new List<ChatMessage>
        {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userPrompt)
        },
            max_tokens = 200,
            temperature = 0.7f
        };

        string jsonData = JsonUtility.ToJson(request);

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            webRequest.SetRequestHeader("api-key", subscriptionKey);

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string utf8Response = Encoding.UTF8.GetString(webRequest.downloadHandler.data);
                    AzureOpenAIResponse response = JsonUtility.FromJson<AzureOpenAIResponse>(utf8Response);

                    if (response.choices != null && response.choices.Count > 0)
                    {
                        string jsonResponse = response.choices[0].message.content;

                        // 解析JSON响应
                        ReviewResponse review = JsonUtility.FromJson<ReviewResponse>(jsonResponse);
                        onResponse?.Invoke(review.comment, review.rating);
                    }
                    else
                    {
                        onResponse?.Invoke("服务体验一般", 5);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Azure AI] 解析评价响应失败: {e.Message}");
                    onResponse?.Invoke("服务体验一般", 5);
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] 评价请求失败: {webRequest.error}");
                onResponse?.Invoke("服务体验一般", 5);
            }
        }
    }

    // 评价响应数据结构
    [System.Serializable]
    public class ReviewResponse
    {
        public string comment;
        public int rating;
    }
    #endregion
}
