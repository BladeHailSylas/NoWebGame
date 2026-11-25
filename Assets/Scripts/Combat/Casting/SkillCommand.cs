using global::NoWebGame.Moves;

using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Data;
using System;


using UnityEngine;

namespace NoWebGame.Combat.Casting
{
    public readonly struct SkillCommand : IEquatable<SkillCommand>
    {
        public readonly CastCommit Commit;
        public readonly SwitchSelection SwitchSelection;
        public readonly AttackTrace Trace;
        public readonly string Source;
        public readonly Transform Caster;
        public readonly SkillTarget Target;
        public readonly TargetMode TargetMode;
        public readonly FixedVector2 CastPosition;
        // 최초 실제 실행에서 확정되며, 같은 Chain의 후속 Command는 이 값을 상속합니다.
        public readonly Vector2? OriginCastPosition;
        public readonly INewMechanism Mech;
        public readonly DamageData Damage;
        public readonly LayerMask Mask;
        public readonly TargetPolicy Targeting;
        // 임시 AI의 명시적 루트 공격에만 사용합니다. 기존 SkillData/LayerMask는 변경하지 않습니다.
        public readonly ExplicitAttackTarget ExplicitTarget;
        public readonly SwitchVariable Var;
        public readonly TravelResult? Exit;

        public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition,
            INewMechanism mech, DamageData damage, SkillTarget target = default,
            SwitchVariable va = default, int masker = 0, TravelResult? exit = null,
            Vector2? originCastPosition = null, ExplicitAttackTarget explicitTarget = null, AttackTrace trace = default, string source = null)
        {
            Preparation = CastPreparationState.Deferred;
            Commit = null; SwitchSelection = null;
            Source = source ?? "ExternalCommand";
            Caster = caster;
            Target = target;
            TargetMode = mode;
            CastPosition = castPosition;
            OriginCastPosition = originCastPosition;
            Trace = trace;
            Mech = mech;
            Damage = damage;
            Var = va;
            // 호출자는 레이어 번호가 아니라 LayerMask 비트 집합을 전달합니다.
            Mask = masker;
            Targeting = TargetPolicy.From(mech);
            Exit = exit;
            ExplicitTarget = explicitTarget;
            Trace = trace.CommandId != 0 ? trace : (trace.AttackId != 0 ? trace : AttackTrace.Root(caster)).Command();
        }

        // 준비 완료는 비용 Commit과 다릅니다. Detect의 미발견도 준비된 결과일 수 있습니다.
        public readonly CastPreparationState Preparation;
        internal SkillCommand WithPreparedTarget(SkillTarget target, TargetMode mode) => new(this, target, mode);

        private SkillCommand(in SkillCommand request, SkillTarget target, TargetMode mode)
        {
            Commit = request.Commit; SwitchSelection = request.SwitchSelection;
            Trace = request.Trace; Source = request.Source; Caster = request.Caster;
            Target = target; TargetMode = mode; CastPosition = request.CastPosition;
            OriginCastPosition = request.OriginCastPosition; Mech = request.Mech; Damage = request.Damage;
            Mask = request.Mask; Targeting = request.Targeting; ExplicitTarget = request.ExplicitTarget;
            Var = request.Var; Exit = request.Exit; Preparation = CastPreparationState.RootTargetPrepared;
        }

        private SkillCommand(in SkillCommand request, CastCommit commit, SwitchSelection selection)
        {
            this = request; Commit = commit; SwitchSelection = selection;
        }
        internal SkillCommand WithCommit(CastCommit commit) => new(this, commit, SwitchSelection);
        internal SkillCommand WithSwitchSelection(SwitchSelection selection) => new(this, Commit, selection);

        public bool Equals(SkillCommand other)
        {
            return Caster == other.Caster && Target.Equals(other.Target) && Equals(Mech, other.Mech) &&
                ReferenceEquals(ExplicitTarget, other.ExplicitTarget) && Preparation == other.Preparation;
        }

        public override bool Equals(object obj)
        {
            return obj is SkillCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Caster, Target, Mech, ExplicitTarget, Preparation);
        }
    }

}
