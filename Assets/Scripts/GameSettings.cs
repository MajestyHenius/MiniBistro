// GameSettings.cs
using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "Settings/Game Settings")]
public class GameSettings : ScriptableObject
{
    public bool isNegativeCustomer = false; // �ͻ�����ģʽ
    public bool isNegativeWaiter = false;   // ����Ա����ģʽ
    public int customerSpawnInterval = 40;
    // �������������Ӹ�����Ϸ����
    // public float musicVolume = 1.0f;
    // public int selectedLevel = 1;
}