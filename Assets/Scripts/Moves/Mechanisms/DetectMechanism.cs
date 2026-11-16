#nullable enable
using System;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class DetectMechanism : NewMechanism, INewMechanism
    {
        public TargetMode requiredMode;
        public MonoBehaviour? requiredComponent;
        [SerializeReference] public MechanismData[] onDetected;
        [SerializeReference] public MechanismData[] onNotFound;
        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not DetectMechanism mech)
                return;

            // 무대상·무효 Target도 실패 분기로 전달하고 기존 Target은 보존합니다.
            var detected = ctx.Target.IsValid && ctx.Mode == mech.requiredMode &&
                (!mech.Targeting.IsRelative || ctx.Target.Kind != TargetKind.Entity || mech.Targeting.Allows(ctx.Caster, ctx.Target.Entity));
            switch (detected)
            {
                case false:
                    //Debug.Log($"Nah {ctx.Mode} is not {param.requiredMode}");
                    break;
                // 2. Component 검사 (선택)
                case true when mech.requiredComponent is not null:
                {
                    //Debug.Log("Detecting");
                    if (!ctx.Target.TryGetComponent(
                            mech.requiredComponent.GetType(),
                            out _))
                    {
                        //Debug.LogWarning("Nah");
                        detected = false;
                    }

                    break;
                }
            }

            // 3. 실행할 FollowUps 선택
            var followUps = detected
                ? mech.onDetected
                : mech.onNotFound;

            if (followUps == null || followUps.Length == 0)
                return;

            // 4. Switch와 동일한 방식으로 FollowUp 실행
            SkillUtils.ActivateChain(followUps, ctx, trigger: "followUps");
        }

    }
}
