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
        // Editorģʽ������Ŀ��Ŀ¼��Config�ļ��ж�ȡ
        configPath = Path.Combine(Application.dataPath, "..", "Config", "AzureOpenAIConfig.json");
#else
    // �����汾����exeͬ����Config�ļ��ж�ȡ
    configPath = Path.Combine(Application.dataPath, "..", "Config", "AzureOpenAIConfig.json");
#endif

        Debug.Log($"[ConfigManager] ���Լ��������ļ�: {configPath}");

        if (File.Exists(configPath))
        {
            try
            {
                string jsonContent = File.ReadAllText(configPath);
                config = JsonUtility.FromJson<AzureOpenAIConfig>(jsonContent);

                if (config.IsValid())
                {
                    Debug.Log("[ConfigManager] �����ļ����سɹ�");
                }
                else
                {
                    Debug.LogError("[ConfigManager] �����ļ����ݲ��������������б����ֶ�");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigManager] �����ļ�����ʧ��: {e.Message}");
                CreateDefaultConfig(configPath);
            }
        }
        else
        {
            Debug.LogWarning("[ConfigManager] �����ļ������ڣ�����Ĭ������");
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

            Debug.Log($"[ConfigManager] �Ѵ���Ĭ�������ļ�: {configPath}");
            Debug.Log("[ConfigManager] ����д�����ļ��е����б�����Ϣ");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConfigManager] ����Ĭ�������ļ�ʧ��: {e.Message}");
            config = new AzureOpenAIConfig();
        }
    }

    public bool IsConfigValid()
    {
        return config != null && config.IsValid();
    }
}