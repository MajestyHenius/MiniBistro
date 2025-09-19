using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
// 这个的用途是显示状态和紧急状况，提示玩家催促上菜、投诉等。
public class CustomerOverheadUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject overheadPanel;
    public TMP_Text statusText;
    public TMP_Text nameText;
    public TMP_Text satisfactionText;
    public TMP_Text statusText_eng;
    public TMP_Text nameText_eng;
    public TMP_Text satisfactionText_eng;
    public GameObject emergencyIcon;

    [Header("UI Settings")]
    public Vector3 offset = new Vector3(0, -4.5f, 0); //对话在1.5-2位置，这个头顶图标可以靠下一点

    public CustomerNPC customer;
    private Camera mainCamera;
    private bool isUIVisible = true;

    void Start()
    {
        // 尝试获取Customer引用
        if (customer == null)
        {
            customer = GetComponentInParent<CustomerNPC>();
        }

        mainCamera = Camera.main;

        if (customer == null)
        {
            Debug.LogError("CustomerNPC not found in parent!");
            return;
        }

        // 确保overheadPanel存在
        if (overheadPanel == null)
        {
            overheadPanel = transform.Find("OverheadPanel")?.gameObject;
            if (overheadPanel == null)
            {
                Debug.LogError("overheadPanel not found!");
                return;
            }
        }

        // 尝试自动获取UI组件引用
        if (statusText == null) statusText = overheadPanel.transform.Find("StatusText")?.GetComponent<TMP_Text>();
        if (nameText == null) nameText = overheadPanel.transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (satisfactionText == null) satisfactionText = overheadPanel.transform.Find("SatisfactionText")?.GetComponent<TMP_Text>();
        if (emergencyIcon == null) emergencyIcon = overheadPanel.transform.Find("EmergencyIcon")?.gameObject;

        // 初始化UI
        UpdateUI();
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

            // 实时更新UI
            UpdateUI();
        }
    }

    // 更新UI
    public void UpdateUI()
    {
        if (customer == null) return;

        statusText.text = customer.GetCustomerState();
        statusText_eng.text = customer.GetCustomerState_eng();
        if (customer.currentState == CustomerNPC.CustomerState.Emergency)
        { 
            statusText.color = Color.red;
            statusText_eng.color = Color.red;
        }
        else 
        {
            statusText.color = Color.white;
            statusText_eng.color = Color.white;
        }
        satisfactionText.text = $"满意度：{customer.satisfaction:F0}";
        satisfactionText_eng.text = $"Satisfication：{customer.satisfaction:F0}";
        nameText.text = customer.customerName;
        nameText_eng.text = customer.customerName_eng;
        // 根据紧急状态显示或隐藏紧急图标
        if (emergencyIcon != null)
        {
            emergencyIcon.SetActive(customer.currentState == CustomerNPC.CustomerState.Emergency);
        }
    }

    // 显示UI
    public void ShowUI()
    {
        if (overheadPanel != null)
        {
            isUIVisible = true;
            overheadPanel.SetActive(true);
        }
    }

    // 隐藏UI
    public void HideUI()
    {
        if (overheadPanel != null)
        {
            isUIVisible = false;
            overheadPanel.SetActive(false);
        }
    }
}