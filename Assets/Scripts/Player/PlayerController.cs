using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private float currentMoveSpeed = 3f;
    private bool isFacingRight = false; // ׷�ٵ�ǰ����

    void Start()
    {
        // ��ʼ�����
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // ȷ������Ϊ��
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.flipX = isFacingRight; // ���ó�ʼ����
        }

        // ����ʱ�����Ÿı��¼�
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.AddListener(OnTimeScaleChanged);
            currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
        }

        // ���ó�ʼ��������
        if (animator != null)
        {
            animator.SetInteger("Direction", 1); // Ĭ�ϳ���
        }
    }

    void OnDestroy()
    {
        // ȡ������
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeScaleChanged.RemoveListener(OnTimeScaleChanged);
        }
    }

    void OnTimeScaleChanged(float newTimeScale)
    {
        // �����ƶ��ٶ�
        currentMoveSpeed = TimeManager.Instance.GetScaledMoveSpeed();
    }

    void Update()
    {
        // ��ȡ����
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // �����ƶ�����
        Vector2 moveDirection = new Vector2(horizontal, vertical).normalized;

        // �ƶ�
        transform.position += (Vector3)moveDirection * currentMoveSpeed * Time.deltaTime;

        // ��������
        HandleAnimation(moveDirection);
    }

    void HandleAnimation(Vector2 moveDirection)
    {
        // ����Ƿ����ƶ�
        bool isMoving = moveDirection.magnitude > 0.1f;

        // ����Animator���ƶ�״̬
        if (animator != null)
        {
            animator.SetBool("IsWalking", isMoving);
        }

        // ���·���
        int direction = GetDirection(moveDirection);
        if (animator != null)
        {
            animator.SetInteger("Direction", direction);
        }

        // �ֶ����Ʒ�ת
        if (spriteRenderer != null)
        {
            // ˮƽ�ƶ�ʱ���³���
            if (moveDirection.x != 0)
            {
                bool shouldFaceRight = moveDirection.x > 0;

                // ֻ�ڷ���ı�ʱ����
                if (shouldFaceRight != isFacingRight)
                {
                    isFacingRight = shouldFaceRight;
                    spriteRenderer.flipX = isFacingRight;
                }
            }

            // �������
            //Debug.Log($"����: {GetDirectionName(direction)}, FlipX: {spriteRenderer.flipX}");
        }
    }

    int GetDirection(Vector2 moveDirection)
    {
        // ��û���ƶ�ʱ�����������
        if (moveDirection.magnitude < 0.1f)
        {
            return animator != null ? animator.GetInteger("Direction") : 1;
        }

        // ȷ���������򣨴�ֱ���ȣ�
        if (Mathf.Abs(moveDirection.y) > Mathf.Abs(moveDirection.x))
        {
            return moveDirection.y > 0 ? 0 : 1; // ��:0, ��:1
        }
        else
        {
            return moveDirection.x > 0 ? 3 : 2; // ��:3, ��:2
        }
    }

    string GetDirectionName(int direction)
    {
        return direction switch
        {
            0 => "��",
            1 => "��",
            2 => "��",
            3 => "��",
            _ => "δ֪"
        };
    }
}