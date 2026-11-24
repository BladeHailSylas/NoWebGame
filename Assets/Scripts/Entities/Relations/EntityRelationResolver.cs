using Systems.Data;
using UnityEngine;

namespace Systems.SubSystems
{
    /// <summary>
    /// Caster를 기준으로 Entity 사이의 관계를 판정합니다.
    /// targetable·활성 상태·거리·소유권은 대상 선택/효과 단계의 별도 조건입니다.
    /// 호출자는 Chain의 Caster를 전달하며 Owner를 따라 시전자를 바꾸지 않습니다.
    /// </summary>
    public static class EntityRelationResolver
    {
        public static EntityRelation Resolve(Entity caster, Entity target)
        {
            // Unity에서 이미 파괴된 객체도 정보 부족으로 처리합니다.
            if (!caster || !target) return EntityRelation.Unknown;
            // 동일한 Entity임이 확실하면 소속 정보가 없어도 Self입니다.
            if (caster == target) return EntityRelation.Self;

            if (!HasRelationInformation(caster) || !HasRelationInformation(target))
                return EntityRelation.Unknown;

            // 양쪽 정보가 정상일 때만 명시 프로필을 해석합니다.
            // Neutral과 Unrelated 조합은 Neutral이 우선이며 양방향 결과가 같습니다.
            if (caster.RelationProfile == EntityRelationProfile.ExplicitNeutral ||
                target.RelationProfile == EntityRelationProfile.ExplicitNeutral)
                return EntityRelation.Neutral;
            if (caster.RelationProfile == EntityRelationProfile.ExplicitUnrelated ||
                target.RelationProfile == EntityRelationProfile.ExplicitUnrelated)
                return EntityRelation.Unrelated;

            // 초기 소속 정보 공급자. 기존 Checker의 API와 의미는 유지합니다.
            var sourceLayer = caster.gameObject.layer;
            var targetLayer = target.gameObject.layer;
            if (AllyEnemyChecker.IsAlly(targetLayer, sourceLayer)) return EntityRelation.Ally;
            if (AllyEnemyChecker.IsEnemy(targetLayer, sourceLayer)) return EntityRelation.Enemy;
            return EntityRelation.Unrelated;
        }

        /// <summary>자식 Transform은 가장 가까운 부모 Entity로 정규화합니다. Owner는 조회하지 않습니다.</summary>
        public static EntityRelation ResolveFromTransforms(Transform caster, Transform target) =>
            Resolve(caster ? caster.GetComponentInParent<Entity>(true) : null,
                target ? target.GetComponentInParent<Entity>(true) : null);

        /// <summary>물리 후보의 Collider에서 Entity를 찾는 경계 API입니다.</summary>
        public static EntityRelation ResolveFromCollider(Entity caster, Collider2D target) =>
            Resolve(caster, target ? target.GetComponentInParent<Entity>(true) : null);

        private static bool HasRelationInformation(Entity entity)
        {
            switch (entity.RelationProfile)
            {
                case EntityRelationProfile.ExplicitNeutral:
                case EntityRelationProfile.ExplicitUnrelated:
                    return true;
                case EntityRelationProfile.LegacyLayer:
                    var layer = entity.gameObject.layer;
                    return IsNamedLayer(layer, "You") || IsNamedLayer(layer, "Ally") ||
                        IsNamedLayer(layer, "Foe");
                default:
                    return false;
            }
        }

        private static bool IsNamedLayer(int layer, string name)
        {
            var namedLayer = LayerMask.NameToLayer(name);
            return namedLayer >= 0 && layer == namedLayer;
        }
    }
}
