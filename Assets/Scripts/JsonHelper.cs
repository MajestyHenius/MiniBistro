using System;
using UnityEngine;
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        // 确保json是有效的数组
        json = json.Trim();
        if (!json.StartsWith("[") || !json.EndsWith("]"))
        {
            Debug.LogError("[JsonHelper] JSON不是一个有效的数组");
            return null;
        }
        // 创建包装对象
        string newJson = "{\"array\":" + json + "}";
        try
        {
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper?.array;
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[JsonHelper] JSON解析失败: {e.Message}");
            return null;
        }
    }
    [Serializable]
    private class Wrapper<T>
    {
        public T[] array = null;
    }
}