using global::NoWebGame.Entities.Summon;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Casting;
using System.Collections.Generic;


using UnityEngine;

namespace NoWebGame.Combat.Mechanisms
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.Mechanisms", "Assembly-CSharp", "SummonMechanism")]
    [System.Serializable]
    public class SummonMechanism : NewMechanism, INewMechanism
    {
        [Header("Time")]
        public ushort lifeTick;

        [Header("Settings")]
        [SerializeReference] public SummonEntity summonPrefab;
        [SerializeReference] public List<MechanismData> onSummoned;

        protected override void ExecuteCore(CastContext ctx)
        {
            if (ctx.Mech is not SummonMechanism mech)
            {
                return;
            }
            var prefab = mech.summonPrefab;

            // Entity의 현재 위치와 고정 Position을 같은 계약으로 조회합니다.
            var hasTargetPosition = ctx.TryGetSpatialPosition(out var targetPosition);
            Vector3 centerPosition = hasTargetPosition ? (Vector3)targetPosition : ctx.Caster.position;
            Vector3 direction = hasTargetPosition
                ? (Vector3)(targetPosition - (Vector2)ctx.Caster.position)
                : ctx.Caster.right;
            // 같은 위치를 대상으로 삼아도 유효한 회전을 사용합니다.
            if (direction.sqrMagnitude < 0.000001f) direction = ctx.Caster.right;

            var instance = Object.Instantiate(
                prefab,
                centerPosition,
                Quaternion.LookRotation(Vector3.forward, direction.normalized)
            );

            instance.Init(ctx);
            // 기존 계약대로 소환체가 아닌 원래 Target을 후속 Chain에 전달합니다.
            SkillUtils.ActivateChain(mech.onSummoned, ctx, trigger: "onSummoned");
        }
    }
}
