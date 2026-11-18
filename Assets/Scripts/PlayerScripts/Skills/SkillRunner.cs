using Moves;
using Moves.Mechanisms;
using UnityEngine;

namespace PlayerScripts.Skills
{
    public class SkillRunner
    {
        private readonly CastPreparation _preparation;
        private readonly EffectDisplayer _displayer;
        public SkillRunner(TargetResolver resolver)
        {
            _preparation = new CastPreparation(resolver);
        }

        public SkillRunner(TargetResolver resolver, EffectDisplayer displayer)
        {
            _preparation = new CastPreparation(resolver);
            _displayer = displayer;
        }

        public bool TryPrepareRoot(in SkillCommand cmd, out SkillCommand prepared, out string reason)
            => _preparation.TryPrepareRoot(cmd, out prepared, out reason);

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
            if (cmd.Commit != null && cmd.Commit.State != CastCommitState.Executing)
            { AttackLog.Write(cmd.Trace, "ExecutionRejected", "reason=RootNotDispatched"); return; }
            SkillTarget target;
            TargetMode mode;
            if (cmd.Preparation == CastPreparationState.RootTargetPrepared)
            {
                var validationReason = CastPreparation.ValidatePrepared(cmd);
                if (validationReason != "Allowed")
                { AttackLog.Write(cmd.Trace, "ExecutionRejected", $"reason={validationReason}"); return; }
                target = cmd.Target;
                mode = cmd.TargetMode;
            }
            else if (!_preparation.TryResolveDeferred(cmd, out target, out mode, out var reason))
            {
                AttackLog.Write(cmd.Trace, "ExecutionRejected", $"reason={reason}");
                return;
            }
            AttackLog.Write(cmd.Trace, "Execute", $"mechanism={cmd.Mech.GetType().Name} mode={mode} target={target} targetVersion={cmd.Targeting.Version} relations={cmd.Targeting.Relations} legacyQueryLayers={cmd.Mask.value} origin={cmd.Caster.position}");
            // 요청 시점이 아니라 실제 실행 직전에 한 번만 확정합니다. (0, 0)도 유효합니다.
            var origin = cmd.OriginCastPosition ?? (Vector2)cmd.Caster.position;
            var context = new CastContext(cmd.Mech, cmd.Caster, target, cmd.Damage, cmd.Var, mode, cmd.Exit, origin, cmd.Trace.Effect(), cmd.Source, cmd.SwitchSelection);
            if (cmd.Mech is NewMechanism mechanism)
            { if (!mechanism.TryExecute(context)) return; }
            else cmd.Mech.Execute(context);
            AttackLog.Write(cmd.Trace, "ExecuteReturned", "effectCompletion=NotImplied");
            _displayer?.Display(cmd);
        }
    }
}
