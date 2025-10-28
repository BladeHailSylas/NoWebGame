using System;
using UnityEngine;

/// <summary>
/// Deterministic movement bridge between FixedVector2 logic and Unity 2D physics.
/// </summary>
public class FixedMotor
{
    private readonly Rigidbody2D _rb;
    private readonly Collider2D _col;
    private FixedVector2 _pos;
    private bool _needsSync;
    public CollisionPolicy Policy { get; set; }

    public FixedMotor(Rigidbody2D rb, Collider2D col)
    {
        _rb = rb ?? throw new ArgumentNullException(nameof(rb));
        _col = col ?? throw new ArgumentNullException(nameof(col));
        Policy = new()
        {
            wallsMask = LayerMask.GetMask("Walls&Obstacles"),
            enemyMask = LayerMask.GetMask("Foe"),
            enemyAsBlocker = true,
            unitRadius = 500,
            unitSkin = 125,
            allowWallSlide = true
        };
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        _pos = new FixedVector2(rb.position);
    }

    /// <summary>
    /// Synchronizes Unity transform with deterministic position.
    /// Should be called from PlayerScript.LateUpdate().
    /// </summary>
    public void SyncPosition()
    {
        if (!_needsSync) return;
        _col.transform.position = _pos.AsVector2;
        _needsSync = false;
    }

    /// <summary>
    /// Attempts to move by a delta, considering wall and enemy collision.
    /// </summary>
    public void Move(FixedVector2 desiredDelta)
    {
        Vector2 delta = desiredDelta.ToVector2() / 60f;
        if (delta.sqrMagnitude <= 0f)
            return;

        // 1️⃣ Remove wall collision normals
        delta = RemoveNormalComponent(delta, Policy.wallsMask);

        // 2️⃣ Remove enemy collision normals if treated as blockers
        if (Policy.enemyAsBlocker)
            delta = RemoveNormalComponent(delta, Policy.enemyMask);

        // 3️⃣ Apply movement
        Vector2 target = _rb.position + delta;
        _rb.MovePosition(target);
        _pos = new FixedVector2(target);
        _needsSync = true;
    }

    private Vector2 RemoveNormalComponent(Vector2 vector, LayerMask mask, bool treatedAsBlocker = true)
    {
        Vector2 vFinal = vector;
        float magnitude = vFinal.magnitude;
        if (magnitude <= 0f)
            return Vector2.zero;

        Vector2 origin = _rb.position;
        Vector2 direction = vFinal.normalized;
        float skinRadius = (Policy.unitRadius + Policy.unitSkin) / (float)FixedVector2.UnitsPerFloat;
        float distance = vector.magnitude;

        var hits = Physics2D.CircleCastAll(origin, skinRadius, direction, distance, mask);
        foreach (var hit in hits)
        {
            if (!hit.collider) continue;
            if (mask == Policy.enemyMask && !Policy.enemyAsBlocker) continue;

            Vector2 n = hit.normal.normalized;
            float dot = Vector2.Dot(vFinal, n);
            if (Mathf.Abs(dot) > 0f && treatedAsBlocker)
                vFinal -= dot * n;
        }

        return vFinal;
    }

    /// <summary>
    /// Applies depenetration correction when overlapping with blockers.
    /// </summary>
    public void Depenetrate()
    {
        LayerMask blockers = Policy.wallsMask;
        if (Policy.enemyAsBlocker)
            blockers |= Policy.enemyMask;

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
            if (!other) continue;

            var distInfo = _col.Distance(other);
            if (!distInfo.isOverlapped) continue;

            correction += distInfo.normal * distInfo.distance;
        }

        if (correction.sqrMagnitude < 1e-6f)
            return;

        Vector2 newPos = _rb.position + correction;
        _rb.MovePosition(newPos);
        _pos = new FixedVector2(newPos);
        _needsSync = true;
    }
}

#region ===== CollisionPolicy =====
[Serializable]
public struct CollisionPolicy
{
    public LayerMask wallsMask;
    public LayerMask enemyMask;
    public bool enemyAsBlocker;
    public int unitRadius;
    public int unitSkin;
    public bool allowWallSlide;
}
#endregion
