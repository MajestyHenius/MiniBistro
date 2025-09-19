// MainMenuController.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("UI Elements")]
    public Button exitButton;
    public Button startEasyButton;
    public Button startNormalButton;
    public Button startHardButton;

    [Header("Game Settings")]
    public GameSettings gameSettings; // ��������GlobalGameSettings��Դ��ק������

    // �Ѷ�����
    private DifficultyConfig[] difficultyConfigs = new DifficultyConfig[]
    {
        new DifficultyConfig {
            name = "��",
            isNegativeCustomer = false,
            isNegativeWaiter = false,
            customerSpawnInterval = 60 ,
            waiterNumber =3
        },
        new DifficultyConfig {
            name = "��ͨ",
            isNegativeCustomer = false,
            isNegativeWaiter = false,
            customerSpawnInterval = 30,
            waiterNumber =6
        },
        new DifficultyConfig {
            name = "����",
            isNegativeCustomer = true,
            isNegativeWaiter = true,
            customerSpawnInterval = 20,
            waiterNumber =4

        }
    };

    // ������洢�Ѷ�����
    private class DifficultyConfig
    {
        public string name;
        public bool isNegativeCustomer;
        public bool isNegativeWaiter;
        public int customerSpawnInterval;
        public int waiterNumber;
    }

    void Start()
    {
        // ȷ�����б�Ҫ��������ѷ���
        if (exitButton == null ||
            startEasyButton == null || startNormalButton == null || startHardButton == null)
        {
            Debug.LogError("MainMenuController: ��Щ���δ����!");
            return;
        }

        // ���ð�ť����
        exitButton.onClick.AddListener(OnExitButtonClicked);
        startEasyButton.onClick.AddListener(() => StartGameWithDifficulty(0));
        startNormalButton.onClick.AddListener(() => StartGameWithDifficulty(1));
        startHardButton.onClick.AddListener(() => StartGameWithDifficulty(2));
    }

    void StartGameWithDifficulty(int difficultyIndex)
    {
        // ��ȡѡ�е��Ѷ�����
        DifficultyConfig config = difficultyConfigs[difficultyIndex];

        // Ӧ�����õ���Ϸ����
        gameSettings.isNegativeCustomer = config.isNegativeCustomer;
        gameSettings.isNegativeWaiter = config.isNegativeWaiter;
        gameSettings.customerSpawnInterval = config.customerSpawnInterval;

        // ��ӡ��־
        Debug.Log($"��ʼ{config.name}�Ѷ���Ϸ!");
        Debug.Log($"����: �ͻ�����={config.isNegativeCustomer}, " +
                 $"����Ա����={config.isNegativeWaiter}, " +
                 $"���ɼ��={config.customerSpawnInterval}");

        // ������Ϸ����
        SceneManager.LoadScene("Expanded2");
    }

    void OnExitButtonClicked()
    {
        Debug.Log("�˳���Ϸ��ť�����!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // �Ƴ����м���
        exitButton.onClick.RemoveListener(OnExitButtonClicked);
        startEasyButton.onClick.RemoveAllListeners();
        startNormalButton.onClick.RemoveAllListeners();
        startHardButton.onClick.RemoveAllListeners();
    }
}