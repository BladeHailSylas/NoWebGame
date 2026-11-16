using System;
using System.Collections.Generic;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Moves
{
    /// <summary>
    /// Encapsulates damage configuration for hitscan or projectile skills.
    /// DamageType Type, int Value, int Attack, double APRatio, double Amplitude, (Optional) Transform Attacker
    /// </summary>
    public readonly struct DamageData
    {
        public readonly AttackTrace Trace;
        public readonly bool UseRelationPolicy;
        public readonly DamageType Type;
        public readonly int Value;
        public readonly int Attack;
        public readonly double APRatio;
        public readonly double Amplitude;
        public readonly Transform Attacker;

        public DamageData(DamageType type, int attack, int value = 1, double apRatio = 0, double amplitude = 1, Transform attacker = null, AttackTrace trace = default, bool useRelationPolicy = false)
        {
            Trace = trace;
            UseRelationPolicy = useRelationPolicy;
            Type = type;
            Attack = attack;
            Value = value;
            APRatio = apRatio;
            Amplitude = amplitude;
            Attacker = attacker;
        }
    }
// DamageType Normal, Fixed, MaxPercent, CurrentPercent, LostPercent

    public interface INewMechanism
    {
        public short CooldownTicks { get; }
        public byte DelayTicks { get; }
        public float MinRange { get; }
        public float MaxRange { get; }
        public LayerMask Mask { get; }
        void Execute(CastContext ctx);
    }

    [Serializable]
    public abstract class NewMechanism : INewMechanism, IRelativeTargeting
    {
        public void Execute(CastContext ctx)
        {
            Debug.LogError("You have executed a mechanism without any actions;");
        }
        [SerializeField] private short cooldownTicks;
        [SerializeField] private byte delayTicks;
        [SerializeField] private int minRange;
        [SerializeField] private int maxRange;
        [SerializeField, InspectorName("Legacy Physics Mask"), Tooltip("버전 0에서만 사용합니다. Relative 모드의 관계 설정으로 해석하지 않습니다.")] private LayerMask mask;
        [TargetMaskVersion, SerializeField, Tooltip("0: 기존 LayerMask, 1: 상대 관계. 에셋 변환은 별도 승인 후 수행합니다.")] private int targetMaskVersion;
        [SerializeField, Tooltip("상대 관계 모드 전용. None은 어떤 Entity 효과도 허용하지 않습니다.")] private TargetRelationMask targetRelations;
        public TargetPolicy Targeting => new(targetMaskVersion, targetRelations);
        /// <summary>검증용 임시 객체/향후 명시적 마이그레이션에서만 호출합니다. 구 Mask를 자동 해석하지 않습니다.</summary>
        public void ConfigureRelativeTargets(TargetRelationMask relations)
        {
            if (!TargetRelationMasks.IsValid(relations)) throw new ArgumentOutOfRangeException(nameof(relations));
            targetRelations = relations;
            targetMaskVersion = TargetPolicy.CurrentVersion;
        }
        public short CooldownTicks => cooldownTicks;
        public byte DelayTicks => delayTicks;
        public float MinRange => minRange / 1000f;
        public float MaxRange => maxRange / 1000f;
        public LayerMask Mask => mask;
    }

    public abstract class ObjectGeneratingMechanism
    {
        
    }

    public interface ISystemMechanism : INewMechanism
    {
    
    }

    public enum TargetMode
    {
        TowardsEntity, 
        TowardsCursor, 
        TowardsMovement, 
        TowardsCoordinate,
        TowardsSelf,
        TowardsCaster,
        AutoDetection,
    }
    /// <summary>
    /// INewParams params, Transform Caster, Transform Target, TargetMode Mode, DamageData Damage, (Optional) SwitchVariable Var
    /// </summary>
    public struct CastContext
    {
        public readonly AttackTrace Trace;
        public readonly INewMechanism Mech;
        public readonly Transform Caster;
        public readonly SkillTarget Target;
        public readonly TargetMode Mode;
        public DamageData Damage;
        public SwitchVariable Var;
        public readonly TravelResult? Exit;
        public readonly Vector2? OriginCastPosition;

        /// <summary>
        /// The base constructor that is used before.
        /// It automatically sets the TargetMode to TowardsEntity, so would be replaced by the alternate constructor if you want to specify the TargetMode explicitly.
        /// </summary>
        /// <param name="mech"></param>
        /// <param name="caster"></param>
        /// <param name="target"></param>
        /// <param name="damage"></param>
        /// <param name="va"></param>
        /// <param name="mode"></param>
        /// <param name="exit"></param>
        /// <param name="originCastPosition"></param>
        public CastContext(
            INewMechanism mech,
            Transform caster,
            SkillTarget target,
            DamageData damage,
            SwitchVariable va = default,
            TargetMode mode = TargetMode.TowardsEntity,
            TravelResult? exit = null,
            Vector2? originCastPosition = null, AttackTrace trace = default)
        {
            Trace = trace;
            Mech = mech;
            Caster = caster;
            Target = target;
            Damage = damage;
            Var = va;
            Mode = mode;
            Exit = exit;
            OriginCastPosition = originCastPosition;
        }

        /// <summary>
        /// Alternate constructor for CastContext that allows specifying the TargetMode explicitly.
        /// Also includes optional parameters for SwitchVariable, TravelResult, and origin cast position.          lokkkkkkkkkkkkk
        /// </summary>
        /// <param name="mech"></param>
        /// <param name="caster"></param>
        /// <param name="target"></param>
        /// <param name="damage"></param>
        /// <param name="mode"></param>
        /// <param name="va"></param>
        /// <param name="exit"></param>
        /// <param name="originCastPosition"></param>
        public CastContext(
            INewMechanism mech,
            Transform caster,
            SkillTarget target,
            DamageData damage,
            TargetMode mode,
            SwitchVariable va = default,
            TravelResult? exit = null,
            Vector2? originCastPosition = null, AttackTrace trace = default)
        {
            Trace = trace;
            Mech = mech;
            Caster = caster;
            Target = target;
            Damage = damage;
            Var = va;
            Mode = mode;
            Exit = exit;
            OriginCastPosition = originCastPosition;
        }
        public CastContext WithExit(TravelResult result) => new(Mech, Caster, Target, Damage, Var, Mode, result, OriginCastPosition, Trace);
    } 

    public readonly struct SkillCommand : IEquatable<SkillCommand>
    {
        public readonly AttackTrace Trace;
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
            Vector2? originCastPosition = null, ExplicitAttackTarget explicitTarget = null, AttackTrace trace = default)
        {
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

        public bool Equals(SkillCommand other)
        {
            return Caster == other.Caster && Target.Equals(other.Target) && Equals(Mech, other.Mech) &&
                ReferenceEquals(ExplicitTarget, other.ExplicitTarget);
        }

        public override bool Equals(object obj)
        {
            return obj is SkillCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Caster, Target, Mech, ExplicitTarget);
        }
    }

    public readonly struct SwitchVariable
    {
        public readonly VariableDefinition Variable;
        public readonly int Amount;

        public SwitchVariable(VariableDefinition va, int amount)
        {
            Variable = va;
            Amount = amount;
        }
    }

    public static class SkillUtils
    {
        public static void ActivateChain(IEnumerable<MechanismData> chains, CastContext ctx, SkillTarget? target = null, string trigger = "Unspecified")
        {
            if (chains == null || !ctx.Caster) { AttackLog.Write(ctx.Trace, "ChainSkipped", $"trigger={trigger} reason={(chains == null ? "NoChildren" : "MissingCaster")}", true); return; }
            foreach (var chain in chains)
            {
                if (chain?.mechanism is not { } mech) { AttackLog.Write(ctx.Trace, "ChainSkipped", "reason=MissingMechanism"); continue; }
                var next = target ?? ctx.Target;
                if (chain.requireRetarget) next = default;
                else if (chain.targetSource == ChainTargetSource.ExitPosition)
                {
                    if (!ctx.Exit.HasValue) { AttackLog.Write(ctx.Trace, "ChainSkipped", "reason=MissingExitPosition"); continue; }
                    next = SkillTarget.At(ctx.Exit.Value.Position);
                }
                AttackLog.Write(ctx.Trace, "Chain", $"trigger={trigger} child={mech.GetType().Name} retarget={chain.requireRetarget} source={chain.targetSource} target={next}");
                CommandBridge.Enqueue(new SkillCommand(ctx.Caster, chain.mode,
                    new FixedVector2(ctx.Caster.position), mech, ctx.Damage, next, ctx.Var, TargetPolicy.From(mech).IsRelative ? mech.Mask : ctx.Mech.Mask, ctx.Exit,
                    ctx.OriginCastPosition, trace: ctx.Trace.Command()));
            }
        }
    }

    [Serializable]
    public class MechanismData
    {
        [SerializeReference] public INewMechanism mechanism;
        public TargetMode mode;
        public bool requireRetarget;
        public bool respectBusy;
        public ChainTargetSource targetSource;
    }

}
