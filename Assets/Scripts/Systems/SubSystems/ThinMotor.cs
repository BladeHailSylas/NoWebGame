using UnityEngine;
using Systems.Data;

namespace Systems.SubSystems
{
    /// <summary>
    /// Lightweight deterministic movement helper.
    /// Detects wall / enemy collision but does not resolve behavior.
    /// Decision-making is responsibility of the owner (Projectile, Area, etc.).
    /// </summary>
    public sealed class ThinMotor
    {
        private readonly Rigidbody2D _rb;
        private readonly Collider2D _col;
        private FixedVector2 _pos;

        public LayerMask wallMask;
        public LayerMask enemyMask;

        public ThinMotor(Rigidbody2D rb, Collider2D col)
        {
            _rb = rb ?? throw new System.ArgumentNullException(nameof(rb));
            _col = col ?? throw new System.ArgumentNullException(nameof(col));

            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;

            _pos = new FixedVector2(rb.position);
        }

        /// <summary>
        /// Attempts to move by desired delta.
        /// Returns false if movement was blocked.
        /// </summary>
        public bool TryMove(
            FixedVector2 desiredDelta,
            bool penetrative,
            out ThinHitInfo hitInfo)
        {
            hitInfo = default;

            var delta = desiredDelta.ToVector2() / 60f;
            if (delta.sqrMagnitude <= 0f)
                return true;

            var origin = _rb.position;
            var direction = delta.normalized;
            var distance = delta.magnitude;
            var radius = GetCastRadius();

            // 1️⃣ Wall check (always blocks)
            var wallHit = Physics2D.CircleCast(
                origin,
                radius,
                direction,
                distance,
                wallMask
            );

            if (wallHit.collider)
            {
                hitInfo = ThinHitInfo.Wall(wallHit);
                return false;
            }

            // 2️⃣ Enemy check
            var enemyHit = Physics2D.CircleCast(
                origin,
                radius,
                direction,
                distance,
                enemyMask
            );

            if (enemyHit.collider)
            {
                hitInfo = ThinHitInfo.Enemy(enemyHit);
                if (!penetrative)
                    return false;
            }

            // 3️⃣ Apply movement
            var target = origin + delta;
            _rb.MovePosition(target);
            _pos = new FixedVector2(target);

            return true;
        }

        private float GetCastRadius()
        {
            // 최소한의 안전 반경만 사용
            var bounds = _col.bounds;
            return Mathf.Min(bounds.extents.x, bounds.extents.y);
        }
    }

    /// <summary>
    /// Minimal hit information reported by ThinFixedMotor.
    /// </summary>
    public readonly struct ThinHitInfo
    {
        public readonly HitType type;
        public readonly Collider2D collider;
        public readonly Vector2 point;
        public readonly Vector2 normal;

        private ThinHitInfo(
            HitType type,
            Collider2D collider,
            Vector2 point,
            Vector2 normal)
        {
            this.type = type;
            this.collider = collider;
            this.point = point;
            this.normal = normal;
        }

        public static ThinHitInfo Wall(RaycastHit2D hit)
            => new(HitType.Wall, hit.collider, hit.point, hit.normal);

        public static ThinHitInfo Enemy(RaycastHit2D hit)
            => new(HitType.Enemy, hit.collider, hit.point, hit.normal);
    }

    public enum HitType
    {
        None,
        Wall,
        Enemy
    }
}
