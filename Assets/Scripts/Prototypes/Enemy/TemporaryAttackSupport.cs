using System.Collections.Generic;
using Characters;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Skills;

namespace Systems.SubSystems
{
    /// <summary>임시 AI가 처리하지 못하는 재탐색/분기 스킬은 커서로 실행하지 않고 설정 단계에서 거부합니다.</summary>
    public static class TemporaryAttackSupport
    {
        public static bool Validate(SkillBinding binding, out string error)
        {
            if (!binding.skillData || binding.skillData.mechanismData?.mechanism == null)
            { error = "Attack SkillData/Mechanism이 필요합니다."; return false; }
            if (binding.skillData.mechanismData.mode != TargetMode.TowardsEntity)
            { error = "임시 AI Attack은 SkillData의 루트 Mode가 TowardsEntity일 때만 지원합니다."; return false; }
            var root = binding.skillData.mechanismData.mechanism;
            if (!EntityAttackSelection.ValidRange(root.MinRange, root.MaxRange) || root.MaxRange <= 0)
            { error = "Attack 사거리는 0 <= MinRange <= MaxRange이며 MaxRange > 0이어야 합니다."; return false; }
            return Visit(root, new HashSet<INewMechanism>(), out error);
        }

        private static bool Visit(INewMechanism mechanism, HashSet<INewMechanism> path, out string error)
        {
            if (!path.Add(mechanism)) { error = "순환 Chain은 임시 AI에서 지원하지 않습니다."; return false; }
            var children = new List<MechanismData>();
            void Add(IEnumerable<MechanismData> list) { if (list != null) children.AddRange(list); }
            switch (mechanism)
            {
                case DamageMechanism m: Add(m.onHit); Add(m.onExpire); break;
                case StackMechanism m: Add(m.onHit); Add(m.onExpire); break;
                case DummyMechanism m: Add(m.onHit); Add(m.onExpire); break;
                case MeleeMechanism m: Add(m.onHit); Add(m.onExpire); break;
                case HitscanMechanism m: Add(m.onHit); Add(m.onExpire); break;
                case RayMechanism m: Add(m.onHit); break;
                case AreaMechanism m: Add(m.onEnter); Add(m.onExpire); break;
                case ProjectileMechanism m:
                    Add(m.onHit); Add(m.onExpire); Add(m.onCompleted); Add(m.onInterrupted); break;
                default:
                    error = $"{mechanism.GetType().Name}: 임시 AI는 일반 공격 계열만 지원합니다(Switch/Detect/Dash/Teleport/Summon 제외).";
                    return false;
            }
            foreach (var child in children)
            {
                if (child?.mechanism == null) continue;
                if (child.requireRetarget)
                { error = "requireRetarget 자식은 AI 획득 방식 전환 전까지 지원하지 않습니다."; return false; }
                if (!Visit(child.mechanism, path, out error)) return false;
            }
            path.Remove(mechanism);
            error = null;
            return true;
        }
    }
}
