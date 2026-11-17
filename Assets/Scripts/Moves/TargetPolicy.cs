using Systems.Data;
using Systems.SubSystems;
using UnityEngine;

namespace Moves
{
    /// <summary>이행 기간에만 별도 인터페이스를 둡니다. 구형/외부 Mechanism은 명시적으로 Legacy입니다.</summary>
    public interface IRelativeTargeting
    {
        TargetPolicy Targeting { get; }
    }

    /// <summary>관계 비트를 물리 LayerMask로 넘기지 않는 요청별 설정입니다. 0은 미변환, 1은 상대 관계입니다.</summary>
    public readonly struct TargetPolicy
    {
        public const int CurrentVersion = 1;
        public readonly int Version;
        public readonly TargetRelationMask Relations;
        public bool IsRelative => Version != 0;
        public bool IsValid => Version == CurrentVersion && TargetRelationMasks.IsValid(Relations);
        // 자식 Collider의 Layer는 소속 Layer와 다를 수 있으므로 물리 후보는 넓게 수집합니다.
        public const int CandidateLayers = Physics2D.AllLayers;
        public static int WallLayers => LayerMask.GetMask("Walls&Obstacles");
        public TargetPolicy(int version, TargetRelationMask relations) { Version = version; Relations = relations; }
        public static TargetPolicy From(INewMechanism mechanism) =>
            mechanism is IRelativeTargeting relative ? relative.Targeting : default;
        public bool Allows(Transform caster, Entity target) => IsValid && EntityIdentity.IsValid(target) &&
            TargetRelationMasks.Allows(Relations, EntityRelationResolver.Resolve(EntityOf(caster), target));
        public static Entity EntityOf(Transform transform) => transform ? transform.GetComponentInParent<Entity>(true) : null;
        public static Entity EntityOf(Collider2D collider) => collider ? collider.GetComponentInParent<Entity>(true) : null;

        /// <summary>이미 선택된 Entity의 직접 효과에만 적용합니다. 좌표/제어 노드를 일괄 차단하지 않습니다.</summary>
        public static bool AllowEffect(CastContext ctx)
        {
            var policy = From(ctx.Mech);
            if (!policy.IsRelative) return true;
            var allowed = ctx.Target.Kind == TargetKind.Entity && policy.Allows(ctx.Caster, ctx.Target.Entity);
            if (!allowed) AttackLog.Write(ctx.Trace, "EffectRejected", $"reason=TargetRelationOrValidity target={ctx.Target} relations={policy.Relations} version={policy.Version}");
            return allowed;
        }
        public static void CopyAffiliation(Transform caster, Entity summon)
        {
            var source = EntityOf(caster);
            if (!summon) return;
            // 정보 없는 소환자를 You로 추측하지 않습니다. Default+Legacy는 Unknown입니다.
            summon.RelationProfile = source ? source.RelationProfile : EntityRelationProfile.LegacyLayer;
            summon.gameObject.layer = source ? source.gameObject.layer : 0;
        }
    }
}
