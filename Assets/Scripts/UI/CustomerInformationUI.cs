using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CustomerInformationUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject infoPanel;
    public Slider satisfactionBar;
    public TMP_Text satisfactionText;
    public TMP_Text satisfactionText_eng;
    public TMP_Text backgroundStoryText;
    public TMP_Text backgroundStoryText_eng;
    public TMP_Text nameText;
    public TMP_Text nameText_eng;
    public TMP_Text personalityText;
    public TMP_Text personalityText_eng;
    public TMP_Text statusText;
    public TMP_Text statusText_eng;
    [Header("UI Settings")]
    public Vector3 offset = new Vector3(0, 2.5f, 0);
    public bool alwaysShow = false;
    public bool IsUIVisible => isUIVisible;

    [Header("Animation")]
    public float animationDuration = 0.5f;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public CustomerNPC customer;
    private Camera mainCamera;
    private CanvasGroup canvasGroup;
    private Coroutine satisfactionAnimCoroutine;
    private bool isUIVisible = false;

    void Start()
    {
        // 尝试获取Customer引用
        if (customer == null)
        {
            customer = GetComponentInParent<CustomerNPC>();
        }

        mainCamera = Camera.main;

        // 获取CanvasGroup组件
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (customer == null)
        {
            Debug.LogError("CustomerNPC not found in parent!");
            return;
        }

        // 确保infoPanel存在
        if (infoPanel == null)
        {
            infoPanel = transform.Find("InfoPanel")?.gameObject;
            if (infoPanel == null)
            {
                Debug.LogError("infoPanel not found!");
                return;
            }
        }

        // 尝试自动获取UI组件引用
        if (satisfactionBar == null) satisfactionBar = infoPanel.transform.Find("SatisfactionBar")?.GetComponent<Slider>();
        if (satisfactionText == null) satisfactionText = infoPanel.transform.Find("SatisfactionText")?.GetComponent<TMP_Text>();
        if (backgroundStoryText == null) backgroundStoryText = infoPanel.transform.Find("BackgroundStoryText")?.GetComponent<TMP_Text>();
        if (backgroundStoryText_eng == null) backgroundStoryText = infoPanel.transform.Find("BackgroundStoryText_eng")?.GetComponent<TMP_Text>();
        if (nameText == null) nameText = infoPanel.transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (personalityText == null) personalityText = infoPanel.transform.Find("PersonalityText")?.GetComponent<TMP_Text>();
        if (statusText == null) statusText = infoPanel.transform.Find("StatusText")?.GetComponent<TMP_Text>();

        // 初始化UI
        UpdateUIImmediate();

        if (!alwaysShow)
        {
            HideUI();
        }
    }

    void Update()
    {
        if (mainCamera != null)
        {
            // 保持UI面向相机
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                           mainCamera.transform.rotation * Vector3.up);
        }

        if (customer != null)
        {
            transform.position = customer.transform.position + offset;
        }
    }

    // 显示UI
    public void ShowUI()
    {
        if (infoPanel != null)
        {
            isUIVisible = true;
            infoPanel.SetActive(true);
            UpdateUIImmediate();
        }
    }

    // 隐藏UI
    public void HideUI()
    {
        if (infoPanel != null)
        {
            isUIVisible = false;
            infoPanel.SetActive(false);
        }
    }

    // 切换UI显示状态
    public void ToggleUI()
    {
        if (isUIVisible)
            HideUI();
        else
            ShowUI();
    }

    // 立即更新所有UI元素
    public void UpdateUIImmediate()
    {
        if (customer == null) return;

        satisfactionBar.value = customer.satisfaction / 100f;
        UpdateTexts();
    }

    // 更新满意度（带动画）
    public void UpdateSatisfaction(float oldValue, float newValue)
    {
        if (canvasGroup == null || !isUIVisible) return;

        if (satisfactionAnimCoroutine != null) StopCoroutine(satisfactionAnimCoroutine);
        satisfactionAnimCoroutine = StartCoroutine(AnimateSatisfactionBar(oldValue, newValue));
    }

    private IEnumerator AnimateSatisfactionBar(float oldValue, float newValue)
    {
        if (!isUIVisible) yield break;

        float startValue = oldValue / 100f;
        float endValue = newValue / 100f;
        float elapsedTime = 0;

        ShowSatisfactionChangeIndicator(newValue - oldValue);

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            float curveValue = animationCurve.Evaluate(t);

            satisfactionBar.value = Mathf.Lerp(startValue, endValue, curveValue);

            float currentValue = Mathf.Lerp(oldValue, newValue, curveValue);
            satisfactionText.text = $"Satisfication: {currentValue:F0}/100";

            yield return null;
        }

        satisfactionBar.value = endValue;
        satisfactionText.text = $"Satisfication: {newValue:F0}/100";
    }

    private void ShowSatisfactionChangeIndicator(float change)
    {
        if (change == 0 || !isUIVisible) return;

        GameObject indicator = new GameObject("SatisfactionChangeIndicator");
        indicator.transform.SetParent(satisfactionBar.transform);
        indicator.transform.localPosition = Vector3.zero;

        TMP_Text indicatorText = indicator.AddComponent<TextMeshProUGUI>();
        indicatorText.text = change > 0 ? $"+{change:F0}" : change.ToString("F0");
        indicatorText.color = change > 0 ? Color.green : Color.red;
        indicatorText.fontSize = 2;
        indicatorText.alignment = TextAlignmentOptions.Center;

        StartCoroutine(AnimateIndicator(indicator));
    }

    private IEnumerator AnimateIndicator(GameObject indicator)
    {
        if (!isUIVisible)
        {
            Destroy(indicator);
            yield break;
        }

        float duration = 1.5f;
        float elapsedTime = 0;
        Vector3 startPos = indicator.transform.localPosition;

        TMP_Text text = indicator.GetComponent<TMP_Text>();
        Color startColor = text.color;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            indicator.transform.localPosition = startPos + Vector3.up * 30 * t;
            text.color = new Color(startColor.r, startColor.g, startColor.b, 1 - t);

            yield return null;
        }

        Destroy(indicator);
    }

    // 更新所有文本
    public void UpdateTexts()
    {
        if (canvasGroup == null || !isUIVisible) return;

        satisfactionText.text = $"满意度: {customer.satisfaction:F0}/100";
        nameText.text = customer.customerName;
        personalityText.text = customer.personality;
        backgroundStoryText.text = customer.story;
        //英文显示
        satisfactionText_eng.text = $"Satisfication: {customer.satisfaction:F0}/100";
        nameText_eng.text = customer.customerName_eng;
        personalityText_eng.text = customer.personality_eng;
        backgroundStoryText_eng.text = customer.story_eng;
        statusText.text = customer.GetCustomerState();
    }

    // 更新状态文本
    public void UpdateStatusText()
    {
        if (statusText != null && isUIVisible)
        {
            statusText.text = customer.GetCustomerState();
            statusText_eng.text = customer.GetCustomerState_eng();
        }
    }

    // 初始化UI
    public void InitializeUI()
    {
        if (customer == null) return;

        satisfactionText.text = $"满意度: {customer.satisfaction:F0}/100";
        nameText.text = customer.customerName;
        personalityText.text = customer.personality;
        backgroundStoryText.text = customer.story;
        //英文显示
        satisfactionText_eng.text = $"Satisfication: {customer.satisfaction:F0}/100";
        nameText_eng.text = customer.customerName_eng;
        personalityText_eng.text = customer.personality_eng;
        backgroundStoryText_eng.text = customer.story_eng;
        statusText_eng.text = customer.GetCustomerState_eng();

        satisfactionBar.value = customer.satisfaction / 100f;
    }
}