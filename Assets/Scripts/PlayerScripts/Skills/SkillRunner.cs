using Moves;
using Moves.Mechanisms;
using UnityEngine;

namespace PlayerScripts.Skills
{
    public class SkillRunner
    {
        private readonly TargetResolver _targetResolver;
        private readonly EffectDisplayer _displayer;
        public SkillRunner(TargetResolver resolver)
        {
            _targetResolver = resolver;
        }

        public SkillRunner(TargetResolver resolver, EffectDisplayer displayer)
        {
            _targetResolver = resolver;
            _displayer = displayer;
        }

        public void Activate(in SkillCommand cmd)
        {
            if (!cmd.Caster || cmd.Mech == null) return;
            //Enforce maximum chain depth
            //Determine target
            var target = cmd.Target;
            var mode = cmd.TargetMode;
            if (cmd.Mech is DetectMechanism detect && !target.IsResolved)
            {
                var result = _targetResolver.Detect(cmd.Caster, detect);
                target = result.Target;
                mode = detect.requiredMode;
            }
            //TODO: Consider refactoring into: single target acquisition / post-acquisition interpretation
            // Detect의 탐색 실패도 Execute에 전달하여 onNotFound를 실행합니다.
            // 일반 탐색으로 재시도하면 실패 분기가 사라질 수 있습니다.
            if (!target.IsResolved && cmd.Mech is not DetectMechanism)
            {
                var req = new TargetRequest(cmd.Caster, cmd.Mech.MinRange, cmd.Mech.MaxRange, cmd.TargetMode, cmd.Mask);
                //Temporarily Foe, should be added further target request
                // TODO: Later support range/mask overrides from skill data.
                var result = _targetResolver.ResolveTarget(req);
                if (!result.Found)
                {
                    // No target found — silently return (placeholder behavior)
                    return;
                }
                target = result.Target;
                mode = cmd.TargetMode;
            }
            Debug.Log(cmd.Mech.GetType());
            // 요청 시점이 아니라 실제 실행 직전에 한 번만 확정합니다. (0, 0)도 유효합니다.
            var origin = cmd.OriginCastPosition ?? (Vector2)cmd.Caster.position;
            cmd.Mech.Execute(new CastContext(cmd.Mech, cmd.Caster, target, cmd.Damage, cmd.Var, mode, cmd.Exit, origin));
            _displayer?.Display(cmd);
        }
    }
}
