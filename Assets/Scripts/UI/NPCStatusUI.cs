using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCStatusUI : MonoBehaviour
{
    [Header("UI�������")]
    public GameObject statusPanel;
    public Slider healthBar;
    public Slider energyBar;
    public Slider moodBar;
    public TMP_Text healthText;
    public TMP_Text energyText;
    public TMP_Text moodText;
    //public TMP_Text energyText_eng;
    //public TMP_Text moodText_eng;
    public TMP_Text npcNameText;
    public TMP_Text npcNameText_eng;
    public TMP_Text npcBackgroundText;
    public TMP_Text npcBackgroundText_eng;
    public TMP_Text npcPersonalityText;
    public TMP_Text npcPersonalityText_eng;
    public Text woodText;
    public TMP_Text goldText;
    public Text meatText;
    public Text vegeText;
    public GameObject goldIcon; 

    [Header("UI����")]
    public Vector3 offset = new Vector3(0, 2.5f, 0);
    public bool alwaysShow = true;
    public float fadeDistance = 10f;
    public bool IsUIVisible => isUIVisible;
    [Header("����")]
    public float animationDuration = 0.5f;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public NPCBehavior npc;
    private Camera mainCamera;
    private CanvasGroup canvasGroup;
    private Coroutine healthAnimCoroutine;
    private Coroutine energyAnimCoroutine;
    private Coroutine moodAnimCoroutine;
    private bool isUIVisible = false;

    void Start()
    {
        // ���Ի�ȡNPC����
        if (npc == null)
        {
            npc = GetComponentInParent<NPCBehavior>();
        }

        mainCamera = Camera.main;

        // ��ȡCanvasGroup���
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (npc == null)
        {
            Debug.LogError("NPCBehavior not found in parent!");
            return;
        }

        // ȷ��statusPanel����
        if (statusPanel == null)
        {
            // ���Բ���statusPanel
            statusPanel = transform.Find("Panel")?.gameObject;
            if (statusPanel == null)
            {
                Debug.LogError("statusPanelδ�ҵ�!");
                return;
            }
        }
        //if (!statusPanel.activeSelf)
        //{
        //    statusPanel.SetActive(true);
        //}
        // �����Զ���ȡUI�������
        if (healthBar == null) healthBar = statusPanel.transform.Find("HealthBar")?.GetComponent<Slider>();
        if (energyBar == null) energyBar = statusPanel.transform.Find("EnergyBar")?.GetComponent<Slider>();
        if (moodBar == null) moodBar = statusPanel.transform.Find("MoodBar")?.GetComponent<Slider>();
        if (healthText == null) healthText = statusPanel.transform.Find("HealthText")?.GetComponent<TMP_Text>();
        if (energyText == null) energyText = statusPanel.transform.Find("EnergyText")?.GetComponent<TMP_Text>();
        if (moodText == null) moodText = statusPanel.transform.Find("MoodText")?.GetComponent<TMP_Text>();
        if (npcNameText == null) npcNameText = statusPanel.transform.Find("NameText")?.GetComponent<TMP_Text>();

        // ��ʼ��UI
        //HideUI();
        UpdateBarsImmediate();
    }

    void Update()
    {
        if (mainCamera != null)
        {
            // ����UI�������
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                           mainCamera.transform.rotation * Vector3.up);
        }

        if (npc != null)
        {
            transform.position = npc.transform.position + offset;
        }
    }

    // ��ʾUI
    public void ShowUI()
    {
        if (statusPanel != null)
        {
            isUIVisible = true;
            statusPanel.SetActive(true);

            // ��ʾ gold icon
            if (goldIcon != null)
            {
                goldIcon.SetActive(true);
            }

            UpdateBarsImmediate();
        }
    }

    // ����UI
    public void HideUI()
    {
        if (statusPanel != null)
        {
            isUIVisible = false;
            statusPanel.SetActive(false);

            // ���� gold icon
            if (goldIcon != null)
            {
                goldIcon.SetActive(false);
            }
        }
    }

    // �л�UI��ʾ״̬
    public void ToggleUI()
    {
        if (isUIVisible)
            HideUI();
        else
            ShowUI();
    }

    public void UpdateBarsImmediate()
    {
        if (npc == null) return;

        healthBar.value = (float)npc.health / npc.maxHealth;
        energyBar.value = (float)npc.energy / npc.maxEnergy;
        moodBar.value = (float)npc.mood / npc.maxMood;

        UpdateTexts();
    }

    public void UpdateHealth(int oldValue, int newValue)
    {
        if (canvasGroup == null || !isUIVisible) return;
        if (healthAnimCoroutine != null) StopCoroutine(healthAnimCoroutine);
        healthAnimCoroutine = StartCoroutine(AnimateBar(healthBar, oldValue, newValue, npc.maxHealth, healthText, "����"));
    }

    public void UpdateEnergy(int oldValue, int newValue)
    {
        if (canvasGroup == null || !isUIVisible) return;

        if (energyAnimCoroutine != null) StopCoroutine(energyAnimCoroutine);
        energyAnimCoroutine = StartCoroutine(AnimateBar(energyBar, oldValue, newValue, npc.maxEnergy, energyText, "����"));
    }

    public void UpdateMood(int oldValue, int newValue)
    {
        if (canvasGroup == null || !isUIVisible) return;

        if (moodAnimCoroutine != null) StopCoroutine(moodAnimCoroutine);
        moodAnimCoroutine = StartCoroutine(AnimateBar(moodBar, oldValue, newValue, npc.maxMood, moodText, "����"));
    }

    private IEnumerator AnimateBar(Slider bar, int oldValue, int newValue, int maxValue, TMP_Text text, string statName)
    {
        if (!isUIVisible) yield break;

        float startValue = (float)oldValue / maxValue;
        float endValue = (float)newValue / maxValue;
        float elapsedTime = 0;

        ShowChangeIndicator(bar.transform, newValue - oldValue);

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            float curveValue = animationCurve.Evaluate(t);

            bar.value = Mathf.Lerp(startValue, endValue, curveValue);

            int currentValue = Mathf.RoundToInt(Mathf.Lerp(oldValue, newValue, curveValue));
            text.text = $"{statName}: {currentValue}/{maxValue}";

            yield return null;
        }

        bar.value = endValue;
        text.text = $"{statName}: {newValue}/{maxValue}";
    }

    private void ShowChangeIndicator(Transform barTransform, int change)
    {
        if (change == 0 || !isUIVisible) return;

        GameObject indicator = new GameObject("ChangeIndicator");
        indicator.transform.SetParent(barTransform);
        indicator.transform.localPosition = Vector3.zero;

        TMP_Text indicatorText = indicator.AddComponent<TextMeshProUGUI>();
        indicatorText.text = change > 0 ? $"+{change}" : change.ToString();
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

    public void UpdateTexts()
    {
        if (canvasGroup == null || !isUIVisible) return;
        // �������ģ�
        // �������ģ�
        energyText.text = $"����(Energy)��{npc.energy}/{npc.maxEnergy}";
        moodText.text = $"����(Mood)��{npc.mood}/{npc.maxMood}";
        npcNameText.text = npc.npcName;
        npcPersonalityText.text = $"�Ը�{npc.personality}";
        npcBackgroundText.text = $"�������£�{npc.backgroundStory}";
        goldText.text = $"С��(Tips)��{npc.tips}";
        // ����Ӣ�ģ�
        //energyText.text = $"Energy��{npc.energy}/{npc.maxEnergy}";
        //moodText.text = $"Mood��{npc.mood}/{npc.maxMood}";
        npcNameText_eng.text = npc.npcName_eng;
        npcPersonalityText_eng.text = $"Personality��{npc.personality_eng}";
        npcBackgroundText_eng.text = $"Story��{npc.backgroundStory_eng}";
        //goldText.text = $"Tips��{npc.tips}";

    }
    public void InitializeUI()
    {
        if (npc == null) return;

        // �������ģ�
        energyText.text = $"����(Energy)��{npc.energy}/{npc.maxEnergy}";
        moodText.text = $"����(Mood)��{npc.mood}/{npc.maxMood}";
        npcNameText.text = npc.npcName;
        npcPersonalityText.text = $"�Ը�{npc.personality}";
        npcBackgroundText.text = $"�������£�{npc.backgroundStory}";
        goldText.text = $"С��(Tips)��{npc.tips}";
        // ����Ӣ�ģ�
        //energyText.text = $"Energy��{npc.energy}/{npc.maxEnergy}";
        //moodText.text = $"Mood��{npc.mood}/{npc.maxMood}";
        npcNameText_eng.text = npc.npcName_eng;
        npcPersonalityText_eng.text = $"Personality��{npc.personality_eng}";
        npcBackgroundText_eng.text = $"Story��{npc.backgroundStory_eng}";
        //goldText.text = $"Tips��{npc.tips}";
    }

}