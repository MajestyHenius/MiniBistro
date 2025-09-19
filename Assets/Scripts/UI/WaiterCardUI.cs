using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static NPCBehavior;
public class WaiterCardUI : MonoBehaviour
{
    [Header("UI���")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI personalityText;
    public TextMeshProUGUI backgroundText;
    public TextMeshProUGUI staminaText;
    public TextMeshProUGUI moodText;
    public TextMeshProUGUI nameText_eng;
    public TextMeshProUGUI personalityText_eng;
    public TextMeshProUGUI backgroundText_eng;
    public TextMeshProUGUI staminaText_eng;
    public TextMeshProUGUI moodText_eng;


    [Header("ѡ��״̬")]
    public GameObject selectionIndicator; // ѡ�б�ǣ�����һ������
    public Color selectedColor = new Color(0.8f, 1f, 0.8f);
    public Color normalColor = Color.white;

    private bool isSelected = false;
    private WaiterSelectionUI selectionUI;
    private Image backgroundImage;
    private Button button;

    public bool IsSelected => isSelected;
    public WaiterJsonData WaiterData { get; private set; }

    void Awake()
    {
        backgroundImage = GetComponent<Image>();
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
    }

    public void Setup(WaiterJsonData data, WaiterSelectionUI ui)
    {
        WaiterData = data;
        selectionUI = ui;

        // ������ʾ����
        // ���ģ�
        nameText.text = data.name;
        personalityText.text = $"�Ը�: {data.personalityType}";
        backgroundText.text = $"����: {data.story}";
        staminaText.text = $"����: {data.Energy}/100";
        moodText.text = $"����: {data.Mood}/100";
        // Ӣ�ģ�
        nameText_eng.text = data.name_eng;
        personalityText_eng.text = $"Personality: {data.personalityType_eng}";
        backgroundText_eng.text = $"Story: {data.story_eng}";
        staminaText_eng.text = $"Energy: {data.Energy}/100";
        moodText_eng.text = $"Mood: {data.Mood}/100";

        // ����ѡ��״̬
        SetSelected(false);
    }

    void OnClicked()
    {
        selectionUI.OnWaiterCardClicked(this);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        backgroundImage.color = isSelected ? selectedColor : normalColor;

        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(isSelected);
        }
    }

    void Update()
    {
        // �򵥵������
        Vector3 mousePos = Input.mousePosition;
        RectTransform rect = GetComponent<RectTransform>();

        if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos))
        {
            if (Input.GetMouseButtonDown(0))
            {
                //Debug.Log($"[DEBUG] ֱ�ӵ����⵽: {gameObject.name}");
                OnClicked(); // ֱ�ӵ��õ������
            }
        }
    }
}