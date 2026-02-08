using UnityEngine;

public class RemotePlayerController : MonoBehaviour
{
    private Vector3 targetPosition;
    private Vector3 currentVelocity;

    [Header("Movement Settings")]
    [Tooltip("이동 속도 (높을수록 빠름)")]
    public float moveSpeed = 500f;

    [Tooltip("부드러운 이동 시간 (낮을수록 즉각 반응)")]
    public float smoothTime = 0.01f;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        // 목표 위치로 부드럽게 이동
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref currentVelocity,
                smoothTime,
                moveSpeed
            );
        }
    }

    // 외부에서 목표 위치 설정
    public void SetTargetPosition(Vector3 newTarget)
    {
        targetPosition = newTarget;
    }
}