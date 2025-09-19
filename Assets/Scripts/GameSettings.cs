// GameSettings.cs
using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Settings/Game Settings")]
public class GameSettings : ScriptableObject
{
    public bool isNegativeCustomer = false; // 客户负面模式
    public bool isNegativeWaiter = false;   // 服务员负面模式
    public int customerSpawnInterval = 40;
    // 你可以在这里添加更多游戏设置
    // public float musicVolume = 1.0f;
    // public int selectedLevel = 1;
}