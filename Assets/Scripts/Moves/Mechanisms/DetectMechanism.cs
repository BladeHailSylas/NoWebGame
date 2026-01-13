using System;
using JetBrains.Annotations;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace Moves.Mechanisms
{
    [CreateAssetMenu(
        fileName = "DetectMechanism",
        menuName = "Skills/Mechanisms/Detect")]
    public class DetectMechanism : ScriptableObject, INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            if (ctx.Params is not DetectParams param)
                return;

            // Detect는 "대상이 있을 때만" 의미를 가짐
            if (ctx.Target is null)
                return;

            // 1. TargetMode 검사
            var detected = ctx.Mode == param.requiredMode;
            switch (detected)
            {
                case false:
                    Debug.Log($"Nah {ctx.Mode} is not {param.requiredMode}");
                    break;
                // 2. Component 검사 (선택)
                case true when param.requiredComponent is not null:
                {
                    Debug.Log("Detecting");
                    if (!ctx.Target.TryGetComponent(
                            param.requiredComponent.GetType(),
                            out _))
                    {
                        Debug.LogWarning("Nah");
                        detected = false;
                    }

                    break;
                }
            }

            // 3. 실행할 FollowUps 선택
            var followUps = detected
                ? param.onDetected
                : param.onNotFound;

            if (followUps == null || followUps.Length == 0)
                return;

            // 4. Switch와 동일한 방식으로 FollowUp 실행
            SkillUtils.ActivateFollowUp(followUps, ctx);
        }

    }
    [Serializable]
    public class DetectParams : NewParams
    {
        public TargetMode requiredMode;
        [CanBeNull] public MonoBehaviour requiredComponent;
        public MechanismRef[] onDetected;
        public MechanismRef[] onNotFound;
    }
}