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
    public GameSettings gameSettings; // 将创建的GlobalGameSettings资源拖拽到这里

    // 难度配置
    private DifficultyConfig[] difficultyConfigs = new DifficultyConfig[]
    {
        new DifficultyConfig {
            name = "简单",
            isNegativeCustomer = false,
            isNegativeWaiter = false,
            customerSpawnInterval = 60 ,
            waiterNumber =3
        },
        new DifficultyConfig {
            name = "普通",
            isNegativeCustomer = false,
            isNegativeWaiter = false,
            customerSpawnInterval = 30,
            waiterNumber =6
        },
        new DifficultyConfig {
            name = "困难",
            isNegativeCustomer = true,
            isNegativeWaiter = true,
            customerSpawnInterval = 20,
            waiterNumber =4

        }
    };

    // 辅助类存储难度配置
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
        // 确保所有必要的组件都已分配
        if (exitButton == null ||
            startEasyButton == null || startNormalButton == null || startHardButton == null)
        {
            Debug.LogError("MainMenuController: 有些组件未分配!");
            return;
        }

        // 设置按钮监听
        exitButton.onClick.AddListener(OnExitButtonClicked);
        startEasyButton.onClick.AddListener(() => StartGameWithDifficulty(0));
        startNormalButton.onClick.AddListener(() => StartGameWithDifficulty(1));
        startHardButton.onClick.AddListener(() => StartGameWithDifficulty(2));
    }

    void StartGameWithDifficulty(int difficultyIndex)
    {
        // 获取选中的难度配置
        DifficultyConfig config = difficultyConfigs[difficultyIndex];

        // 应用配置到游戏设置
        gameSettings.isNegativeCustomer = config.isNegativeCustomer;
        gameSettings.isNegativeWaiter = config.isNegativeWaiter;
        gameSettings.customerSpawnInterval = config.customerSpawnInterval;

        // 打印日志
        Debug.Log($"开始{config.name}难度游戏!");
        Debug.Log($"设置: 客户负面={config.isNegativeCustomer}, " +
                 $"服务员负面={config.isNegativeWaiter}, " +
                 $"生成间隔={config.customerSpawnInterval}");

        // 加载游戏场景
        SceneManager.LoadScene("Expanded2");
    }

    void OnExitButtonClicked()
    {
        Debug.Log("退出游戏按钮被点击!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // 移除所有监听
        exitButton.onClick.RemoveListener(OnExitButtonClicked);
        startEasyButton.onClick.RemoveAllListeners();
        startNormalButton.onClick.RemoveAllListeners();
        startHardButton.onClick.RemoveAllListeners();
    }
}