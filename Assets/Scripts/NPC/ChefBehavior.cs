using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static RestaurantManager;

public class ChefBehavior : MonoBehaviour
{
    private RestaurantManager.Order currentOrder;

    [Header("NPC基本信息")]
    public string npcName = "厨师";
    private float currentMoveSpeed = 3f;
    public string personality = "专注";
    public string occupation = "厨师";

    [Header("厨师工作站点")]
    public Transform restPoint;
    public Transform fridgePoint;
    public Transform counterPoint;
    public Transform stovePoint;
    public Transform servingPoint;

    [Header("状态设置")]
    public bool hasOrder = false;
    public GameObject dialogueBubblePrefab;
    private GameObject currentBubble;
    private Coroutine bubbleCoroutine;
    public Vector3 bubbleOffset = new Vector3(0, 200, 0);
    public float bubbleDisplayTime = 3f;

    public enum ChefState
    {
        Idle,
        TakeFood,
        Prepare,
        Cook,
        Serve,
        MovingToDestination
    }

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private bool isFacingRight = false;
    private const int UP = 0;
    private const int DOWN = 1;
    private const int LEFT = 2;
    private const int RIGHT = 3;

    public ChefState chefState = ChefState.Idle;
    private bool isProcessingOrder = false; // 标记是否正在处理订单

    void Start()
    {
        InitializeAnimComponents();
        SetupTimeManager();

        // 直接启动简化的工作循环
        StartCoroutine(SimplifiedChefRoutine());
    }

    // 简化的主循环 - 不依赖复杂的状态机
    IEnumerator SimplifiedChefRoutine()
    {
        yield return new WaitForSeconds(1f); // 初始等待

        while (true)
        {
            // 如果有订单且没在处理中，开始处理
            if (hasOrder && currentOrder != null && !isProcessingOrder)
            {
                yield return StartCoroutine(ProcessOrder());
            }
            else if (!hasOrder && !isProcessingOrder)
            {
                // 空闲时回到休息点
                if (Vector3.Distance(transform.position, restPoint.position) > 2f)
                {
                    yield return StartCoroutine(SimpleMove(restPoint.position));
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // 处理订单的完整流程
    IEnumerator ProcessOrder()
    {
        isProcessingOrder = true;
        Debug.Log($"[{npcName}] 开始处理订单 #{currentOrder?.orderId}");

        // 1. 去冰箱取食材
        chefState = ChefState.TakeFood;
        Debug.Log($"[{npcName}] 步骤1：前往冰箱");
        yield return StartCoroutine(SimpleMove(fridgePoint.position));
        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);

        // 检查订单是否还有效
        if (currentOrder == null)
        {
            Debug.Log($"[{npcName}] 订单已取消");
            isProcessingOrder = false;
            hasOrder = false;
            chefState = ChefState.Idle;
            yield break;
        }

        // 2. 去案板加工
        chefState = ChefState.Prepare;
        Debug.Log($"[{npcName}] 步骤2：前往案板");
        yield return StartCoroutine(SimpleMove(counterPoint.position));
        yield return new WaitForSeconds(3f / TimeManager.Instance.timeScale);

        // 3. 去灶台烹饪
        chefState = ChefState.Cook;
        Debug.Log($"[{npcName}] 步骤3：前往灶台");
        yield return StartCoroutine(SimpleMove(stovePoint.position));
        yield return new WaitForSeconds(10f / TimeManager.Instance.timeScale);

        // 4. 去出餐口
        chefState = ChefState.Serve;
        Debug.Log($"[{npcName}] 步骤4：前往出餐口");
        yield return StartCoroutine(SimpleMove(servingPoint.position));

        // 5. 完成订单
        if (currentOrder != null)
        {
            ShowDialogueBubble($"{currentOrder.dishName}做好了！");
            RestaurantManager.CompleteOrder(currentOrder);
            Debug.Log($"[{npcName}] 订单 #{currentOrder.orderId} 完成");
        }

        // 重置状态
        currentOrder = null;
        hasOrder = false;
        isProcessingOrder = false;
        chefState = ChefState.Idle;

        // 立即检查新订单
        RestaurantManager.Order pendingOrder = RestaurantManager.TryAssignOrderToChef(this);
        if (pendingOrder != null)
        {
            Debug.Log($"[{npcName}] 接到新订单 #{pendingOrder.orderId}");
        }
    }

    // 简化的移动方法 - 不使用复杂的阈值判断
    IEnumerator SimpleMove(Vector3 targetPosition)
    {
        float startTime = Time.time;
        float maxTime = 10f;

        // 持续移动直到足够接近
        while (Time.time - startTime < maxTime)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);

            // 使用较大的判断距离
            if (distance < 1f)
            {
                // 直接设置到目标位置
                transform.position = targetPosition;
                if (animator != null)
                {
                    animator.SetBool("IsWalking", false);
                }
                yield break;
            }

            // 计算移动
            Vector3 direction = (targetPosition - transform.position).normalized;
            float step = currentMoveSpeed * Time.deltaTime;

            // 如果下一步会超过目标，直接到达
            if (step >= distance)
            {
                transform.position = targetPosition;
                if (animator != null)
                {
                    animator.SetBool("IsWalking", false);
                }
                yield break;
            }

            // 执行移动
            transform.position += direction * step;

            // 更新动画
            HandleAnimation(direction);

            yield return null;
        }

        // 超时后强制到达
        Debug.LogWarning($"[{npcName}] 移动超时，强制传送到目标");
        transform.position = targetPosition;
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
        }
    }

