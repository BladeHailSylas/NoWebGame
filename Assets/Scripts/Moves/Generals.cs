using System;
using Systems.Data;
using Systems.Stacks.Definition;
using UnityEngine;

namespace Moves
{
    /// <summary>
    /// Encapsulates damage configuration for hitscan or projectile skills.
    /// </summary>
    public readonly struct DamageData
    {
        public readonly DamageType Type;
        public readonly int Value;
        public readonly int Attack;
        public readonly double APRatio;
        public readonly double Amplitude;

        public DamageData(DamageType type, int attack, int value = 1, double apRatio = 0, double amplitude = 1)
        {
            Type = type;
            Attack = attack;
            Value = value;
            APRatio = apRatio;
            Amplitude = amplitude;
        }
    }
// DamageType Normal, Fixed, MaxPercent, CurrentPercent, LostPercent

    public interface INewMechanism
    {
        void Execute(CastContext ctx);
    }

    public interface ISystemMechanism : INewMechanism
    {
    
    }

    public enum TargetMode { TowardsEntity, TowardsCursor, TowardsMovement, TowardsCoordinate }

    public interface INewParams
    {
        short CooldownTicks { get; }
        float MinRange { get; }
        float MaxRange { get; }
    }

    public struct CastContext
    {
        public readonly INewParams Params;
        public readonly Transform Caster;
        public readonly Transform Target;
        public readonly TargetMode Mode;
        public DamageData Damage;
        public SwitchVariable Var;

        public CastContext(INewParams param, Transform caster, Transform target, DamageData damage, SwitchVariable va = default, TargetMode mode = TargetMode.TowardsEntity)
        {
            Params = param;
            Caster = caster;
            Target = target;
            Damage = damage;
            Var = va;
            Mode = mode;
        }
    } 

    public readonly struct SkillCommand
    {
        public readonly Transform Caster;
        public readonly Transform Target;
        public readonly TargetMode TargetMode;
        public readonly FixedVector2 CastPosition;
        public readonly INewMechanism Mech;
        public readonly INewParams Params;
        public readonly DamageData Damage;
        public readonly SwitchVariable Var;

        public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition,
            INewMechanism mech, INewParams @params, DamageData damage, Transform target = null, SwitchVariable va = default)
        {
            Caster = caster;
            Target = target;
            TargetMode = mode;
            CastPosition = castPosition;
            Mech = mech;
            Params = @params;
            Damage = damage;
            Var = va;
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

    [Serializable]
    public struct MechanismRef
    {
        public ScriptableObject mechanism;
        [SerializeReference] public INewParams @params;
        public bool requireRetarget;
        public bool respectBusy;
    }
}