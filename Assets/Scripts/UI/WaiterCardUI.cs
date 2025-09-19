using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static NPCBehavior;
public class WaiterCardUI : MonoBehaviour
{
    [Header("UI组件")]
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


    [Header("选择状态")]
    public GameObject selectionIndicator; // 选中标记（比如一个勾）
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

        // 设置显示内容
        // 中文：
        nameText.text = data.name;
        personalityText.text = $"性格: {data.personalityType}";
        backgroundText.text = $"背景: {data.story}";
        staminaText.text = $"体力: {data.Energy}/100";
        moodText.text = $"心情: {data.Mood}/100";
        // 英文：
        nameText_eng.text = data.name_eng;
        personalityText_eng.text = $"Personality: {data.personalityType_eng}";
        backgroundText_eng.text = $"Story: {data.story_eng}";
        staminaText_eng.text = $"Energy: {data.Energy}/100";
        moodText_eng.text = $"Mood: {data.Mood}/100";

        // 重置选择状态
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
        // 简单的鼠标检测
        Vector3 mousePos = Input.mousePosition;
        RectTransform rect = GetComponent<RectTransform>();

        if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos))
        {
            if (Input.GetMouseButtonDown(0))
            {
                //Debug.Log($"[DEBUG] 直接点击检测到: {gameObject.name}");
                OnClicked(); // 直接调用点击方法
            }
        }
    }
}