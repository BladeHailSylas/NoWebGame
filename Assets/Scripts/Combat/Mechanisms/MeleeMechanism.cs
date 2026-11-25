using global::NoWebGame.Combat.Presentation;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace NoWebGame.Combat.Mechanisms
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "MeleeMechanism")]
    [Serializable]
    public class MeleeMechanism : NewMechanism, INewMechanism
    {
        [Header("Area")]
        [Range(0, 360)] public float angleDeg = 120f;
        [InspectorName("Legacy Melee Hit Layers"), Tooltip("Legacy 모드 전용입니다. Relative 모드는 Target Relations를 사용합니다.")] public LayerMask enemyMask;
        public MeleeEffectEntity effectPrefab;

        [Header("Callbacks")]
        [SerializeReference] public List<MechanismData> onHit = new();
        [SerializeReference] public List<MechanismData> onExpire = new();

        protected override void ExecuteCore(CastContext ctx)
        {

            if (ctx.Mech is not MeleeMechanism mech)
                return;

            if (mech.Targeting.IsRelative) { RelativeHitQueries.Melee(ctx, mech); return; }
            var caster = ctx.Caster;
            if (!ctx.TryGetSpatialPosition(out var target)) return;

            Vector2 origin = caster.position;
            Vector2 dir = target - (Vector2)caster.position;
            var radius = mech.MaxRange;
            var halfAngle = mech.angleDeg * 0.5f;
            if (mech.effectPrefab)
            {
                // Init은 Mesh 변경과 소멸을 수행하므로 생성한 인스턴스에만 호출합니다.
                var effect = UnityEngine.Object.Instantiate(mech.effectPrefab, origin, Quaternion.identity);
                effect.Init(origin, radius, mech.angleDeg, dir.normalized);
            }

            var hits = Physics2D.OverlapCircleAll(origin, radius, mech.enemyMask);
            AttackLog.Write(ctx.Trace, "MeleeQuery", $"origin={origin} direction={dir} radius={radius} angle={mech.angleDeg} queryLayers={mech.enemyMask.value} colliders={hits.Length}");
            foreach (var hit in hits)
            {
                if (hit.transform == caster)
                { AttackLog.Hit(ctx, hit, "RejectedCasterTransform"); continue; }

                var toTarget = ((Vector2)hit.transform.position - origin).normalized;
                if (mech.angleDeg < 360f)
                {
                    var angle = Vector2.Angle(dir, toTarget);
                    if (angle > halfAngle)
                    { AttackLog.Hit(ctx, hit, "RejectedAngle"); continue; }
                }
                AttackLog.Hit(ctx, hit, "Accepted");
                SkillUtils.ActivateChain(mech.onHit, ctx, hit.transform, trigger: "onHit");
            }

            SkillUtils.ActivateChain(mech.onExpire, ctx, trigger: "onExpire");
        }
    }
}
