using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static NPCBehavior;

public class WaiterSelectionUI : MonoBehaviour
{
    [Header("UI引用")]
    public GameObject waiterCardPrefab;
    public Transform cardContainer; // ScrollView的Content
    public Button confirmButton;
    public Button cancelButton;
    public TextMeshProUGUI selectionCountText; // 显示 "已选择: 2/3"

    [Header("设置")]
    public int cardPoolSize = 8; // 卡池大小
    public int maxSelectCount = 3; // 最多选择数量

    private List<WaiterCardUI> currentCards = new List<WaiterCardUI>();
    private List<WaiterJsonData> currentPool = new List<WaiterJsonData>();
    private List<WaiterJsonData> selectedWaiters = new List<WaiterJsonData>();
    private RestaurantManager restaurantManager;

    void Start()
    {
        Debug.Log("[WaiterSelectionUI] Start被调用");
        // 先设置按钮监听
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancel);
        // 使用协程初始化
        StartCoroutine(InitializeAfterDataLoaded());
    }
    IEnumerator InitializeAfterDataLoaded()
    {
        // 等待RestaurantManager存在
        while (RestaurantManager.Instance == null)
        {
            yield return null;
        }
        restaurantManager = RestaurantManager.Instance;
        // 等待数据库加载完成
        while (restaurantManager.GetWaiterDatabase().Count == 0)
        {
            Debug.Log("[WaiterSelectionUI] 等待服务员数据库加载...");
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"[WaiterSelectionUI] 数据库已加载，包含 {restaurantManager.GetWaiterDatabase().Count} 个服务员");
        // 初始生成卡池
        GenerateWaiterPool();
        UpdateUI();
    }
    // 添加一个公共方法供外部调用刷新
    public void RefreshWaiterPool()
    {
        if (restaurantManager != null && restaurantManager.GetWaiterDatabase().Count > 0)
        {
            GenerateWaiterPool();
            UpdateUI();
        }
    }
    // 生成8个服务员卡池
    void GenerateWaiterPool()
    {
        Debug.Log("[WaiterSelectionUI] 开始生成服务员卡池...");

        // 清除现有卡片
        ClearCurrentCards();

        // 从数据库随机选取8个服务员
        currentPool = GetRandomWaiters(cardPoolSize);
        Debug.Log($"[WaiterSelectionUI] 获取到 {currentPool.Count} 个服务员数据");

        // 为每个服务员创建卡片
        foreach (var waiterData in currentPool)
        {
            CreateWaiterCard(waiterData);
        }

        Debug.Log($"[WaiterSelectionUI] 创建了 {currentCards.Count} 张卡片");
    }

    // 从数据库随机选取不重复的服务员
    List<WaiterJsonData> GetRandomWaiters(int count)
    {
        var allWaiters = restaurantManager.GetWaiterDatabase(); // 需要在RestaurantManager中添加这个方法

        // 如果总服务员少于需求数量，返回所有服务员
        if (allWaiters.Count <= count)
        {
            return new List<WaiterJsonData>(allWaiters);
        }

        // 随机选取
        var shuffled = allWaiters.OrderBy(x => Random.value).ToList();
        return shuffled.Take(count).ToList();
    }

    // 创建服务员卡片
    void CreateWaiterCard(WaiterJsonData waiterData)
    {
        Debug.Log($"[WaiterSelectionUI] 创建卡片: {waiterData.name}");
        // 检查父对象链
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
            Debug.LogError("[WaiterSelectionUI] 卡片预制体上没有WaiterCardUI组件!");
        }
    }

    // 清除当前所有卡片
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

    // 当卡片被点击时调用
    public void OnWaiterCardClicked(WaiterCardUI card)
    {
        if (card.IsSelected)
        {
            // 如果已选中，则取消选择
            selectedWaiters.Remove(card.WaiterData);
            card.SetSelected(false);
        }
        else
        {
            // 如果未选中，检查是否达到上限
            if (selectedWaiters.Count < maxSelectCount)
            {
                selectedWaiters.Add(card.WaiterData);
                card.SetSelected(true);
            }
            else
            {
                Debug.Log($"最多只能选择{maxSelectCount}名服务员！");
            }
        }

        UpdateUI();
    }

    // 更新UI显示
    void UpdateUI()
    {
        // 更新选择计数
        if (selectionCountText != null)
        {
            selectionCountText.text = $"已选择: {selectedWaiters.Count}/{maxSelectCount}";
        }

        // 更新按钮状态
        confirmButton.interactable = selectedWaiters.Count > 0;
    }

    // 点击确认按钮
    void OnConfirm()
    {
        if (selectedWaiters.Count == 0)
        {
            Debug.LogWarning("请至少选择一名服务员！");
            return;
        }

        Debug.Log($"确认选择了 {selectedWaiters.Count} 名服务员");

        // 通知 RestaurantManager 记录选中的服务员
        RestaurantManager.Instance.SetSelectedWaiters(selectedWaiters);

        // TODO: 进入第二步
        // 这里可以调用其他系统或切换到下一个界面

        // 暂时隐藏选择界面
        this.gameObject.SetActive(false);
    }

    // 点击取消按钮 - 移除已选的3个，重新随机8个
    void OnCancel()
    {
        // 从当前池中移除已选择的服务员
        var remainingPool = currentPool.Where(w => !selectedWaiters.Contains(w)).ToList();

        // 清除界面
        ClearCurrentCards();

        // 获取新的服务员填充到8个
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

        // 打乱顺序
        currentPool = remainingPool.OrderBy(x => Random.value).ToList();

        // 重新创建卡片
        foreach (var waiterData in currentPool)
        {
            CreateWaiterCard(waiterData);
        }

        UpdateUI();



    }


    // 获取选中的服务员
    public List<WaiterJsonData> GetSelectedWaiters()
    {
        return new List<WaiterJsonData>(selectedWaiters);
    }
    public void EnsureTimePaused()
    {
        // 获取TimeManager实例并确保时间暂停
        TimeManager timeManager = TimeManager.Instance;
        if (timeManager != null)
        {
            timeManager.SetCustomTimeScale(0f);
        }
    }

}