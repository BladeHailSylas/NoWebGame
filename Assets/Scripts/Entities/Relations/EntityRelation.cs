using System;

namespace Systems.Data
{
    /// <summary>두 Entity 사이의 단일 판정 결과. Mask의 비트값과 직접 변환하지 않습니다.</summary>
    public enum EntityRelation
    {
        Unknown = 0,
        Self = 1,
        Ally = 2,
        Enemy = 3,
        Neutral = 4,
        Unrelated = 5
    }

    /// <summary>허용할 관계의 조합. None은 아무 관계도 허용하지 않으며 Unknown 비트는 없습니다.</summary>
    [Flags]
    public enum TargetRelationMask
    {
        None = 0,
        Self = 1 << 0,
        Ally = 1 << 1,
        Enemy = 1 << 2,
        Neutral = 1 << 3,
        Unrelated = 1 << 4
    }

    /// <summary>초기 관계 정보의 출처. 기본값 0은 기존 Layer 기반 콘텐츠를 보존합니다.</summary>
    public enum EntityRelationProfile
    {
        LegacyLayer = 0,
        ExplicitNeutral = 1,
        ExplicitUnrelated = 2
    }

    public static class TargetRelationMasks
    {
        private const TargetRelationMask KnownBits = TargetRelationMask.Self | TargetRelationMask.Ally |
            TargetRelationMask.Enemy | TargetRelationMask.Neutral | TargetRelationMask.Unrelated;

        /// <summary>직렬화·입력 경계에서 미정의 비트(예: -1)를 검출할 때 사용합니다.</summary>
        public static bool IsValid(TargetRelationMask mask) => (mask & ~KnownBits) == 0;

        /// <summary>Unknown·미정의 관계·잘못된 Mask는 항상 거부합니다.</summary>
        public static bool Allows(TargetRelationMask mask, EntityRelation relation)
        {
            if (!IsValid(mask)) return false;
            // 결과 enum의 숫자를 비트로 캐스팅하면 Enemy(3)가 Self|Ally로 오인됩니다.
            var bit = relation switch
            {
                EntityRelation.Self => TargetRelationMask.Self,
                EntityRelation.Ally => TargetRelationMask.Ally,
                EntityRelation.Enemy => TargetRelationMask.Enemy,
                EntityRelation.Neutral => TargetRelationMask.Neutral,
                EntityRelation.Unrelated => TargetRelationMask.Unrelated,
                _ => TargetRelationMask.None
            };
            return bit != TargetRelationMask.None && (mask & bit) != 0;
        }
    }
}
