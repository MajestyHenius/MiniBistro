using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
// �������;����ʾ״̬�ͽ���״������ʾ��Ҵߴ��ϲˡ�Ͷ�ߵȡ�
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
    public Vector3 offset = new Vector3(0, -4.5f, 0); //�Ի���1.5-2λ�ã����ͷ��ͼ����Կ���һ��

    public CustomerNPC customer;
    private Camera mainCamera;
    private bool isUIVisible = true;

    void Start()
    {
        // ���Ի�ȡCustomer����
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

        // ȷ��overheadPanel����
        if (overheadPanel == null)
        {
            overheadPanel = transform.Find("OverheadPanel")?.gameObject;
            if (overheadPanel == null)
            {
                Debug.LogError("overheadPanel not found!");
                return;
            }
        }

        // �����Զ���ȡUI�������
        if (statusText == null) statusText = overheadPanel.transform.Find("StatusText")?.GetComponent<TMP_Text>();
        if (nameText == null) nameText = overheadPanel.transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (satisfactionText == null) satisfactionText = overheadPanel.transform.Find("SatisfactionText")?.GetComponent<TMP_Text>();
        if (emergencyIcon == null) emergencyIcon = overheadPanel.transform.Find("EmergencyIcon")?.gameObject;

        // ��ʼ��UI
        UpdateUI();
    }

    void Update()
    {
        if (mainCamera != null)
        {
            // ����UI�������
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                           mainCamera.transform.rotation * Vector3.up);
        }

        if (customer != null)
        {
            transform.position = customer.transform.position + offset;

            // ʵʱ����UI
            UpdateUI();
        }
    }

    // ����UI
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
        satisfactionText.text = $"����ȣ�{customer.satisfaction:F0}";
        satisfactionText_eng.text = $"Satisfication��{customer.satisfaction:F0}";
        nameText.text = customer.customerName;
        nameText_eng.text = customer.customerName_eng;
        // ���ݽ���״̬��ʾ�����ؽ���ͼ��
        if (emergencyIcon != null)
        {
            emergencyIcon.SetActive(customer.currentState == CustomerNPC.CustomerState.Emergency);
        }
    }

    // ��ʾUI
    public void ShowUI()
    {
        if (overheadPanel != null)
        {
            isUIVisible = true;
            overheadPanel.SetActive(true);
        }
    }

    // ����UI
    public void HideUI()
    {
        if (overheadPanel != null)
        {
            isUIVisible = false;
            overheadPanel.SetActive(false);
        }
    }
}