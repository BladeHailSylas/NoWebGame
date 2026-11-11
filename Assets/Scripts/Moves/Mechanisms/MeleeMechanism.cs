using System;
using System.Collections.Generic;
using Moves.Effects.Definitions;
using UnityEngine;

namespace Moves.Mechanisms
{
    [Serializable]
    public class MeleeMechanism : NewMechanism, INewMechanism
    {
        [Header("Area")]
        [Range(0, 360)] public float angleDeg = 120f;
        public LayerMask enemyMask;
        public MeleeEffectEntity effectPrefab;

        [Header("Callbacks")]
        [SerializeReference] public List<MechanismData> onHit = new();
        [SerializeReference] public List<MechanismData> onExpire = new();

        public new void Execute(CastContext ctx)
        {
            Debug.Log("MeleeMechanism.Execute");
            if (ctx.Mech is not MeleeMechanism mech)
                return;

            var caster = ctx.Caster;
            if (!ctx.Target.TryGetPosition(out var target)) return;

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
            foreach (var hit in hits)
            {
                if (hit.transform == caster)
                    continue;

                var toTarget = ((Vector2)hit.transform.position - origin).normalized;
                if (mech.angleDeg < 360f)
                {
                    var angle = Vector2.Angle(dir, toTarget);
                    if (angle > halfAngle)
                        continue;
                }
                Debug.Log(hit.transform.name);
                SkillUtils.ActivateChain(mech.onHit, ctx, hit.transform);
            }

            SkillUtils.ActivateChain(mech.onExpire, ctx);
        }
    }
}
