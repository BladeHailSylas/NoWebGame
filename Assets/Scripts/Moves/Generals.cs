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

    [Serializable]
    public abstract class NewMechanism : INewMechanism
    {
        public void Execute(CastContext ctx)
        {
            Debug.LogError("You have executed a mechanism without any actions;");
        }
        [SerializeField] private short cooldownTicks;
        [SerializeField] private byte delayTicks;
        [SerializeField] private int minRange;
        [SerializeField] private int maxRange;
        [SerializeField] private LayerMask mask;
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
            Vector2? originCastPosition = null)
        {
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
            Vector2? originCastPosition = null)
        {
            Mech = mech;
            Caster = caster;
            Target = target;
            Damage = damage;
            Var = va;
            Mode = mode;
            Exit = exit;
            OriginCastPosition = originCastPosition;
        }
        public CastContext WithExit(TravelResult result) => new(Mech, Caster, Target, Damage, Var, Mode, result, OriginCastPosition);
    } 

    public readonly struct SkillCommand : IEquatable<SkillCommand>
    {
        public readonly Transform Caster;
        public readonly SkillTarget Target;
        public readonly TargetMode TargetMode;
        public readonly FixedVector2 CastPosition;
        // 최초 실제 실행에서 확정되며, 같은 Chain의 후속 Command는 이 값을 상속합니다.
        public readonly Vector2? OriginCastPosition;
        public readonly INewMechanism Mech;
        public readonly DamageData Damage;
        public readonly LayerMask Mask;
        public readonly SwitchVariable Var;
        public readonly TravelResult? Exit;

        public SkillCommand(Transform caster, TargetMode mode, FixedVector2 castPosition,
            INewMechanism mech, DamageData damage, SkillTarget target = default,
            SwitchVariable va = default, int masker = 0, TravelResult? exit = null,
            Vector2? originCastPosition = null)
        {
            Caster = caster;
            Target = target;
            TargetMode = mode;
            CastPosition = castPosition;
            OriginCastPosition = originCastPosition;
            Mech = mech;
            Damage = damage;
            Var = va;
            // 호출자는 레이어 번호가 아니라 LayerMask 비트 집합을 전달합니다.
            Mask = masker;
            Exit = exit;
        }

        public bool Equals(SkillCommand other)
        {
            return Caster == other.Caster && Target.Equals(other.Target) && Equals(Mech, other.Mech);
        }

        public override bool Equals(object obj)
        {
            return obj is SkillCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Caster, Target, Mech);
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
        public static void ActivateChain(IEnumerable<MechanismData> chains, CastContext ctx, SkillTarget? target = null)
        {
            if (chains == null || !ctx.Caster) return;
            foreach (var chain in chains)
            {
                if (chain?.mechanism is not { } mech) continue;
                var next = target ?? ctx.Target;
                if (chain.requireRetarget) next = default;
                else if (chain.targetSource == ChainTargetSource.ExitPosition)
                {
                    if (!ctx.Exit.HasValue) continue;
                    next = SkillTarget.At(ctx.Exit.Value.Position);
                }
                CommandBridge.Enqueue(new SkillCommand(ctx.Caster, chain.mode,
                    new FixedVector2(ctx.Caster.position), mech, ctx.Damage, next, ctx.Var, ctx.Mech.Mask, ctx.Exit,
                    ctx.OriginCastPosition));
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
