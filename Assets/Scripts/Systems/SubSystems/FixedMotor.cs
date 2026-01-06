using System;
using Moves;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Systems.SubSystems
{
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
            var delta = desiredDelta.ToVector2() / 60f;
            if (delta.sqrMagnitude <= 0f)
                return;

            // 1️⃣ Remove wall collision normals
            delta = RemoveNormalComponent(delta, Policy.wallsMask);

            // 2️⃣ Remove enemy collision normals if treated as blockers
            if (Policy.enemyAsBlocker)
                delta = RemoveNormalComponent(delta, Policy.enemyMask);

            // 3️⃣ Apply movement
            var target = _rb.position + delta;
            _rb.MovePosition(target);
            _pos = new FixedVector2(target);
            _needsSync = true;
        }
        /// <summary>
        /// An extended version of Move().
        /// </summary>
        /// <param name="desiredDelta"></param>
        public bool TryDash(DashContract contract)
        {
            var delta = ((Vector2)contract.Context.Target.position - _rb.position).normalized * contract.Speed / 60f;
            if (delta.sqrMagnitude <= 0f)
                return true;

            var origin = _rb.position;
            var direction = delta.normalized;
            var distance = delta.magnitude;
            var radius = (Policy.unitRadius + Policy.unitSkin) / (float)FixedVector2.UnitsPerFloat;

            // 1️⃣ CircleCast: 벽 + 적 동시 탐색
            var hits = Physics2D.CircleCastAll(
                origin,
                radius,
                direction,
                distance,
                Policy.wallsMask | Policy.enemyMask
            );

            foreach (var hit in hits)
            {
                if (!hit.collider)
                    continue;

                var layer = hit.collider.gameObject.layer;

                // 2️⃣ 벽 충돌 → 즉시 종료
                if (((1 << layer) & Policy.wallsMask) != 0)
                {
                    return false;
                }

                // 3️⃣ 적 충돌 → 피해 적용
                if (((1 << layer) & Policy.enemyMask) != 0)
                {
                    if (hit.collider.TryGetComponent<Entity>(out var entity))
                    {
                        // 자기 자신 방지 (안전장치)
                        if (entity.transform == _col.transform)
                            continue;
                        foreach (var mechanism in contract.OnHit)
                        {
                            if (mechanism.mechanism is not INewMechanism mech) continue;
                            SkillCommand cmd = new(contract.Context.Caster, new FixedVector2(_rb.position), contract.Context.Mode,
                                mech, mechanism.@params, contract.Context.Damage, null, entity.transform, contract.Context.Var);
                            CommandCollector.Instance.EnqueueCommand(cmd);
                        }
                        // ❗ 지금은 비관통 Dash로 가정
                        return false;
                    }
                }
            }
            // 4️⃣ 실제 이동 적용
            var target = origin + delta;
            _rb.MovePosition(target);
            _pos = new FixedVector2(target);
            _needsSync = true;

            return true;
        }


        private Vector2 RemoveNormalComponent(Vector2 vector, LayerMask mask, bool treatedAsBlocker = true)
        {
            var vFinal = vector;
            var magnitude = vFinal.magnitude;
            if (magnitude <= 0f)
                return Vector2.zero;

            var origin = _rb.position;
            var direction = vFinal.normalized;
            var skinRadius = (Policy.unitRadius + Policy.unitSkin) / (float)FixedVector2.UnitsPerFloat;
            var distance = vector.magnitude;

            var hits = Physics2D.CircleCastAll(origin, skinRadius, direction, distance, mask);
            foreach (var hit in hits)
            {
                if (!hit.collider) continue;
                if (mask == Policy.enemyMask && !Policy.enemyAsBlocker) continue;

                var n = hit.normal.normalized;
                var dot = Vector2.Dot(vFinal, n);
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
            var blockers = Policy.wallsMask;
            if (Policy.enemyAsBlocker)
                blockers |= Policy.enemyMask;

            ContactFilter2D filter = new() { useLayerMask = true, useTriggers = false };
            filter.SetLayerMask(blockers);

            var overlaps = new Collider2D[8];
            var count = _col.Overlap(filter, overlaps);
            if (count == 0)
                return;

            var correction = Vector2.zero;
            for (var i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (!other) continue;

                var distInfo = _col.Distance(other);
                if (!distInfo.isOverlapped) continue;

                correction += distInfo.normal * distInfo.distance;
            }

            if (correction.sqrMagnitude < 1e-6f)
                return;

            var newPos = _rb.position + correction;
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
}