using Moves;
using Moves.Mechanisms;
using UnityEngine;

namespace PlayerScripts.Skills
{
    public class SkillRunner
    {
        private readonly CastPreparation _preparation;
        private readonly IEffectDisplayer _displayer;
        public SkillRunner(TargetResolver resolver)
        {
            _preparation = new CastPreparation(resolver);
        }

        public SkillRunner(TargetResolver resolver, IEffectDisplayer displayer)
        {
            _preparation = new CastPreparation(resolver);
            _displayer = displayer;
        }

        public bool TryPrepareRoot(in SkillCommand cmd, out SkillCommand prepared, out string reason)
            => _preparation.TryPrepareRoot(cmd, out prepared, out reason);

        public void Activate(in SkillCommand cmd) => TryActivate(cmd);
        internal bool TryActivate(in SkillCommand cmd)
        {
            try { return ActivateCore(cmd); }
            catch (System.Exception exception)
            {
                AttackLog.Write(cmd.Trace, "ExecutionException", $"type={exception.GetType().Name} message={exception.Message}");
                throw; // 원래 예외/스택을 유지하며 성공으로 처리하지 않습니다.
            }
        }

        private bool ActivateCore(in SkillCommand cmd)
        {
            if (cmd.Commit != null && cmd.Commit.State != CastCommitState.Executing)
            { AttackLog.Write(cmd.Trace, "ExecutionRejected", "reason=RootNotDispatched"); return false; }
            SkillTarget target;
            TargetMode mode;
            if (cmd.Preparation == CastPreparationState.RootTargetPrepared)
            {
                var validationReason = CastPreparation.ValidatePrepared(cmd);
                if (validationReason != "Allowed")
                { AttackLog.Write(cmd.Trace, "ExecutionRejected", $"reason={validationReason}"); cmd.Commit?.Aborted(validationReason); return false; }
                target = cmd.Target;
                mode = cmd.TargetMode;
            }
            else if (!_preparation.TryResolveDeferred(cmd, out target, out mode, out var reason))
            {
                AttackLog.Write(cmd.Trace, "ExecutionRejected", $"reason={reason}");
                cmd.Commit?.Aborted(reason); return false;
            }
            AttackLog.Write(cmd.Trace, "Execute", $"mechanism={cmd.Mech.GetType().Name} mode={mode} target={target} targetVersion={cmd.Targeting.Version} relations={cmd.Targeting.Relations} legacyQueryLayers={cmd.Mask.value} origin={cmd.Caster.position}");
            // 요청 시점이 아니라 실제 실행 직전에 한 번만 확정합니다. (0, 0)도 유효합니다.
            var origin = cmd.OriginCastPosition ?? (Vector2)cmd.Caster.position;
            var context = new CastContext(cmd.Mech, cmd.Caster, target, cmd.Damage, cmd.Var, mode, cmd.Exit, origin, cmd.Trace.Effect(), cmd.Source, cmd.SwitchSelection);
            if (cmd.Mech is NewMechanism mechanism)
            { if (!mechanism.TryExecute(context)) { cmd.Commit?.Aborted("SpatialPreparationFailed"); return false; } }
            else cmd.Mech.Execute(context);
            AttackLog.Write(cmd.Trace, "ExecuteReturned", "effectCompletion=NotImplied");
            // 효과가 이미 실행된 뒤 표시 실패로 같은 Cast를 재실행하지 않습니다.
            try { _displayer?.Display(cmd); }
            catch (System.Exception error)
            {
                AttackLog.Write(cmd.Trace, "DisplayException", $"type={error.GetType().Name} {cmd.Commit?.Describe()}");
                Debug.LogException(error);
            }
            return true;
        }
    }
}
