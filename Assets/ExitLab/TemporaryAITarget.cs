using Moves;
using Systems.Data;
using UnityEngine;

namespace ExitLab
{
    /// <summary>실제 DamageMechanism 호출을 세는 검증 표적입니다. 일반 게임의 피해 필터를 대체하지 않습니다.</summary>
    public sealed class TemporaryAITarget : Entity, IVulnerable
    {
        public int HitCount { get; private set; }
        public Transform LastAttacker { get; private set; }
        public new void TakeDamage(DamageData damage)
        {
            HitCount++;
            LastAttacker = damage.Attacker;
            Debug.Log($"[TemporaryAI Hit] target={name}, caster={LastAttacker?.name}, hits={HitCount}");
        }
    }
}
