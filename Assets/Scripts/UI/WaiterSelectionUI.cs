using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NPCBehavior;

public class WaiterSelectionUI : MonoBehaviour
{
    [Header("UI����")]
    public GameObject waiterCardPrefab;
    public Transform cardContainer; // ScrollView��Content
    public Button confirmButton;
    public Button cancelButton;
    public TextMeshProUGUI selectionCountText; // ��ʾ "��ѡ��: 2/3"

    [Header("����")]
    public int cardPoolSize = 8; // ���ش�С
    public int maxSelectCount = 3; // ���ѡ������

    private List<WaiterCardUI> currentCards = new List<WaiterCardUI>();
    private List<WaiterJsonData> currentPool = new List<WaiterJsonData>();
    private List<WaiterJsonData> selectedWaiters = new List<WaiterJsonData>();
    private RestaurantManager restaurantManager;

    void Start()
    {
        Debug.Log("[WaiterSelectionUI] Start������");
        // �����ð�ť����
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
        // ʹ��Э�̳�ʼ��
        StartCoroutine(InitializeAfterDataLoaded());
    }
    IEnumerator InitializeAfterDataLoaded()
    {
        // �ȴ�RestaurantManager����
        while (RestaurantManager.Instance == null)
        {
            yield return null;
        }
        restaurantManager = RestaurantManager.Instance;
        // �ȴ����ݿ�������
        while (restaurantManager.GetWaiterDatabase().Count == 0)
        {
            Debug.Log("[WaiterSelectionUI] �ȴ�����Ա���ݿ����...");
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"[WaiterSelectionUI] ���ݿ��Ѽ��أ����� {restaurantManager.GetWaiterDatabase().Count} ������Ա");
        // ��ʼ���ɿ���
        GenerateWaiterPool();
        UpdateUI();
    }
    // ���һ�������������ⲿ����ˢ��
    public void RefreshWaiterPool()
    {
        if (restaurantManager != null && restaurantManager.GetWaiterDatabase().Count > 0)
        {
            GenerateWaiterPool();
            UpdateUI();
        }
    }
    // ����8������Ա����
    void GenerateWaiterPool()
    {
        Debug.Log("[WaiterSelectionUI] ��ʼ���ɷ���Ա����...");

        // ������п�Ƭ
        ClearCurrentCards();

        // �����ݿ����ѡȡ8������Ա
        currentPool = GetRandomWaiters(cardPoolSize);
        Debug.Log($"[WaiterSelectionUI] ��ȡ�� {currentPool.Count} ������Ա����");

        // Ϊÿ������Ա������Ƭ
        foreach (var waiterData in currentPool)
        {
            CreateWaiterCard(waiterData);
        }

        Debug.Log($"[WaiterSelectionUI] ������ {currentCards.Count} �ſ�Ƭ");
    }

    // �����ݿ����ѡȡ���ظ��ķ���Ա
    List<WaiterJsonData> GetRandomWaiters(int count)
    {
        var allWaiters = restaurantManager.GetWaiterDatabase(); // ��Ҫ��RestaurantManager������������

        // ����ܷ���Ա���������������������з���Ա
        if (allWaiters.Count <= count)
        {
            return new List<WaiterJsonData>(allWaiters);
        }

        // ���ѡȡ
        var shuffled = allWaiters.OrderBy(x => Random.value).ToList();
        return shuffled.Take(count).ToList();
    }

    // ��������Ա��Ƭ
    void CreateWaiterCard(WaiterJsonData waiterData)
    {
        Debug.Log($"[WaiterSelectionUI] ������Ƭ: {waiterData.name}");
        // ��鸸������
        Transform current = cardContainer;
        while (current != null)
        {
            current = current.parent;
        }

        GameObject cardObj = Instantiate(waiterCardPrefab, cardContainer);


        WaiterCardUI cardUI = cardObj.GetComponent<WaiterCardUI>();

        if (cardUI != null)
        {
            cardUI.Setup(waiterData, this);
            currentCards.Add(cardUI);

        }
        else
        {
            Debug.LogError("[WaiterSelectionUI] ��ƬԤ������û��WaiterCardUI���!");
        }
    }

    // �����ǰ���п�Ƭ
    void ClearCurrentCards()
    {
        foreach (var card in currentCards)
        {
            Destroy(card.gameObject);
        }
        currentCards.Clear();
        currentPool.Clear();
        selectedWaiters.Clear();
    }

    // ����Ƭ�����ʱ����
    public void OnWaiterCardClicked(WaiterCardUI card)
    {
        if (card.IsSelected)
        {
            // �����ѡ�У���ȡ��ѡ��
            selectedWaiters.Remove(card.WaiterData);
            card.SetSelected(false);
        }
        else
        {
            // ���δѡ�У�����Ƿ�ﵽ����
            if (selectedWaiters.Count < maxSelectCount)
            {
                selectedWaiters.Add(card.WaiterData);
                card.SetSelected(true);
            }
            else
            {
                Debug.Log($"���ֻ��ѡ��{maxSelectCount}������Ա��");
            }
        }

        UpdateUI();
    }

    // ����UI��ʾ
    void UpdateUI()
    {
        // ����ѡ�����
        if (selectionCountText != null)
        {
            selectionCountText.text = $"��ѡ��: {selectedWaiters.Count}/{maxSelectCount}";
        }

        // ���°�ť״̬
        confirmButton.interactable = selectedWaiters.Count > 0;
    }

    // ���ȷ�ϰ�ť
    void OnConfirm()
    {
        if (selectedWaiters.Count == 0)
        {
            Debug.LogWarning("������ѡ��һ������Ա��");
            return;
        }

        Debug.Log($"ȷ��ѡ���� {selectedWaiters.Count} ������Ա");

        // ֪ͨ RestaurantManager ��¼ѡ�еķ���Ա
        RestaurantManager.Instance.SetSelectedWaiters(selectedWaiters);

        // TODO: ����ڶ���
        // ������Ե�������ϵͳ���л�����һ������

        // ��ʱ����ѡ�����
        this.gameObject.SetActive(false);
    }

    // ���ȡ����ť - �Ƴ���ѡ��3�����������8��
    void OnCancel()
    {
        // �ӵ�ǰ�����Ƴ���ѡ��ķ���Ա
        var remainingPool = currentPool.Where(w => !selectedWaiters.Contains(w)).ToList();

        // �������
        ClearCurrentCards();

        // ��ȡ�µķ���Ա��䵽8��
        int needCount = cardPoolSize - remainingPool.Count;
        if (needCount > 0)
        {
            var allWaiters = restaurantManager.GetWaiterDatabase();
            var availableWaiters = allWaiters.Where(w => !remainingPool.Contains(w)).ToList();

            if (availableWaiters.Count > 0)
            {
                var newWaiters = availableWaiters.OrderBy(x => Random.value).Take(needCount).ToList();
                remainingPool.AddRange(newWaiters);
            }
        }

        // ����˳��
        currentPool = remainingPool.OrderBy(x => Random.value).ToList();

        // ���´�����Ƭ
        foreach (var waiterData in currentPool)
        {
            CreateWaiterCard(waiterData);
        }

        UpdateUI();



    }


    // ��ȡѡ�еķ���Ա
    public List<WaiterJsonData> GetSelectedWaiters()
    {
        return new List<WaiterJsonData>(selectedWaiters);
    }
    public void EnsureTimePaused()
    {
        // ��ȡTimeManagerʵ����ȷ��ʱ����ͣ
        TimeManager timeManager = TimeManager.Instance;
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
        }
    }

}