using Moves;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Stacks.Instances;
using Systems.SubSystems;
using UnityEngine;

namespace PlayerScripts.Core
{
    public class InteractionFilter
    {
        private readonly Entity _entity;
        private readonly StatsBridge _stats;
        private readonly StackManager _stack;
        private readonly VariableStorage _storage;

        public InteractionFilter(Entity entity, StatsBridge stats, StackManager stack)
        {
            _entity = entity;
            _stats = stats;
            _stack = stack;
            _storage = _stack.Storage;
        }

        public void FilterDamage(in DamageData dmg)
        {
            if (dmg.UseRelationPolicy)
            {
                FilterRelativeDamage(dmg);
                return;
            }
            // 1. 공격자 관계 계산
            var relation = FindRelation(dmg.Attacker);
            // 2. 허용 여부 판단
            var allowed = IsDamageAllowed(relation);
            var policy = relation == RelationType.Enemy ? "AllowEnemyDamage" : relation == RelationType.Owner ? "AllowOwnerDamage" : relation == RelationType.Neutral ? "AllowNeutralDamage" : "BuiltInRule";
            AttackLog.Write(dmg.Trace, "DamagePolicy", $"target={AttackLog.Identity(_entity)} damagePolicyRelation={relation} policy={policy} allowed={allowed}");
            if (!allowed)
            {
                AttackLog.Write(dmg.Trace, "DamageRejected", "reason=DamagePolicyDenied");
                return;
            }

            // 3. 허용된 경우에만 실제 피해 처리
            _stats.TakeDamage(dmg);
        }

        private void FilterRelativeDamage(in DamageData dmg)
        {
            var source = TargetPolicy.EntityOf(dmg.Attacker);
            var relation = EntityRelationResolver.Resolve(source, _entity);
            // Owner는 관계 enum이 아니라 수신 정책입니다. Self가 먼저입니다.
            var owner = relation != EntityRelation.Self && source && _entity.Owner &&
                source == TargetPolicy.EntityOf(_entity.Owner);
            var environmental = !source;
            var policy = relation == EntityRelation.Self ? "Self" : owner ? "AllowOwnerDamage" :
                environmental || relation == EntityRelation.Neutral ? "AllowNeutralDamage" :
                relation == EntityRelation.Enemy ? "AllowEnemyDamage" : "Denied";
            var allowed = policy == "Self" || (policy != "Denied" && _storage.HasNameOf(policy));
            AttackLog.Write(dmg.Trace, "DamagePolicy", $"target={AttackLog.Identity(_entity)} damagePolicyRelation={relation} owner={owner} environmentalCompatibility={environmental} policy={policy} allowed={allowed}");
            if (allowed) _stats.TakeDamage(dmg);
            else AttackLog.Write(dmg.Trace, "DamageRejected", "reason=DamagePolicyDenied");
        }
        private bool IsDamageAllowed(RelationType relation)
        {
            bool hasVariable;
            switch (relation)
            {
                case RelationType.Enemy:
                {
                    hasVariable = _storage.HasNameOf("AllowEnemyDamage");
                    break;
                }
                case RelationType.Owner:
                {
                    hasVariable = _storage.HasNameOf("AllowOwnerDamage");
                    break;
                }
                case RelationType.Neutral:
                {
                    hasVariable = _storage.HasNameOf("AllowNeutralDamage");
                    break;
                }
                case RelationType.Ally: return false;
                case RelationType.Self: return true;
                default: return false;
            }
            return hasVariable;
        }

        private RelationType FindRelation(Transform source)
        {
            if (source is null)
            {
                return RelationType.Neutral;
            }
            //Debug.Log($"source is {source.name}({source.gameObject.layer}) and you are {_entity.name}({_entity.gameObject.layer})");
            var baseLayer = _entity.gameObject.layer;
            // 1. 자기 자신
            if (source == _entity.transform)
                return RelationType.Self;

            // 2. Entity가 아닌 경우 (환경, 투사체 등)
            if (!source.TryGetComponent<Entity>(out var sourceEntity))
            {
                return RelationType.Neutral;
            }

            // 3. Owner 관계 (Summon → Owner)
            if (_entity.Owner is not null && sourceEntity.transform == _entity.Owner)
            {
                return RelationType.Owner;
            }

            // 4. Layer 기반 아군 / 적 판정
            // (정책이 아니라 '사실 계산'이므로 여기서 사용해도 됨)
            if (AllyEnemyChecker.IsAlly(sourceEntity.gameObject.layer, baseLayer))
                return RelationType.Ally;

            if (AllyEnemyChecker.IsEnemy(sourceEntity.gameObject.layer, baseLayer))
                return RelationType.Enemy;

            return RelationType.Neutral;
        }
        private enum RelationType
        {
            Self,
            Owner,
            Ally,
            Enemy,
            Neutral
        }
    }
}
