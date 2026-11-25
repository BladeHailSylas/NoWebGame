using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;




namespace NoWebGame.Combat.Casting
{
    public enum CastPreparationState
    {
        // 외부 직접 명령과 Chain은 기존 실행 직전 탐색 계약을 사용합니다.
        Deferred = 0,
        RootTargetPrepared = 1
    }

    /// <summary>
    /// Target 선택만 담당합니다. 비용, Delay 등록, Origin 및 Destination 준비는 수행하지 않습니다.
    /// PrepareCast(Hold 준비)가 아니라 Attacker가 실제 공격 입력을 처리할 때 사용합니다.
    /// </summary>
    public sealed class CastPreparation
    {
        private readonly TargetResolver _resolver;
        public CastPreparation(TargetResolver resolver) { _resolver = resolver; }

        public bool TryPrepareRoot(in SkillCommand request, out SkillCommand prepared, out string reason)
        {
            prepared = request;
            if (request.Preparation == CastPreparationState.RootTargetPrepared)
            { reason = "AlreadyPrepared"; return true; }
            if (!TryResolveDeferred(request, out var target, out var mode, out reason)) return false;
            prepared = request.WithPreparedTarget(target, mode);
            AttackLog.Write(prepared.Trace, "TargetPrepared", $"target={target} mode={mode} origin=Deferred commit=NotPerformed");
            return true;
        }

        internal bool TryResolveDeferred(in SkillCommand cmd, out SkillTarget target, out TargetMode mode, out string reason)
        {
            target = cmd.Target; mode = cmd.TargetMode; reason = "Allowed";
            if (!cmd.Caster || cmd.Mech == null) { reason = "MissingCasterOrMechanism"; return false; }
            if (cmd.ExplicitTarget != null)
            {
                reason = cmd.ExplicitTarget.Validate(cmd.Caster, cmd.Mech);
                AttackLog.Write(cmd.Trace, "TargetValidation", $"result={reason} {cmd.ExplicitTarget.Describe(cmd.Mech)}");
                if (reason != "Allowed") return false;
                if (!target.Matches(cmd.ExplicitTarget.Target)) { reason = "CommandTargetMismatch"; return false; }
            }
            if (target.IsResolved) return true;
            if (!_resolver) { reason = "MissingTargetResolver"; return false; }
            if (cmd.Mech is DetectMechanism detect)
            {
                target = _resolver.Detect(cmd.Caster, detect, cmd.Trace).Target;
                mode = detect.requiredMode;
                // 미발견을 발동 거부로 바꾸지 않습니다. 실행 시 onNotFound로 전달합니다.
                return true;
            }
            var request = new TargetRequest(cmd.Caster, cmd.Mech.MinRange, cmd.Mech.MaxRange,
                mode, cmd.Mask, cmd.Trace, cmd.Targeting);
            var result = _resolver.ResolveTarget(request);
            if (!result.Found) { reason = "TargetNotFound"; return false; }
            target = result.Target;
            return true;
        }

        internal static string ValidatePrepared(in SkillCommand cmd)
        {
            if (!cmd.Caster || cmd.Mech == null) return "MissingCasterOrMechanism";
            if (cmd.ExplicitTarget != null)
            {
                var reason = cmd.ExplicitTarget.ValidateExecution(cmd.Caster, cmd.Mech);
                if (reason != "Allowed") return reason;
                if (!cmd.Target.Matches(cmd.ExplicitTarget.Target)) return "CommandTargetMismatch";
            }
            // Detect는 무효 Entity와 미발견도 실패 분기로 처리하는 제어 노드입니다.
            if (cmd.Mech is DetectMechanism) return "Allowed";
            if (!cmd.Target.IsValid) return "PreparedTargetLost";
            if (cmd.Target.Kind == TargetKind.Entity && cmd.Targeting.IsRelative &&
                !cmd.Targeting.Allows(cmd.Caster, cmd.Target.Entity)) return "PreparedTargetRelationMismatch";
            // 선택 사거리는 다시 검사하지 않습니다. 실제 공격/이동 제한은 Mechanism이 유지합니다.
            return "Allowed";
        }
    }
}
