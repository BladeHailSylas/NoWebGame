using UnityEngine;

/// <summary>
/// 시전자의 이동 변화를 추적하여 방향과 속도를 계산하는 컴포넌트.
/// TowardsMovement 등에서 마지막 이동 방향을 참조하기 위해 사용.
/// </summary>
[DisallowMultipleComponent]
public class MovementTracker : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("디버그 출력을 활성화하면 매 프레임 delta 및 속도를 콘솔에 표시합니다.")]
    public bool debugLog = true;

    [Tooltip("정지로 간주할 최소 이동 거리 (미터 단위)")]
    public float stopThreshold = 0.001f;

    // 결정적 연산을 위해 FixedVector2를 사용
    public FixedVector2 LastPosition { get; private set; }
    public FixedVector2 Delta { get; private set; }
    public float Speed { get; private set; }

    private int _frameCount;

    private void Awake()
    {
        LastPosition = FixedVector2.FromVector2(transform.position);
        Delta = new FixedVector2(0, 0);
        Speed = 0f;
        _frameCount = 0;
    }

    private void LateUpdate()
    {
        _frameCount++;

        // 현재 위치 계산
        FixedVector2 current = FixedVector2.FromVector2(transform.position);
        //Debug.Log($"Current at {current}, previously {LastPosition}");
        Delta = current - LastPosition;
        LastPosition = current;

        // 이동 거리 및 속도 계산
        float distance = Delta.AsVector2.magnitude;
        Speed = distance / Time.deltaTime;

        // 디버그 출력
        if (debugLog && _frameCount % 10 == 0) // 10프레임마다 출력
        {
            if (distance > stopThreshold)
            {
                Debug.Log($"[MovementTracker] Δ=({Delta.AsVector2.x:F3}, {Delta.AsVector2.y:F3})  " +
                          $"Speed={Speed:F3} m/s");
            }
            else
            {
                Debug.Log("[MovementTracker] 정지 상태 감지");
            }
        }
    }

    /// <summary>
    /// 마지막 이동 방향을 정규화하여 반환합니다.
    /// 이동이 없었다면 (0,0) 반환.
    /// </summary>
    public FixedVector2 GetLastDirectionNormalized()
    {
        var v = Delta;
        if (v.RawX == 0 && v.RawY == 0)
            return new FixedVector2(0, 0);

        double mag = System.Math.Sqrt(v.RawX * (double)v.RawX + v.RawY * (double)v.RawY);
        return new FixedVector2((int)(v.RawX / mag), (int)(v.RawY / mag));
    }

    /// <summary>
    /// 정지 여부를 반환합니다.
    /// </summary>
    public bool IsStationary()
    {
        return Delta.AsVector2.magnitude < stopThreshold;
    }
}