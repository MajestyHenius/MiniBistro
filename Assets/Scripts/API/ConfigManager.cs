using System;
using System.IO;
using UnityEngine;

public class ConfigManager : MonoBehaviour
{
    private static ConfigManager instance;
    public static ConfigManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("ConfigManager");
                instance = go.AddComponent<ConfigManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private AzureOpenAIConfig config;
    public AzureOpenAIConfig Config => config;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void LoadConfig()
    {
        string configPath;

#if UNITY_EDITOR
        // Editor模式：从项目根目录的Config文件夹读取
        configPath = Path.Combine(Application.dataPath, "..", "Config", "AzureOpenAIConfig.json");
#else
    // 构建版本：从exe同级的Config文件夹读取
    configPath = Path.Combine(Application.dataPath, "..", "Config", "AzureOpenAIConfig.json");
#endif

        Debug.Log($"[ConfigManager] 尝试加载配置文件: {configPath}");

        if (File.Exists(configPath))
        {
            try
            {
                string jsonContent = File.ReadAllText(configPath);
                config = JsonUtility.FromJson<AzureOpenAIConfig>(jsonContent);

                if (config.IsValid())
                {
                    Debug.Log("[ConfigManager] 配置文件加载成功");
                }
                else
                {
                    Debug.LogError("[ConfigManager] 配置文件内容不完整，请检查所有必需字段");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigManager] 配置文件解析失败: {e.Message}");
                CreateDefaultConfig(configPath);
            }
        }
        else
        {
            Debug.LogWarning("[ConfigManager] 配置文件不存在，创建默认配置");
            CreateDefaultConfig(configPath);
        }
    }

    private void CreateDefaultConfig(string configPath)
    {
        try
        {
            string configDir = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            config = new AzureOpenAIConfig();
            string jsonContent = JsonUtility.ToJson(config, true);
            File.WriteAllText(configPath, jsonContent);

            Debug.Log($"[ConfigManager] 已创建默认配置文件: {configPath}");
            Debug.Log("[ConfigManager] 请填写配置文件中的所有必需信息");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConfigManager] 创建默认配置文件失败: {e.Message}");
            config = new AzureOpenAIConfig();
        }
    }

    public bool IsConfigValid()
    {
        return config != null && config.IsValid();
    }
}