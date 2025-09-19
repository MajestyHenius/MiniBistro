// NPCData.cs
using System;
using UnityEngine;
[System.Serializable]
public class NPCData
{

    public string npcName;
    public string personality;
    public string occupation;
    public string currentState;
    public int health;
    public int maxHealth;
    public int mood;
    public int maxMood;
    public int energy;
    public int maxEnergy;
    public int money;
    int wood;
    public string currentLocation;


    // �������Ĺ��캯�� - ����쳣����
    public NPCData(NPCBehavior npc)
    {
        if (npc == null)
        {
            Debug.LogError("NPCData: �����NPCΪnull");
            SetDefaultValues();
            return;
        }

        // ʹ��null�ϲ������������ܵĿ�ֵ
        npcName = npc.npcName ?? "δ֪����Ա";
        personality = npc.personality ?? "רҵ";
        occupation = npc.occupation ?? "����Ա";

        // ��ȫ��ȡ״̬
        try
        {
            currentState = npc.GetWaiterState()?.ToString() ?? "����";
        }
        catch (Exception e)
        {
            Debug.LogError($"��ȡ����Ա״̬ʱ����: {e.Message}");
            currentState = "������";
        }

        // ��ֵ����ֱ�Ӹ�ֵ
        health = npc.health;
        maxHealth = npc.maxHealth;
        mood = npc.mood;
        maxMood = npc.maxMood;
        energy = npc.energy;
        maxEnergy = npc.maxEnergy;
        money = npc.tips;
        wood = npc.wood;

        // ��ȫ��ȡλ����Ϣ
        try
        {
            currentLocation = npc.GetCurrentLocationName();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"��ȡNPCλ��ʱ����: {e.Message}��ʹ��Ĭ��λ��");
            currentLocation = "����";
        }
    }

    // �޲������캯�� - ����Ĭ��ֵ
    public NPCData()
    {
        SetDefaultValues();
    }

    // ����Ĭ��ֵ�ķ���
    private void SetDefaultValues()
    {
        npcName = "����ķ���Ա";
        personality = "רҵ";
        occupation = "����Ա";
        currentState = "��Ϣ";
        health = 100;
        maxHealth = 100;
        mood = 50;
        maxMood = 100;
        energy = 50;
        maxEnergy = 100;
        money = 0;
        wood = 0;
        currentLocation = "����";
    }

    // ���һ����̬������������ȫ����NPCData
    public static NPCData CreateSafe(NPCBehavior npc)
    {
        try
        {
            return new NPCData(npc);
        }
        catch (Exception e)
        {
            Debug.LogError($"����NPCDataʱ�����쳣: {e.Message}");
            var data = new NPCData();

            // ���Ի�ȡ������Ϣ
            if (npc != null)
            {
                data.npcName = npc.npcName ?? "������";
                data.personality = npc.personality ?? "����";
                data.occupation = npc.occupation ?? "����Ա";
                data.health = npc.health;
                data.maxHealth = npc.maxHealth;
                data.mood = npc.mood;
                data.maxMood = npc.maxMood;
                data.energy = npc.energy;
                data.maxEnergy = npc.maxEnergy;
                data.money = npc.tips;
                data.wood = npc.wood;
            }

            return data;
        }
    }
}
