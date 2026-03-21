#nullable enable
using System;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    public class DetectMechanism : NewMechanism
    {
        public TargetMode requiredMode;
        public MonoBehaviour? requiredComponent;
        public MechanismData[] onDetected;
        public MechanismData[] onNotFound;
        public new void Execute(CastContext ctx)
        {
            if (ctx.Mech is not DetectMechanism mech)
                return;

            // Detect는 "대상이 있을 때만" 의미를 가짐
            if (ctx.Target is null)
                return;

            // 1. TargetMode 검사
            var detected = ctx.Mode == mech.requiredMode;
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

            if (followUps.Length == 0)
                return;

            // 4. Switch와 동일한 방식으로 FollowUp 실행
            SkillUtils.ActivateFollowUp(followUps, ctx);
        }

    }
}