using System;
using System.Collections.Generic;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Anchor;
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
            Policy = new CollisionPolicy
            {
                wallsMask = LayerMask.GetMask("Walls&Obstacles"),
                enemyMask = LayerMask.GetMask("Foe"),
                enemyAsBlocker = true,
                unitRadius = 500,
                unitSkin = 10,
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
            // 벽
            int selfLayer = _col.gameObject.layer;
            delta = RemoveNormalComponent(
                delta,
                Policy.wallsMask,
                selfLayer,
                (target, self) => true // 벽은 항상 blocker
            );
            // 유닛
            if (Policy.enemyAsBlocker)
            {
                delta = RemoveNormalComponent(
                    delta,
                    Policy.enemyMask,
                    selfLayer,
                    AllyEnemyChecker.IsEnemy // ← 여기!
                );
            }
            // 3️⃣ Apply movement
            var target = _rb.position + delta;
            _rb.MovePosition(target);
            _pos = new FixedVector2(target);
            _needsSync = true;
        }
        /// <summary>
        /// An extended version of Move().
        /// </summary>
        public bool TryDash(DashContract contract, HashSet<Entity> dashHits)
        {
            var delta = ((Vector2)contract.Context.Target.position - _rb.position).normalized * contract.Speed / 60f;
            if (delta.sqrMagnitude <= 0f)
                return false;
            var origin = _rb.position;
            var direction = delta.normalized;
            var distance = delta.magnitude;
            var radius = (Policy.unitRadius + Policy.unitSkin) / (float)FixedVector2.UnitsPerFloat;

            // 벽 + 적 탐색
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

                // 벽 충돌 → 즉시 종료
                if (((1 << layer) & Policy.wallsMask) != 0)
                {
                    return false;
                }

                // 적 충돌 → 피해 적용
                if (((1 << layer) & Policy.enemyMask) == 0) continue; //Negate when not enemyMask
                if (!hit.collider.TryGetComponent<Entity>(out var entity) || dashHits.Contains(entity)) continue; //Negate when touched already
                // 자기 자신 방지 (안전장치)
                if (entity.transform == _col.transform)
                    continue;
                var _ctx = contract.Context;
                SkillUtils.ActivateFollowUp(contract.OnHit, _ctx, entity.transform);
                return contract.Penetrative && contract.Context.Target != entity.transform;
                //False when not penetrative since it needs to stop when touches an enemy
                //True only when penetrative and not target; It needs to stop if it hits the target
            }
            // Moving
            var target = origin + delta;
            _rb.MovePosition(target);
            _pos = new FixedVector2(target);
            _needsSync = true;
            return true;
        }


        private Vector2 RemoveNormalComponent(
            Vector2 vector,
            LayerMask detectMask,
            int selfLayer,
            Func<int, int, bool> isBlocker // (targetLayer, selfLayer) => bool
        )
        {
            var vFinal = vector;
            var magnitude = vFinal.magnitude;
            if (magnitude <= 0f)
                return Vector2.zero;

            var origin = _rb.position;
            var direction = vFinal.normalized;
            var skinRadius = (Policy.unitRadius + Policy.unitSkin) / (float)FixedVector2.UnitsPerFloat;
            var distance = vector.magnitude;

            var hits = Physics2D.CircleCastAll(origin, skinRadius, direction, distance, detectMask);

            foreach (var hit in hits)
            {
                if (!hit.collider) continue;

                var targetLayer = hit.collider.gameObject.layer;

                // 🔴 여기서만 차단 여부 판단
                if (!isBlocker(targetLayer, selfLayer))
                    continue;

                var n = hit.normal.normalized;
                var dot = Vector2.Dot(vFinal, n);
                if (Mathf.Abs(dot) > 0f)
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

        private Vector2 ComputeDepenetration(Func<int, int, bool> isBlocker)
        {
            return Vector2.zero;
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