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
        public readonly DamageType Type;
        public readonly int Value;
        public readonly int Attack;
        public readonly double APRatio;
        public readonly double Amplitude;
        public readonly Transform Attacker;

        public DamageData(DamageType type, int attack, int value = 1, double apRatio = 0, double amplitude = 1, Transform attacker = null)
        {
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

    public abstract class NewMechanism : INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            Debug.LogError("You have executed a mechanism without any actions;");
        }
        private short cooldownTicks;
        private byte delayTicks;
        private int minRange;
        private int maxRange;
        private LayerMask mask;
        public short CooldownTicks => cooldownTicks;
        public byte DelayTicks => delayTicks;
        public float MinRange => minRange / 1000f;
        public float MaxRange => maxRange / 1000f;
        public LayerMask Mask => mask;
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
        public readonly INewMechanism Mech;
        public readonly Transform Caster;
        public readonly Transform Target;
        public readonly TargetMode Mode;
        public DamageData Damage;
        public SwitchVariable Var;

        public CastContext(INewMechanism mech, Transform caster, Transform target, DamageData damage, SwitchVariable va = default, TargetMode mode = TargetMode.TowardsEntity)
        {
            Mech = mech;
            Caster = caster;
            Target = target;
            Damage = damage;
            Var = va;
            Mode = mode;
        }
    } 

    public readonly struct SkillCommand : IEquatable<SkillCommand>
    {
        public readonly Transform Caster;
        public readonly Transform Target;
        public readonly TargetMode TargetMode;
        public readonly FixedVector2 CastPosition;
        public readonly INewMechanism Mech;
        public readonly DamageData Damage;
        public readonly LayerMask Mask;
        public readonly SwitchVariable Var;

        public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition,
            INewMechanism mech, DamageData damage, Transform target = null,
            SwitchVariable va = default, int masker = 0)
        {
            Caster = caster;
            Target = target;
            TargetMode = mode;
            CastPosition = castPosition;
            Mech = mech;
            Damage = damage;
            Var = va;
            Mask = 1 << masker;
        }

        public bool Equals(SkillCommand other)
        {
            return Caster.Equals(other.Caster) && Target.Equals(other.Target) && Mech.Equals(other.Mech);
        }

        public override bool Equals(object obj)
        {
            return obj is SkillCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Caster, Target, (int)TargetMode, CastPosition, Mech);
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
        public static void ActivateFollowUp(List<MechanismData> followups, CastContext ctx, Transform target = null)
        {
            if (followups.Count == 0) return;
            foreach (var followup in followups)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                target ??= ctx.Target;
                var ctxTarget = !followup.requireRetarget ? target : null;
                SkillCommand cmd = new(ctx.Caster, followup.mode, new FixedVector2(ctx.Caster.position),
                    mech, ctx.Damage, ctxTarget, ctx.Var, ctx.Mech.Mask);
                CommandCollector.Instance.EnqueueCommand(cmd);
            }
        }

        public static void ActivateFollowUp(MechanismData[] followups, CastContext ctx)
        {
            if (followups.Length == 0) return;
            foreach (var followup in followups)
            {
                if (followup.mechanism is not INewMechanism mech) continue;
                var ctxTarget = !followup.requireRetarget ? ctx.Target : null;
                SkillCommand cmd = new(ctx.Caster, followup.mode, new FixedVector2(ctx.Caster.position),
                    mech, ctx.Damage, ctxTarget, ctx.Var, ctx.Mech.Mask);
                CommandCollector.Instance.EnqueueCommand(cmd);
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
    }

    [Serializable]
    [CreateAssetMenu(menuName = "Skills/SkillData", fileName = "SkillData")]
    public class SkillData : ScriptableObject
    {
        [SerializeReference] public MechanismData mechanismData;
    }
}
