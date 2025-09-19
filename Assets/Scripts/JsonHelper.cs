using System;
using UnityEngine;
public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        // ȷ��json����Ч������
        json = json.Trim();
        if (!json.StartsWith("[") || !json.EndsWith("]"))
        {
            Debug.LogError("[JsonHelper] JSON����һ����Ч������");
            return null;
        }
        // ������װ����
        string newJson = "{\"array\":" + json + "}";
        try
        {
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
            return wrapper?.array;
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[JsonHelper] JSON����ʧ��: {e.Message}");
            return null;
        }
    }
    [Serializable]
    private class Wrapper<T>
    {
        public T[] array = null;
    }
}