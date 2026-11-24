using PlayerScripts.Skills;
using UnityEngine;

namespace Moves
{
    /// <summary>
    /// INewParams params, Transform Caster, Transform Target, TargetMode Mode, DamageData Damage, (Optional) SwitchVariable Var
    /// </summary>
    public struct CastContext
    {
        public readonly SwitchSelection SwitchSelection;
        public readonly AttackTrace Trace;
        public readonly string Source;
        public readonly Vector2? Destination;
        internal readonly bool SpatialPrepared;
        public bool TryGetSpatialPosition(out Vector2 position)
        {
            if (Destination.HasValue) { position = Destination.Value; return true; }
            return Target.TryGetPosition(out position);
        }
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
            Vector2? originCastPosition = null, AttackTrace trace = default, string source = null, SwitchSelection switchSelection = null)
        {
            SwitchSelection = switchSelection;
            Trace = trace;
            Mech = mech;
            Caster = caster;
            Target = target;
            Damage = damage;
            Var = va;
            Mode = mode;
            Exit = exit;
            OriginCastPosition = originCastPosition;
            Source = source ?? "ExternalDirect"; Destination = null; SpatialPrepared = false;
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
            Vector2? originCastPosition = null, AttackTrace trace = default, string source = null, SwitchSelection switchSelection = null)
        {
            SwitchSelection = switchSelection;
            Trace = trace;
            Mech = mech;
            Caster = caster;
            Target = target;
            Damage = damage;
            Var = va;
            Mode = mode;
            Exit = exit;
            OriginCastPosition = originCastPosition;
            Source = source ?? "ExternalDirect"; Destination = null; SpatialPrepared = false;
        }
        private CastContext(CastContext input, Vector2? origin, Vector2? destination)
            : this(input.Mech, input.Caster, input.Target, input.Damage, input.Var, input.Mode, input.Exit, origin, input.Trace, input.Source, input.SwitchSelection)
        { Destination = destination; SpatialPrepared = true; }
        internal CastContext WithSpatial(Vector2? origin, Vector2? destination) => new(this, origin, destination);
        public CastContext WithExit(TravelResult result)
        {
            var copy = new CastContext(Mech, Caster, Target, Damage, Var, Mode, result, OriginCastPosition, Trace, Source, SwitchSelection);
            return SpatialPrepared ? copy.WithSpatial(OriginCastPosition, Destination) : copy;
        }
    } 

}
