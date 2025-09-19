using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static RestaurantManager;

public class ChefBehavior : MonoBehaviour
{
    private RestaurantManager.Order currentOrder;

    [Header("NPC������Ϣ")]
    public string npcName = "��ʦ";
    private float currentMoveSpeed = 3f;
    public string personality = "רע";
    public string occupation = "��ʦ";

    [Header("��ʦ����վ��")]
    public Transform restPoint;
    public Transform fridgePoint;
    public Transform counterPoint;
    public Transform stovePoint;
    public Transform servingPoint;

    [Header("״̬����")]
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
    private bool isProcessingOrder = false; // ����Ƿ����ڴ�����

    void Start()
    {
        InitializeAnimComponents();
        SetupTimeManager();

        // ֱ�������򻯵Ĺ���ѭ��
        StartCoroutine(SimplifiedChefRoutine());
    }

    // �򻯵���ѭ�� - ���������ӵ�״̬��
    IEnumerator SimplifiedChefRoutine()
    {
        yield return new WaitForSeconds(1f); // ��ʼ�ȴ�

        while (true)
        {
            // ����ж�����û�ڴ����У���ʼ����
            if (hasOrder && currentOrder != null && !isProcessingOrder)
            {
                yield return StartCoroutine(ProcessOrder());
            }
            else if (!hasOrder && !isProcessingOrder)
            {
                // ����ʱ�ص���Ϣ��
                if (Vector3.Distance(transform.position, restPoint.position) > 2f)
                {
                    yield return StartCoroutine(SimpleMove(restPoint.position));
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // ����������������
    IEnumerator ProcessOrder()
    {
        isProcessingOrder = true;
        Debug.Log($"[{npcName}] ��ʼ������ #{currentOrder?.orderId}");

        // 1. ȥ����ȡʳ��
        chefState = ChefState.TakeFood;
        Debug.Log($"[{npcName}] ����1��ǰ������");
        yield return StartCoroutine(SimpleMove(fridgePoint.position));
        yield return new WaitForSeconds(1f / TimeManager.Instance.timeScale);

        // ��鶩���Ƿ���Ч
        if (currentOrder == null)
        {
            Debug.Log($"[{npcName}] ������ȡ��");
            isProcessingOrder = false;
            hasOrder = false;
            chefState = ChefState.Idle;
            yield break;
        }

        // 2. ȥ����ӹ�
        chefState = ChefState.Prepare;
        Debug.Log($"[{npcName}] ����2��ǰ������");
        yield return StartCoroutine(SimpleMove(counterPoint.position));
        yield return new WaitForSeconds(3f / TimeManager.Instance.timeScale);

        // 3. ȥ��̨���
        chefState = ChefState.Cook;
        Debug.Log($"[{npcName}] ����3��ǰ����̨");
        yield return StartCoroutine(SimpleMove(stovePoint.position));
        yield return new WaitForSeconds(10f / TimeManager.Instance.timeScale);

        // 4. ȥ���Ϳ�
        chefState = ChefState.Serve;
        Debug.Log($"[{npcName}] ����4��ǰ�����Ϳ�");
        yield return StartCoroutine(SimpleMove(servingPoint.position));

        // 5. ��ɶ���
        if (currentOrder != null)
        {
            ShowDialogueBubble($"{currentOrder.dishName}�����ˣ�");
            RestaurantManager.CompleteOrder(currentOrder);
            Debug.Log($"[{npcName}] ���� #{currentOrder.orderId} ���");
        }

        // ����״̬
        currentOrder = null;
        hasOrder = false;
        isProcessingOrder = false;
        chefState = ChefState.Idle;

        // ��������¶���
        RestaurantManager.Order pendingOrder = RestaurantManager.TryAssignOrderToChef(this);
        if (pendingOrder != null)
        {
            Debug.Log($"[{npcName}] �ӵ��¶��� #{pendingOrder.orderId}");
        }
    }

    // �򻯵��ƶ����� - ��ʹ�ø��ӵ���ֵ�ж�
    IEnumerator SimpleMove(Vector3 targetPosition)
    {
        float startTime = Time.time;
        float maxTime = 10f;

        // �����ƶ�ֱ���㹻�ӽ�
        while (Time.time - startTime < maxTime)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);

            // ʹ�ýϴ���жϾ���
            if (distance < 1f)
            {
                // ֱ�����õ�Ŀ��λ��
                transform.position = targetPosition;
                if (animator != null)
                {
                    animator.SetBool("IsWalking", false);
                }
                yield break;
            }

            // �����ƶ�
            Vector3 direction = (targetPosition - transform.position).normalized;
            float step = currentMoveSpeed * Time.deltaTime;

            // �����һ���ᳬ��Ŀ�ֱ꣬�ӵ���
            if (step >= distance)
            {
                transform.position = targetPosition;
                if (animator != null)
                {
                    animator.SetBool("IsWalking", false);
                }
                yield break;
            }

            // ִ���ƶ�
            transform.position += direction * step;

            // ���¶���
            HandleAnimation(direction);

            yield return null;
        }

        // ��ʱ��ǿ�Ƶ���
        Debug.LogWarning($"[{npcName}] �ƶ���ʱ��ǿ�ƴ��͵�Ŀ��");
        transform.position = targetPosition;
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
        }
    }

    // ���䶩�� - �򻯰汾
    public void AssignOrder(RestaurantManager.Order order)
    {
        if (isProcessingOrder)
        {
            Debug.LogWarning($"[{npcName}] ���ڴ��������������޷������¶���");
            return;
        }

        currentOrder = order;
        hasOrder = true;
        Debug.Log($"[{npcName}] ���ն��� #{order.orderId}: {order.dishName}");
    }

    // ��ȡ��ǰ������Ϣ
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