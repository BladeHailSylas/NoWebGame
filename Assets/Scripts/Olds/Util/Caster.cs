// Casting.cs

using System;
using System.Collections.Generic;
using Systems.Data;
using UnityEngine;

namespace Olds.Util
{
    public enum AbilityHook { OnCastStart, OnHit, OnCastEnd }

    /// 실행 주문서: 무엇을(메커닉/파라미터), 대상 오버라이드(옵션)
    public readonly struct CastOrder
    {
        public readonly ISkillMechanism Mech;
        public readonly ISkillParams Params;
        public readonly Transform TargetOverride;
        public CastOrder(ISkillMechanism mech, ISkillParams @params, Transform targetOverride = null)
        { Mech = mech; Params = @params; TargetOverride = targetOverride; }
    }

    /// FollowUp 공급자(오직 Param만 구현)
    public interface IFollowUpProvider
    {
        IEnumerable<(CastOrder order, float delay, bool respectBusyCooldown)>
            BuildFollowUps(AbilityHook hook, Transform prevTarget);
    }

    /// Switch 정책(오직 Param만 구현) — 실행 직전에 선택할 주문 1개
    public interface ISwitchPolicy
    {
        bool TrySelect(Transform owner, Camera cam, Transform prevTarget, out CastOrder order, out MechanicRef reference);
    }

    /// Param이 참조하는 “다음 기술”
    [Serializable]
    public struct MechanicRef
    {
        public ScriptableObject mechanic;               // ISkillMechanic
        [SerializeReference] public ISkillParams @params;  // 캐릭터별 SerializeReference
        public float delay;
        public bool passSameTarget;
        public bool respectBusyCooldown;

        public readonly bool TryBuildOrder(Transform prevTarget, out CastOrder order)
        {
            if (mechanic is not ISkillMechanism next || @params == null || !next.ParamType.IsInstanceOfType(@params))
            { order = default; return false; }

            order = new CastOrder(next, @params, passSameTarget ? prevTarget : null);
            return true;
        }
    }
}