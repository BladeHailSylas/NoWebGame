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
            try { ActivateCore(cmd); }
            catch (System.Exception exception)
            {
                AttackLog.Write(cmd.Trace, "ExecutionException", $"type={exception.GetType().Name} message={exception.Message}");
                throw; // 원래 예외/스택을 유지하며 성공으로 처리하지 않습니다.
            }
        }

        private void ActivateCore(in SkillCommand cmd)
        {
            if (!cmd.Caster || cmd.Mech == null) { AttackLog.Write(cmd.Trace, "ExecutionRejected", "reason=MissingCasterOrMechanism"); return; }
            // AI 요청만 실행 직전에 다시 검사합니다. 실패한 요청을 커서 탐색으로 대체하지 않습니다.
            if (cmd.ExplicitTarget != null)
            {
                var reason = cmd.ExplicitTarget.Validate(cmd.Caster, cmd.Mech);
                if (reason != "Allowed" || !cmd.Target.Matches(cmd.ExplicitTarget.Target))
                { AttackLog.Write(cmd.Trace, "ExecutionRejected", $"reason={(reason != "Allowed" ? reason : "CommandTargetMismatch")} {cmd.ExplicitTarget.Describe(cmd.Mech)}"); return; }
            }
            //Enforce maximum chain depth
            //Determine target
            var target = cmd.Target;
            var mode = cmd.TargetMode;
            if (cmd.Mech is DetectMechanism detect && !target.IsResolved)
            {
                var result = _targetResolver.Detect(cmd.Caster, detect, cmd.Trace);
                target = result.Target;
                mode = detect.requiredMode;
            }
            //TODO: Consider refactoring into: single target acquisition / post-acquisition interpretation
            // Detect의 탐색 실패도 Execute에 전달하여 onNotFound를 실행합니다.
            // 일반 탐색으로 재시도하면 실패 분기가 사라질 수 있습니다.
            if (!target.IsResolved && cmd.Mech is not DetectMechanism)
            {
                var req = new TargetRequest(cmd.Caster, cmd.Mech.MinRange, cmd.Mech.MaxRange, cmd.TargetMode, cmd.Mask, cmd.Trace, cmd.Targeting);
                //Temporarily Foe, should be added further target request
                // TODO: Later support range/mask overrides from skill data.
                var result = _targetResolver.ResolveTarget(req);
                if (!result.Found)
                {
                    AttackLog.Write(cmd.Trace, "ExecutionRejected", "reason=TargetNotFound");
                    return;
                }
                target = result.Target;
                mode = cmd.TargetMode;
            }
            AttackLog.Write(cmd.Trace, "Execute", $"mechanism={cmd.Mech.GetType().Name} mode={mode} target={target} targetVersion={cmd.Targeting.Version} relations={cmd.Targeting.Relations} legacyQueryLayers={cmd.Mask.value} origin={cmd.Caster.position}");
            // 요청 시점이 아니라 실제 실행 직전에 한 번만 확정합니다. (0, 0)도 유효합니다.
            var origin = cmd.OriginCastPosition ?? (Vector2)cmd.Caster.position;
            var context = new CastContext(cmd.Mech, cmd.Caster, target, cmd.Damage, cmd.Var, mode, cmd.Exit, origin, cmd.Trace.Effect(), cmd.Source);
            if (cmd.Mech is NewMechanism mechanism)
            { if (!mechanism.TryExecute(context)) return; }
            else cmd.Mech.Execute(context);
            AttackLog.Write(cmd.Trace, "ExecuteReturned", "effectCompletion=NotImplied");
            _displayer?.Display(cmd);
        }
    }
}
