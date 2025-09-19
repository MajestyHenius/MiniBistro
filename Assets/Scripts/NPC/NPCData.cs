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


    // 带参数的构造函数 - 添加异常处理
    public NPCData(NPCBehavior npc)
    {
        if (npc == null)
        {
            Debug.LogError("NPCData: 传入的NPC为null");
            SetDefaultValues();
            return;
        }

        // 使用null合并运算符处理可能的空值
        npcName = npc.npcName ?? "未知服务员";
        personality = npc.personality ?? "专业";
        occupation = npc.occupation ?? "服务员";

        // 安全获取状态
        try
        {
            currentState = npc.GetWaiterState()?.ToString() ?? "待命";
        }
        catch (Exception e)
        {
            Debug.LogError($"获取服务员状态时出错: {e.Message}");
            currentState = "工作中";
        }

        // 数值类型直接赋值
        health = npc.health;
        maxHealth = npc.maxHealth;
        mood = npc.mood;
        maxMood = npc.maxMood;
        energy = npc.energy;
        maxEnergy = npc.maxEnergy;
        money = npc.tips;
        wood = npc.wood;

        // 安全获取位置信息
        try
        {
            currentLocation = npc.GetCurrentLocationName();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"获取NPC位置时出错: {e.Message}，使用默认位置");
            currentLocation = "餐厅";
        }
    }

    // 无参数构造函数 - 设置默认值
    public NPCData()
    {
        SetDefaultValues();
    }

    // 设置默认值的方法
    private void SetDefaultValues()
    {
        npcName = "成熟的服务员";
        personality = "专业";
        occupation = "服务员";
        currentState = "休息";
        health = 100;
        maxHealth = 100;
        mood = 50;
        maxMood = 100;
        energy = 50;
        maxEnergy = 100;
        money = 0;
        wood = 0;
        currentLocation = "餐厅";
    }

    // 添加一个静态工厂方法，安全创建NPCData
    public static NPCData CreateSafe(NPCBehavior npc)
    {
        try
        {
            return new NPCData(npc);
        }
        catch (Exception e)
        {
            Debug.LogError($"创建NPCData时发生异常: {e.Message}");
            var data = new NPCData();

            // 尝试获取基本信息
            if (npc != null)
            {
                data.npcName = npc.npcName ?? "新来的";
                data.personality = npc.personality ?? "生疏";
                data.occupation = npc.occupation ?? "服务员";
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
