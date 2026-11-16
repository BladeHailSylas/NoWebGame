#if UNITY_EDITOR
using Moves;
using PlayerScripts.Acts;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using UnityEngine;

namespace ExitLab
{
    /// <summary>검증 전용 수신체입니다. 실제 정책 검사는 InteractionFilter를 별도로 통과시킵니다.</summary>
    public sealed class RelativeTargetTestEntity : Entity, IVulnerable, IStackable, IDashable
    {
        public int HitCount, StackCount;
        public DamageData LastDamage;
        public ActBridge Act;
        public new void TakeDamage(DamageData damage) { HitCount++; LastDamage = damage; }
        public new void ApplyStack(StackKey key, ushort tick, int amount = 1, StackMetadata metadata = default) { StackCount += amount; }
        public void AddDashContract(DashContract contract) => Act.AddDashContract(contract);
        public void AddTeleportContract(TeleportContract contract) => Act.AddTeleportContract(contract);
    }
}
#endif
