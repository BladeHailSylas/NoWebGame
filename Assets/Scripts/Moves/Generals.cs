using System;
using System.Collections.Generic;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
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
            TryExecute(ctx);
        }
        [SerializeField] private DestinationSettings destination;
        public DestinationSettings Destination => destination;
        public void ConfigureDestination(DestinationSettings value) => destination = value;
        public bool TryExecute(CastContext ctx)
        {
            if (!ReferenceEquals(ctx.Mech, this))
            { AttackLog.Write(ctx.Trace, "ExecutionAborted", "reason=MechanismMismatch"); return false; }
            if (!SpatialExecution.TryPrepare(ctx, out var prepared, out _)) return false;
            ExecuteCore(prepared);
            return true;
        }
        protected virtual void ExecuteCore(CastContext ctx) => Debug.LogError("Mechanism has no implementation.");
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
    public readonly struct SwitchVariable
    {
        public readonly VariableConsumption Consumption;
        public readonly VariableDefinition Variable;
        public readonly int Amount;

        public SwitchVariable(VariableDefinition va, int amount, VariableConsumption consumption = null)
        {
            Consumption = consumption;
            Variable = va;
            Amount = amount;
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
