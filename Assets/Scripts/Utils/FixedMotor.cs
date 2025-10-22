using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FixedMotor : MonoBehaviour
{
    public CollisionPolicy policy = new()
    {
        wallsMask = LayerMask.GetMask("Walls&Obstacles"),
        enemyMask = LayerMask.GetMask("Foe"),
        enemyAsBlocker = true,
        unitradius = 500,
        unitskin = 125,
        allowWallSlide = true
    };

    private Rigidbody2D _rb;
    private Collider2D _col;
    private FixedVector2 _position;
    private bool _needsSync;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _col = GetComponent<Collider2D>();
        _position = new FixedVector2(transform.position);
    }

    private void LateUpdate()
    {
        if (_needsSync)
        {
            transform.position = _position.asVector2;
            _needsSync = false;
        }
    }

    /// <summary>
    /// 벽과의 충돌을 감안하여 delta 방향 이동을 시도합니다.
    /// enemyAsBlocker가 false라면 enemyMask는 무시합니다.
    /// </summary>
    public void Move(FixedVector2 desiredDelta, Rigidbody2D rb = null)
    {
        rb ??= _rb;
        Vector2 delta = desiredDelta.ToVector2() / 60;
        if (delta.sqrMagnitude < 0)
            return;

        //MoveResult result = default;

        // 1️⃣ 벽과 충돌 시 법선 성분 제거
        delta = RemoveNormalComponent(delta, policy.wallsMask);

        // 2️⃣ 적과 충돌 시, 적이 blocker일 때만 제거
        if (policy.enemyAsBlocker)
            delta = RemoveNormalComponent(delta, policy.enemyMask);
        // 3️⃣ 이동 수행
        Vector2 target = rb.position + delta;
        rb.MovePosition(target);
        _position = new FixedVector2(target);
        _needsSync = true;
    }
#region ===== Utils =====
    /// <summary>
    /// 이동 벡터에서 충돌체의 법선 방향 성분을 제거하여 "벽을 따라 미끄러지는" 효과를 만듭니다.
    /// </summary>
    private Vector2 RemoveNormalComponent(Vector2 vector, LayerMask mask, bool treatedAsBlocker = true)
    {
        // Bridge deterministic data to Unity physics by operating in float space locally.
        Vector2 vfinalFloat = vector;
        float magnitude = vfinalFloat.magnitude;
        if (magnitude <= 0f)
        {
            return new Vector2(0, 0);
        }

        Vector2 origin = _rb.position;
        Vector2 direction = vfinalFloat.normalized;
        float skinRadius = (policy.unitradius + policy.unitskin) / (float)FixedVector2.UnitsPerFloat;
        float distance = vector.magnitude;
        //var maskHit = Physics2D.CircleCastAll(origin, .unitradius, direction, magnitude, mask);
        var maskHit = Physics2D.CircleCastAll(origin, skinRadius, direction, distance, mask);
        foreach (var hit in maskHit)
        {
            if (!hit.collider)
            {
                continue;
            }

            if (mask == policy.enemyMask && !policy.enemyAsBlocker)
            {
                continue;
            }
            Vector2 nFloat = hit.normal.normalized;
            float dot = Vector2.Dot(vfinalFloat, nFloat);
            if (Mathf.Abs(dot) > 0f && treatedAsBlocker)
            {
                vfinalFloat -= dot * nFloat;
            }
        }

        return vfinalFloat;
    }
    /// <summary>
    /// Collider 겹침을 해소하기 위해 보정 벡터를 계산하고 적용합니다.
    /// </summary>
    public void Depenetrate()
    {
        if (!_rb || !_col)
            return;

        LayerMask blockers = policy.wallsMask;
        if (policy.enemyAsBlocker)
            blockers |= policy.enemyMask;

        ContactFilter2D filter = new() { useLayerMask = true, useTriggers = false };
        filter.SetLayerMask(blockers);

        Collider2D[] overlaps = new Collider2D[8];
        int count = _col.Overlap(filter, overlaps);
        if (count == 0)
            return;

        Vector2 correction = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            var other = overlaps[i];
            if (!other)
                continue;

            var distInfo = _col.Distance(other);
            if (!distInfo.isOverlapped)
                continue;

            correction += distInfo.normal * distInfo.distance;
        }

        if (correction.sqrMagnitude < 1e-6f)
            return;

        Vector2 newPos = _rb.position + correction;
        _rb.MovePosition(newPos);
        _position = new FixedVector2(newPos);
        _needsSync = true;
    }
}
#endregion