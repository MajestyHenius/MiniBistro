using System;
using UnityEngine;
// ”ÎManager∑÷¿Î
[System.Serializable]
public class AzureOpenAIConfig
{
    public string endpoint = "";
    public string deployment = "";
    public string apiVersion = "";
    public string subscriptionKey = "";

    public string GetApiUrl()
    {
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deployment) || string.IsNullOrEmpty(apiVersion))
        {
            return "";
        }
        return $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
    }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(endpoint) &&
               !string.IsNullOrEmpty(deployment) &&
               !string.IsNullOrEmpty(apiVersion) &&
               !string.IsNullOrEmpty(subscriptionKey);
    }
}