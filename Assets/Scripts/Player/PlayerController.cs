using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float currentMoveSpeed = 3f;
    private bool isFacingRight = false; // 追踪当前朝向

    void Start()
    {
        // 初始化组件
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 确保精灵为空
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.flipX = isFacingRight; // 设置初始朝向
        }

        // 订阅时间缩放改变事件
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.AddListener(OnTimeScaleChanged);
            currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
        }

        // 设置初始动画方向
        if (animator != null)
        {
            animator.SetInteger("Direction", 1); // 默认朝下
        }
    }

    void OnDestroy()
    {
        // 取消订阅
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.RemoveListener(OnTimeScaleChanged);
        }
    }

    void OnTimeScaleChanged(float newTimeScale)
    {
        // 更新移动速度
        currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
    }

    void Update()
    {
        // 获取输入
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 计算移动方向
        Vector2 moveDirection = new Vector2(horizontal, vertical).normalized;

        // 移动
        transform.position += (Vector3)moveDirection * currentMoveSpeed * Time.deltaTime;

        // 动画控制
        HandleAnimation(moveDirection);
    }

    void HandleAnimation(Vector2 moveDirection)
    {
        // 检测是否在移动
        bool isMoving = moveDirection.magnitude > 0.1f;

        // 更新Animator的移动状态
        if (animator != null)
        {
            animator.SetBool("IsWalking", isMoving);
        }

        // 更新方向
        int direction = GetDirection(moveDirection);
        if (animator != null)
        {
            animator.SetInteger("Direction", direction);
        }

        // 手动控制翻转
        if (spriteRenderer != null)
        {
            // 水平移动时更新朝向
            if (moveDirection.x != 0)
            {
                bool shouldFaceRight = moveDirection.x > 0;

                // 只在方向改变时更新
                if (shouldFaceRight != isFacingRight)
                {
                    isFacingRight = shouldFaceRight;
                    spriteRenderer.flipX = isFacingRight;
                }
            }

            // 调试输出
            //Debug.Log($"方向: {GetDirectionName(direction)}, FlipX: {spriteRenderer.flipX}");
        }
    }

    int GetDirection(Vector2 moveDirection)
    {
        // 当没有移动时，保持最后方向
        if (moveDirection.magnitude < 0.1f)
        {
            return animator != null ? animator.GetInteger("Direction") : 1;
        }

        // 确定主导方向（垂直优先）
        if (Mathf.Abs(moveDirection.y) > Mathf.Abs(moveDirection.x))
        {
            return moveDirection.y > 0 ? 0 : 1; // 上:0, 下:1
        }
        else
        {
            return moveDirection.x > 0 ? 3 : 2; // 右:3, 左:2
        }
    }

    string GetDirectionName(int direction)
    {
        return direction switch
        {
            0 => "上",
            1 => "下",
            2 => "左",
            3 => "右",
            _ => "未知"
        };
    }
}