    // 分配订单 - 简化版本
    public void AssignOrder(RestaurantManager.Order order)
    {
        if (isProcessingOrder)
        {
            Debug.LogWarning($"[{npcName}] 正在处理其他订单，无法接受新订单");
            return;
        }

        currentOrder = order;
        hasOrder = true;
        Debug.Log($"[{npcName}] 接收订单 #{order.orderId}: {order.dishName}");
    }

    // 获取当前订单信息
    public RestaurantManager.Order GetCurrentOrder()
    {
        return currentOrder;
    }

    void SetupTimeManager()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.AddListener(OnTimeScaleChanged);
            currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
        }
    }

    void OnTimeScaleChanged(float newTimeScale)
    {
        currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.RemoveListener(OnTimeScaleChanged);
        }
        RestaurantManager.UnregisterChef(this);
    }

    void InitializeAnimComponents()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = isFacingRight;
        }

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetInteger("Direction", DOWN);
        }
    }

    void HandleAnimation(Vector2 moveDirection)
    {
        bool isMoving = moveDirection.magnitude > 0.1f;

        if (animator != null)
        {
            animator.SetBool("IsWalking", isMoving);

            if (isMoving)
            {
                int direction = GetDirection(moveDirection);
                animator.SetInteger("Direction", direction);
            }
        }

        if (spriteRenderer != null && moveDirection.x != 0)
        {
            bool shouldFaceRight = moveDirection.x > 0;
            if (shouldFaceRight != isFacingRight)
            {
                isFacingRight = shouldFaceRight;
                spriteRenderer.flipX = isFacingRight;
            }
        }
    }

    int GetDirection(Vector2 moveDirection)
    {
        if (moveDirection.magnitude < 0.1f)
        {
            return animator != null ? animator.GetInteger("Direction") : DOWN;
        }

        if (Mathf.Abs(moveDirection.y) > Mathf.Abs(moveDirection.x))
        {
            return moveDirection.y > 0 ? UP : DOWN;
        }
        else
        {
            return moveDirection.x > 0 ? RIGHT : LEFT;
        }
    }

    void ShowDialogueBubble(string text)
    {
        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
        }

        if (currentBubble == null)
        {
            currentBubble = Instantiate(dialogueBubblePrefab, transform);
            currentBubble.transform.localPosition = bubbleOffset;
        }

        currentBubble.SetActive(true);

        Transform panel = currentBubble.transform.Find("Panel");
        if (panel != null)
        {
            Transform contentTransform = panel.Find("Content");
            if (contentTransform != null)
            {
                TMPro.TMP_Text tmpText = contentTransform.GetComponent<TMPro.TMP_Text>();
                if (tmpText != null)
                {
                    tmpText.text = text;
                }
            }

            Transform nameLayer = panel.Find("Header");
            if (nameLayer != null)
            {
                Transform NPCNameLayer = nameLayer.Find("NPCName");
                if (NPCNameLayer != null)
                {
                    TMPro.TMP_Text nameText = NPCNameLayer.GetComponent<TMPro.TMP_Text>();
                    if (nameText != null)
                    {
                        nameText.text = npcName;
                    }
                }
            }
        }

        bubbleCoroutine = StartCoroutine(HideBubbleAfterDelay());
    }

    IEnumerator HideBubbleAfterDelay()
    {
        yield return new WaitForSeconds(bubbleDisplayTime);
        HideDialogueBubble();
    }

    void HideDialogueBubble()
    {
        if (currentBubble != null)
        {
            currentBubble.SetActive(false);
        }

        if (bubbleCoroutine != null)
        {
            StopCoroutine(bubbleCoroutine);
            bubbleCoroutine = null;
        }
    }
}