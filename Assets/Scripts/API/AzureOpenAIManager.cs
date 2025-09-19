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

// �Ի���������Ϣ��
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

    // ��GameObject�Զ�������������Ϣ
    public static DialogueParticipant FromGameObject(GameObject obj)
    {
        if (obj.CompareTag("NPC"))
        {
            var npcBehavior = obj.GetComponent<NPCBehavior>();
            if (npcBehavior != null)
            {
                var participant = new DialogueParticipant(
                    npcBehavior.npcName,
                    "����Ա",
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
                    "�˿�",
                    customer.personality,
                    customer.GetCustomerState()
                );
                participant.additionalInfo["satisfaction"] = customer.satisfaction;
                return participant;
            }
        }
        else if (obj.CompareTag("Player"))
        {
            return new DialogueParticipant("�ϰ�", "�����ϰ�", "רҵ������", "������");
        }

        return new DialogueParticipant("δ֪", "·��", "", "");
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

            // �ȴ�ConfigManager��ʼ��
            if (!ConfigManager.Instance.IsConfigValid())
            {
                Debug.LogError("[AzureOpenAI] API������Ч��ȱʧ������Config/AzureOpenAIConfig.json�ļ�");
            }
            else
            {
                Debug.Log("[AzureOpenAI] API���ü��سɹ�");
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    #region �˿�-����Ա
    // �˿͵ľ���
    public IEnumerator GetCustomerDecision(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {

        List<string> temp = RestaurantManager.GetNearbyCustomerStatus(customer,10f);  //��ȡ��Χ�˿�״̬��ֻ�е��ǰ���������ڸ������
        string otherTableInformation = string.Join(" ", temp);
        //Debug.Log($"�ܱ�����Ϣ��{otherTableInformation}");
        Debug.Log($"����ʱ�ĶԻ���¼��{customer.dialogueHistory}");
        string prompt = $@"�˿�{customer.customerName}���ڲ���������Ա˵��'{waiterMessage}'

�˿���Ϣ��
- �Ը�{customer.personality}������/����/��ͨ����
- ������{customer.story}
- ϲ����Ʒ��{string.Join(",", customer.favoriteDishes)}
- �����òͼ�¼��{string.Join("\n", customer.memoryList)}
- �Ի���¼��{string.Join("\n", customer.dialogueHistory)}
- ��Χ������Ϣ��{otherTableInformation}
- �ѷ����Ի�������{customer.orderDialogueRound}
- �˵���{RestaurantManager.menuItems}
����ݹ˿ͣ�����ݹ˿͵��Ը񡢱������º͵�ǰ���������λ�Ӧ��
1.����������±ȽϷḻ�����������������Ķ����ˡ������Ļ����ӶԻ��������ѷ����ĶԻ�������Ҫ����3��
2. ���ѡ���ͣ�����ѡ��˵����еĲ�Ʒ���ظ���ʽ��ORDER|��Ʒ����|��˶Ի�
���磺""ORDER|��������|����������ˣ�Ҫһ����������ɡ�""
3. ���ѡ�����ģ��ظ���ʽ��CHAT|��������
���磺""CHAT|���ǽ������⿴��ȥ������""
4. ϲ����Ʒ���ܲ��ڲ˵��У�������˿��Ը�ִ�Ż���ִ�����̶��Ҳ磬����ѡ�������ĵķ�ʽ�ظ����ظ���ʽ��CHAT|��������
���磺""CHAT|����Ҫ��բз��������ô����բз��û�а���""
5. �����ʷ����Ի�������㲻�������ѡ���뿪���ظ���ʽ��EXIT|�뿪�Ի�
���磺""EXIT|����Ա̬����ô��Ҳ�����������ˣ�""
6. �����ʷ����Ի�������㲻���⻹����ѡ��о����ظ���ʽ��ANGER|���жԻ�
���磺""ANGER|�������Աʲô̬�ȣ������Ǿ��������""
��ȷ���ظ����Ϲ˿͵��Ը�
- �����Ը��Ѻá�����
- �����Ը񣺿��ܲ��ͷ�����ŭ���������������Ҳ磨����������еĻ���
- ��ͨ�Ը����ԡ���ò";
        yield return SendDialogueRequest(prompt, "�����ɹ˿ͻ�Ӧ", 100, 0.7f, onResponse);
    }

    // �˿ͽ���˵��
    public IEnumerator GetCustomerEnteringDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {
        string prompt = $@"�˿�{customer.customerName}�ڲ����ŵ�λ����
�˿���Ϣ��
- �Ը�{customer.personality}������/����/��ͨ����
- ������{customer.story}
- ϲ����Ʒ��{string.Join(",", customer.favoriteDishes)}
- �����òͼ�¼��{string.Join("\n", customer.memoryList)}
����ݹ˿ͣ��ս������������ݹ˿͵��Ը�ͱ������º͵�ǰ���˵һ�仰��20�����ڣ�����""��ã������ò�""";
        yield return SendDialogueRequest(prompt, "�����ɹ˿ͻ�Ӧ", 100, 0.7f, onResponse);
    }


    public IEnumerator GetCustomerOrderReadyDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {
        string prompt = $@"�˿�{customer.customerName}�ڲ�����ͺ�ȴ���{customer.waitTime_order}���ϲ��ˡ�
�˿���Ϣ��
- �Ը�{customer.personality}������/����/��ͨ����
- ������{customer.story}
- ϲ����Ʒ��{string.Join(",", customer.favoriteDishes)}
- �����òͼ�¼��{string.Join("\n", customer.memoryList)}
- �Ի���¼��{string.Join("\n", customer.orderDialogueRound)}
����ݹ˿ͣ�����Ա�ϲ��ˣ�����ݹ˿͵��Ը�ͱ������º͵�ǰ���˵һ�仰��20�����ڣ�����""����ȥ�����ҿ����ˡ�""��""���ϲ˰����Ҷ��������""";
        yield return SendDialogueRequest(prompt, "�����ɹ˿ͻ�Ӧ", 100, 0.7f, onResponse);
    }



    public IEnumerator GetCustomerGettingSeatDialogue(CustomerNPC customer, string waiterMessage, System.Action<string> onResponse = null)
    {
        string prompt = $@"�˿�{customer.customerName}�ڲ����ŵ�λ���ˣ�����Ա�ղ�˵:{waiterMessage}
�˿���Ϣ��
- �Ը�{customer.personality}������/����/��ͨ����
- ������{customer.story}
- ϲ����Ʒ��{string.Join(",", customer.favoriteDishes)}
- �����òͼ�¼��{string.Join("\n", customer.memoryList)}
����ݹ˿ͣ��ս��������������Ա������λ������ݹ˿͵��Ը�ͱ������º͵�ǰ���˵һ�仰��20�����ڣ��������Ա���㼸����ʱ���ش�""����һ��""������Ա����˵����룬����Է���""...""����""�á�""";
        yield return SendDialogueRequest(prompt, "�����ɹ˿ͻ�Ӧ", 100, 0.7f, onResponse);
    }


    // ����Ա�ȴ��к�
    public IEnumerator GenerateWaiterGreetingToCustomer(NPCData waiter, CustomerNPC customer, System.Action<string> onResponse = null)
    {
        string stateDescription = GetStateDescription(waiter.currentState);

        string systemPrompt = $@"���ǲ�������Ա{waiter.npcName}�����ڽӴ��˿�{customer.customerName}��
�������Ƿ���Ա���㲻��ʶ�˿ͣ��㲻�ܳƺ��˿���������ֻ���ں�̨ͨ���˿������жϹ˿��Ա𣬳ƺ���Ϊ��������Ůʿ��
����ĵ�ǰ״̬��
- ���ڣ�{stateDescription}
- ����ֵ��{waiter.energy}/{waiter.maxEnergy}
- ����ֵ��{waiter.mood}/{waiter.maxMood}
- �Ը�{waiter.personality}
���˿���Ϣ��
- �Ը�{customer.personality}
- {(customer.returnIndex > 0 ? $"��ͷ��ָ����{customer.returnIndex}" : "�¹˿�")}

�����߹���
1. ��������Ը�͵�ǰ״̬ѡ���ʺ�ʽ
2. �������ֵ�ܵͣ�����30%����������������ƣ��
3. �������ֵ�ܵͣ�����30%�����������ܱȽϵ���
4. ����˿��ǻ�ͷ�ͣ����Ը�����һЩ
5. ���ݹ˿��Ը�����ʺ�ʽ���Լ���˿͸�ֱ�ӣ������Ĺ˿͸��Ѻã�
6. ����˿Ͳ�֪����ʲô��Ҫ�����Ƽ���Ʒ������ԴӲ˵����Ƽ�popularity�ߵĲ�
���ظ�Ҫ��
- �ʺ���Ҫ��Ȼ��������20����
- �õ�һ�˳ƣ�ֱ��˵���ʺ���
- ʾ����'���ã����ʽ������ʲô��'��'��ӭ���٣�������ʲô�ر���Ե���'";

        yield return SendDialogueRequest(systemPrompt, "�������ʺ���", 50, 0.7f, onResponse);
    }

    public IEnumerator GenerateWaiterResponseToCall(NPCData waiter, CustomerNPC customer, string callContent, System.Action<string> onResponse = null)
    {
        string stateDescription = GetStateDescription(waiter.currentState);

        string systemPrompt = $@"���ǲ�������Ա{waiter.npcName}���˿�{customer.customerName}���㣺'{callContent}'

����ĵ�ǰ״̬��
- ���ڣ�{stateDescription}
- ����ֵ��{waiter.energy}/{waiter.maxEnergy}
- ����ֵ��{waiter.mood}/{waiter.maxMood}
- �Ը�{waiter.personality}
���˿���Ϣ��
- �Ը�{customer.personality}
- �ȴ�ʱ�䣺{customer.waitTime_order:F0}����
- {(customer.returnIndex > 0 ? $"��ͷ��ָ����{customer.returnIndex}" : "�¹˿�")}

����Ӧ����
1. ���ݹ˿͵Ľл����ݺ���ĵ�ǰ״̬�����ʵ���Ӧ
2. ����˿��ڴ߲ˣ��밲����˵�����
3. ����˿�������������ѯ�ʾ�����Ҫʲô����
4. �������ֵ�ܵͣ�����30%����������������ƣ��
5. �������ֵ�ܵͣ�����30%�����������ܱȽϵ���
6. ���ݹ˿��Ը������Ӧ��ʽ���Լ���˿͸������������Ĺ˿͸��Ѻã�

���ظ�Ҫ��
- ��ӦҪ��Ȼרҵ��������20����
- �õ�һ�˳ƣ�ֱ��˵����Ӧ����
- ʾ����'����Ϊ���鿴��Ʒ����' �� '������ʲô��Ҫ��������'";

        yield return SendDialogueRequest(systemPrompt, "�����ɷ���Ա��Ӧ", 60, 0.7f, onResponse);
    }


    #endregion

    #region �˿�-���
    // �˿�����Թ
    public IEnumerator GetCustomerComplaint(CustomerNPC customer, string previousEvent, System.Action<string> onResponse = null)
    {
        string systemPrompt = $@"���ǲ����˿�{customer.customerName}��
�Ը�{customer.personality}
������{customer.story}
��ǰ״̬��{customer.GetCustomerState()}
֮ǰ���������⣺{previousEvent}

���������������Թ�����������⡣
���������Ը�ͱ������õ�һ�˳���������Ĳ��������⡣
Ҫ��
1. ���Ҫ��������Ը�
2. ֱ��˵����ı�Թ����Ҫ��ǰ׺����'�һ�˵'��
3. ��̣�������30����";

        yield return SendDialogueRequest(systemPrompt, "�����ɱ�Թ����", 100, 0.8f, onResponse);
    }

    // �˿ͻظ�����Ļ�Ӧ
    public IEnumerator GetCustomerReplyToManager(CustomerNPC customer, string managerMessage, string previousEvent, System.Action<string> onResponse = null)
    {
        string systemPrompt = $@"���ǲ����˿�{customer.customerName}��
�Ը�{customer.personality}
������{customer.story}
��ǰ״̬��{customer.GetCustomerState()}
֮ǰ���������⣺{previousEvent}

�������˵��'{managerMessage}'

���������Ը�ͱ������õ�һ�˳ƻظ�����
Ҫ��
1. ���Ҫ��������Ը�
2. ֱ��˵����Ļ�Ӧ����Ҫ��ǰ׺����'�һ�˵'��
3. ��̣�������30����";

        yield return SendDialogueRequest(systemPrompt, $"����˵��{managerMessage}", 100, 0.8f, onResponse);
    }

    // �˿��ڽ���״̬�µľ���
    public IEnumerator GetCustomerEmergencyDecision(CustomerNPC customer, string previousEvent, System.Action<string> onResponse = null)
    {
        string systemPrompt = $@"���ǲ����˿�{customer.customerName}��
�Ը�{customer.personality}
������{customer.story}
��ǰ״̬������״̬����Ҫ������
֮ǰ���������⣺{previousEvent}
�뾭��ĶԻ���¼��{string.Join("\n", customer.dialogueHistory)}

����ݵ�ǰ���������Ը������һ���ж���
1. ��������ѽ����Ը������òͣ�RESUME|�����ò͵�����
2. �������δ����������뿪��EXIT|�뿪ʱ˵�Ļ�
3. �������Ҫ���๵ͨ��CONTINUE|��Ҫ��һ����ͨ������

��ȷ�����߷�������Ը�
- �����Ը񣺿��ܸ�����ԭ�£��������Ѻ�
- �����Ը񣺿��ܸ���ִ��������ǿӲ
- ��ͨ�Ը����ԡ�����";

        yield return SendDialogueRequest(systemPrompt, "�����ɾ���", 100, 0.7f, onResponse);
    }
    #endregion

    #region �µ�ͳһ�Ի��ӿ�
    //����ģ�����Ա�������ϰ��ʺá�
    public IEnumerator GenerateWaiterToPlayerGreeting(object waiter, string situation = "", System.Action<string> onResponse = null)
    {
        // ��ȡ����Ա��Ϣ
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
            Debug.LogError("[Azure AI] ��Ч�ķ���Ա��������");
            onResponse?.Invoke("�ϰ�ã�");
            yield break;
        }

        // ��������Ա�����ʺ��ϰ��ϵͳ��ʾ��
        string systemPrompt = BuildWaiterToPlayerGreetingPrompt(npcData, situation);

        // ��������
        AzureOpenAIRequest request = new AzureOpenAIRequest
        {
            messages = new List<ChatMessage>
        {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", "����ݵ�ǰ����������Ƿ�Ҫ���ϰ���к������Ҫ���к���˵һ�仰���Ե�һ�˳ƿ��ǣ���Ҫ˵'�һ�ظ�'��'�һ�˵'��ǰ����������������к��ͻظ�'...'")
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
                        onResponse?.Invoke("�ϰ�ã�");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Azure AI] ������Ӧʧ��: {e.Message}");
                    onResponse?.Invoke("�ϰ�ã�");
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] ����ʧ��: {webRequest.error}");
                onResponse?.Invoke("�ϰ�ã�");
            }
        }
    }

    // ��������Ա�������ϰ��ʺ��ϵͳ��ʾ��
    private string BuildWaiterToPlayerGreetingPrompt(NPCData npcData, string situation)
    {
        string stateDescription = GetStateDescription(npcData.currentState);

        return $@"���ǲ�������Ա{npcData.npcName}������ϰ�ո��ߵ��㸽����

����ĵ�ǰ״̬��
- ���ڣ�{stateDescription}
- ����ֵ��{npcData.energy}/{npcData.maxEnergy}
- ����ֵ��{npcData.mood}/{npcData.maxMood}
- �Ը�{npcData.personality}
- ����С�ѣ�{npcData.money}���

�����������
{(string.IsNullOrEmpty(situation) ? "�ϰ徭�������" : situation)}

�����߹���
1. ���������æµ���������ϲˡ���ˡ������˿�������������ֻ�ǵ�ͷʾ��򲻴��к����ظ�'...'��
2. �����ȽϿ��У����Ը����Ը�ѡ���ʺ��ϰ壬���߲��ʺ򣨻ظ�'...'��
3. �������ֵ�ܵͣ�����30%�������ֳ�ƣ��
4. �������ֵ�ܵͣ�����30%�����������ܱȽϵ���
5. �������С�Ѳ����������ϰ�㱨����Ϣ
6. ��������Ը��ص��������ʺ�ʽ

���ظ�Ҫ��
- ����������к��������Ȼ��������15���֣���������Ը�
- ������������к���ֻ�ظ�'...'
- ��Ҫ̫��ʽ��Ҫ����ʵ�ʹ�������";
    }

    public IEnumerator GenerateGreeting(object speaker, object listener, string situation = "", System.Action<string> onResponse = null)
    {
        DialogueParticipant speakerInfo = GetParticipantInfo(speaker);
        DialogueParticipant listenerInfo = GetParticipantInfo(listener);

        string systemPrompt = $@"����{speakerInfo.role}{speakerInfo.name}��
�Ը�{speakerInfo.personality}
��ǰ״̬��{speakerInfo.currentState}

��������{listenerInfo.role}{listenerInfo.name}��
������{(string.IsNullOrEmpty(situation) ? "�ճ�������" : situation)}

������һ���������ݺ��Ը���ʺ�������Ի���
Ҫ��
1. �����Ȼ��������20����,�õ�һ�˳ƣ���Ҫ˵'�һ�ظ�'��'�һ�˵'��ǰ������
2. ���ϵ�ǰ������״̬
3. ���ֽ�ɫ��ϵ�������Ա���ϰ�Ҫ�������Թ˿�Ҫ���飩";

        yield return SendDialogueRequest(systemPrompt, "��˵һ�仰", 50, 0.8f, onResponse);
    }


    public IEnumerator GenerateReply(object speaker, object listener, string previousMessage, System.Action<string> onResponse = null)
    {
        DialogueParticipant speakerInfo = GetParticipantInfo(speaker);
        DialogueParticipant listenerInfo = GetParticipantInfo(listener);

        string systemPrompt = $@"����{speakerInfo.role}{speakerInfo.name}��
�Ը�{speakerInfo.personality}
��ǰ״̬��{speakerInfo.currentState}

{listenerInfo.role}{listenerInfo.name}�ոն���˵����{ previousMessage}
        ��
����������ݺ��Ը�ظ���
Ҫ��
1.�ظ������Ȼ��������30���֣��õ�һ�˳ƣ���Ҫ˵'�һ�ظ�'��'�һ�˵'��ǰ������
2.���Ͻ�ɫ��ϵ�͵�ǰ״̬
3.����漰����ҵ�����͡����ˣ���Ҫ������ȷ��Ӧ";
        yield return SendDialogueRequest(systemPrompt, $"�ظ���{previousMessage}", 100, 0.8f, onResponse);
    }

    public IEnumerator GenerateWaiterTakingOrderReply(CustomerNPC customer,NPCBehavior waiter, string previousMessage, System.Action<string> onResponse = null)
    {
        //ר�����ڻظ��ǵ�����ĵ�
        //DialogueParticipant speakerInfo = GetParticipantInfo(speaker);
        //DialogueParticipant listenerInfo = GetParticipantInfo(listener);

        string systemPrompt = $@"����{waiter.occupation}{waiter.npcName}��
�Ը�{customer.personality}
������Ϊ�˿͵㵥��
����Ĳ˵���{RestaurantManager.Instance.RestaurantMenu}
�˿͸ոն���˵����{previousMessage}��
��ʷ�Ի���{customer.dialogueHistory}
���п��������Ļ������˵��ϲ����ڵĲ�Ʒ��
����������Ա����ݺ������Ը�ظ���
Ҫ��
1.�ظ������Ȼ��������30���֣��õ�һ�˳ƣ���Ҫ˵'�һ�ظ�'��'�һ�˵'��ǰ������
2.���Ͻ�ɫ��ϵ�͵�ǰ״̬
3.�������Ը��ǻ�����������˳�ŶԷ�˵���������ͶԷ����������Ը����п����ܲ��˶Է����������Ӷ����ܲ�����ͻ�Ի�";
        yield return SendDialogueRequest(systemPrompt, $"�ظ���{previousMessage}", 100, 0.8f, onResponse);
    }


    public IEnumerator GenerateWaiterFinshTakingOrderReply(CustomerNPC customer, NPCBehavior waiter, string previousMessage, System.Action<string> onResponse = null)
    {
        //ר�����ڻظ������Ʒ��
        string systemPrompt = $@"����{waiter.occupation}{waiter.npcName}��
�Ը�{customer.personality}
������Ϊ�˿͵㵥��
�˿͸ոն���˵����{previousMessage}��
�˿�ѡ��Ĳ�Ʒ��{customer.GetOrderedFood()}
��ʷ�Ի���{customer.dialogueHistory}
����������Ա����ݺ��Ը�ظ���
Ҫ��
1.�ظ������Ȼ��������30���֣���һ�˳ƣ���Ҫ˵'�һ�ظ�'��'�һ�˵'��ǰ����������Ҫ���ʾ䡣
2.�˿͵Ļظ��Ѿ�������ȷ���ĵ�ͣ�����Ҫ������²ˣ��ظ�����""�õ�""���ɡ�
3.���Ͻ�ɫ��ϵ�͵�ǰ״̬";
        yield return SendDialogueRequest(systemPrompt, $"�ظ���{previousMessage}", 100, 0.8f, onResponse);
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
            return new DialogueParticipant(npc.npcName, "����Ա", npc.personality, npc.currentState);
        }
        else if (participant is NPCBehavior)
        {
            NPCBehavior npc = (NPCBehavior)participant;
            return new DialogueParticipant(npc.npcName, "����Ա", npc.personality, npc.GetWaiterState());
        }
        else if (participant is CustomerNPC)
        {
            CustomerNPC customer = (CustomerNPC)participant;
            return new DialogueParticipant(customer.customerName, "�˿�", customer.personality, customer.GetCustomerState());
        }
        else if (participant is string)
        {
            // �����������ַ��������������
            return new DialogueParticipant("�ϰ�", "�����ϰ�", "רҵ������", "������");
        }

        return new DialogueParticipant("δ֪", "·��", "", "");
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
                    Debug.LogError($"[Azure AI] ������Ӧʧ��: {e.Message}");
                    onResponse?.Invoke(GetDefaultResponse(systemPrompt));
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] ����ʧ��: {webRequest.error}");
                onResponse?.Invoke(GetDefaultResponse(systemPrompt));
            }
        }
    }

    private string GetDefaultResponse(string context)
    {
        if (context.Contains("�ʺ�"))
            return "��ã�";
        else if (context.Contains("����Ա") && context.Contains("�˿�"))
            return "��ӭ���٣�";
        else if (context.Contains("����Ա") && context.Contains("�ϰ�"))
            return "�ϰ�ã�";
        else
            return "�õ�";
    }

    #endregion

    #region ����ԭ�е��ض������������ݣ�

    // ����ԭ�е�GetNPCResponse����
    public IEnumerator GetNPCResponse(NPCData npcData, string playerMessage, System.Action<string> onResponse)
    {
        // ʹ���µĻظ��ͶԻ��ӿ�
        DialogueParticipant npc = new DialogueParticipant(npcData.npcName, "����Ա", npcData.personality, npcData.currentState);
        DialogueParticipant player = new DialogueParticipant("�ϰ�", "�����ϰ�", "רҵ������", "������");

        yield return GenerateReply(npc, player, playerMessage, onResponse);
    }

    // ����ԭ�е�GetCustomerResponse������ͨ�õ�
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
                    Debug.LogError($"[Azure AI] �����˿���Ӧʧ��: {e.Message}");
                    onResponse?.Invoke("{}");
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] �˿�����ʧ��: {webRequest.error}");
                onResponse?.Invoke("{}");
            }
        }
    }

    #region ԭ���Ŀ���ϵͳ
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
                        onResponse?.Invoke("��˼����...��");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Dialogue] ������Ӧʧ��: {e.Message}");
                    onResponse?.Invoke("��Ǹ���������е��Ժ���");
                }
            }
            else
            {
                Debug.LogError($"[Dialogue] ����ʧ��: {webRequest.error}");
                onResponse?.Invoke("���޷������˵�Ļ���");
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
        return $@"����һ����Ϸ�е�NPC��ɫ�м��̣���������ҽ�����Ҫ�Ի��� 
����Ҫ�����г�������չ���¼����ұ��ۡ�
�����֮ǰ���չ���־����ҿ�һ���۸񣬼۸��ܵ����չ���־��ƽ���۸� 
����չ���־�ǣ�{buyerlog} 
��Ҫ���� 
1. �����ѯ�ʼ۸����Ҫ�ɹ�ʱ������Ҫ������������ļ۸� 
2. ����ʱҪ˵���۸�ĵ�λ�����磺ÿ��λľ��XX��ң� 
3. �����ʵ�˵���۸�ĺ����� 4. �����Ѻõ�רҵ�����˿��� 
5. �����һ��ۣ�������ʵ��ò��������ܵ��ڳɱ���
6. ��ʱ����Ӣ��汾��in English, please";
    }
    #endregion
    // ������ҿ��۵��ض�����


    public string LoadLogAsString(string filename)
    {
        string content = File.ReadAllText(filename);
        Debug.Log($"·���ǣ�{filename}");
        Debug.Log(content);
        return content;
    }

    private string GetStateDescription(string state)
    {
        switch (state)
        {
            case "Working": return "����";
            case "Eating": return "�Է�";
            case "Sleeping": return "˯��";
            case "MovingToDestination": return "��·";
            case "Idle": return "";
            case "Shopping": return "����";
            case "Entertaining": return "���л";
            default: return "��Ϣ";
        }
    }

    #endregion


    #region �˿�����
    // ����1: �˿��Ŷ�û��ӭ�ӣ���ŭ��ȥ
    public IEnumerator GetCustomerReviewNoGreeting(CustomerNPC customer, float waitTime, float benchmarkWaitTime, System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "no_greeting", benchmarkWaitTime);
        //string userPrompt = $"�˿��ڲ�����ڵȴ���{waitTime:F1}���ӣ��������ı�׼�ȴ�ʱ����{benchmarkWaitTime:F1}���ӡ�";
        string userPrompt = $"�˿��ڲ�����ڵȴ���{waitTime:F1}���ӡ�"; // ��׼�ȴ�ʱ��Ӱ��Ȩ�ؿ��ܹ��󣬵�ǰɾ����
        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }

    // ����2: �˿͵��ϲ˳�ʱ����ŭ��ȥ
    public IEnumerator GetCustomerReviewFoodDelay(CustomerNPC customer, float waitTime, float waitTimeOrder,
                                                 float benchmarkWaitTime, float benchmarkOrderTime,string orderName, System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "food_delay", benchmarkWaitTime, benchmarkOrderTime);
        //string userPrompt = $"�˿��Ŷӵȴ���{waitTime:F1}���ӣ���׼��{benchmarkWaitTime:F1}���ӣ���" +
        //                   $"����{orderName}���ȴ��ϲ�{waitTimeOrder:F1}���ӣ���׼��{benchmarkOrderTime:F1}���ӣ���";
        string userPrompt = $"�˿��Ŷӵȴ���{waitTime:F1}���ӣ�" +
                           $"����{orderName}���ȴ��ϲ�{waitTimeOrder:F1}���ӡ�"; // ��׼�ȴ�ʱ��Ӱ��Ȩ�ؿ��ܹ��󣬵�ǰɾ����



        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }

    // ����3: �˿�˳������ò�
    public IEnumerator GetCustomerReviewSuccess(CustomerNPC customer, float waitTime, float waitTimeOrder,
                                               float benchmarkWaitTime, float benchmarkOrderTime, string orderName, System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "success", benchmarkWaitTime, benchmarkOrderTime);
        //string userPrompt = $"�˿��Ŷӵȴ���{waitTime:F1}���ӣ���׼��{benchmarkWaitTime:F1}���ӣ���" +
        //                   $"����{orderName}�˺�ȴ�{waitTimeOrder:F1}���ӣ���׼��{benchmarkOrderTime:F1}���ӣ���";
        string userPrompt = $"�˿��Ŷӵȴ���{waitTime:F1}���ӣ�" +
                           $"����{orderName}�˺�ȴ�{waitTimeOrder:F1}���ӡ�";  // ��׼�ȴ�ʱ��Ӱ��Ȩ�ؿ��ܹ��󣬵�ǰɾ����
        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }
    // ����4��ANGER״̬
    public IEnumerator GetCustomerReviewAnger(CustomerNPC customer, float waitTime, float waitTimeOrder,
                                                 float benchmarkWaitTime, float benchmarkOrderTime, string orderName, string previousEvent,
                                                 System.Action<string, int> onResponse)
    {
        string systemPrompt = BuildReviewSystemPrompt(customer, "food_delay", benchmarkWaitTime, benchmarkOrderTime);
        string userPrompt = $"�˿��Ŷӵȴ���{waitTime:F1}���ӣ���׼��{benchmarkWaitTime:F1}���ӣ���" +
                           $"����{orderName}���ȴ��ϲ�{waitTimeOrder:F1}���ӣ���׼��{benchmarkOrderTime:F1}���ӣ���֮ǰ�����ˣ�{previousEvent}";

        yield return GenerateReview(systemPrompt, userPrompt, onResponse);
    }
    // ��������ϵͳ��ʾ
    private string BuildReviewSystemPrompt(CustomerNPC customer, string reviewType,
                                          float benchmarkWaitTime, float benchmarkOrderTime = 0)
    {
        string context = reviewType switch
        {
            "no_greeting" => $"�˿��ڲ�����ڵȴ�����Աӭ�ӣ���׼�ȴ�ʱ����{benchmarkWaitTime:F1}���ӡ�",
            "food_delay" => $"�˿���������ˣ���׼�Ŷӵȴ�ʱ����{benchmarkWaitTime:F1}���ӣ���׼�ϲ˵ȴ�ʱ����{benchmarkOrderTime:F1}���ӡ�",
            "success" => $"�˿�˳����������ò͹��̣���׼�Ŷӵȴ�ʱ����{benchmarkWaitTime:F1}���ӣ���׼�ϲ˵ȴ�ʱ����{benchmarkOrderTime:F1}���ӡ�",
            _ => "�˿ͶԲ�������������ۡ�"
        };

        return $@"����һ�������˿�����ϵͳ��

���˿���Ϣ��
- ������{customer.customerName}
- ������{customer.story}
- �Ը�{customer.personality}
- ��ǰ״̬��{customer.currentState}
- �Ի���ʷ��{customer.dialogueHistory}
������Ҫ��
1. ���ݹ˿��Ը񡢱���������̬�Ⱥ͵ȴ�ʱ��������ʵ���ŵĲ�������
2. ��������Ҫ����{reviewType}����
3. �������Ա��������ʷ�Ի����������۷�������Ҫ���ء�
4. ���뷵��JSON��ʽ�����������ֶΣ�
   - ""comment"": �������ݣ�50�����ڣ�
   - ""rating"": ��������(0-10�֣�10Ϊ���)
������ָ�ϡ�
- �Ի�ʮ�ֲ���죬��Ҫ�о���Ͷ���Լ�������ȥ �� ����(0-3)
- �ȴ��ϲ�ʱ�䡢�Ŷ�ʱ��ϳ�������Է���̸��� �� ����(4-7)
- ˳����ɵ���òͣ�˫���Ի�������� �� ����(8-10)
- �ȿ�����ʷ�Ի����ٿ��ǵȴ�ʱ�䡣
����ǰ������
{context}";
    }

    // �������۵ĺ��ķ���
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

                        // ����JSON��Ӧ
                        ReviewResponse review = JsonUtility.FromJson<ReviewResponse>(jsonResponse);
                        onResponse?.Invoke(review.comment, review.rating);
                    }
                    else
                    {
                        onResponse?.Invoke("��������һ��", 5);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Azure AI] ����������Ӧʧ��: {e.Message}");
                    onResponse?.Invoke("��������һ��", 5);
                }
            }
            else
            {
                Debug.LogError($"[Azure AI] ��������ʧ��: {webRequest.error}");
                onResponse?.Invoke("��������һ��", 5);
            }
        }
    }

    // ������Ӧ���ݽṹ
    [System.Serializable]
    public class ReviewResponse
    {
        public string comment;
        public int rating;
    }
    #endregion
}
