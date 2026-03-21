using System.Collections.Generic;
using Moves.ObjectEntity;
using PlayerScripts.Skills;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(fileName = "SummonMechanism", menuName = "Skills/Mechanisms/Summon")]
    public class SummonMechanism : NewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")]
        public SummonEntity summonPrefab;
        public List<SkillData> onSummoned;

        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not SummonMechanism mech)
            {
                return;
            }

            var centerPos = ctx.Target?.position ?? ctx.Caster.position;
            var go = Instantiate(mech.summonPrefab, centerPos, Quaternion.identity);
            var dir = ctx.Target is not null
                ? (ctx.Target.position - ctx.Caster.position).normalized
                : ctx.Caster.right;
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
            if (!go.TryGetComponent<SummonEntity>(out var entity)) return;
            entity.Init(ctx);
            SkillUtils.ActivateFollowUp(mech.onSummoned, ctx);
        }
    }
}
