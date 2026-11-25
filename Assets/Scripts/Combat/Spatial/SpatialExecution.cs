using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Moves.ObjectEntity;

using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Diagnostics.Runtime;


using UnityEngine;

namespace NoWebGame.Combat.Spatial
{
    /// <summary>Target 탐색은 Runner 책임입니다. 직접 실행도 이 경계에서 같은 공간 준비를 수행합니다.</summary>
    public static class SpatialExecution
    {
        public static bool Supports(INewMechanism mechanism) => mechanism is ProjectileMechanism or DashMechanism or
            TeleportMechanism or AreaMechanism or SummonMechanism or RayMechanism or HitscanMechanism or MeleeMechanism;

        public static bool TryPrepare(CastContext input, out CastContext prepared, out SpatialFailure failure)
        {
            prepared = input; failure = SpatialFailure.None;
            if (input.SpatialPrepared) return true;
            // Runner 밖에서 시작한 독립 실행에도 고유 추적 식별자를 부여합니다.
            if (input.Trace.AttackId == 0)
                input = new CastContext(input.Mech, input.Caster, input.Target, input.Damage, input.Var, input.Mode,
                    input.Exit, input.OriginCastPosition, AttackTrace.Root(input.Caster).Command().Effect(), input.Source, input.SwitchSelection);
            var settings = input.Mech is NewMechanism mech ? mech.Destination : default;
            var origin = input.OriginCastPosition ?? (input.Caster ? (Vector2?)input.Caster.position : null);
            var values = new BasePositionInputs(input.Caster ? (Vector2?)input.Caster.position : null,
                input.Target.TryGetPosition(out var position) ? position : (Vector2?)null, origin, input.Exit?.Position);
            if (settings.mode == DestinationMode.Disabled)
            { prepared = input.WithSpatial(origin, null); return true; }
            if (settings.mode != DestinationMode.BasePosition) failure = SpatialFailure.UnsupportedDestinationMode;
            else if (!Supports(input.Mech)) failure = SpatialFailure.UnsupportedDestinationMechanism;
            else if (!input.Caster) failure = SpatialFailure.MissingCasterPosition;
            else if (BasePositionResolver.TryResolve(settings.position, values, out var destination, out failure))
            {
                var directionOrigin = (Vector2)input.Caster.position;
                var directional = input.Mech is RayMechanism or HitscanMechanism or SummonMechanism ||
                    input.Mech is MeleeMechanism melee && melee.angleDeg < 360;
                if (input.Mech is AreaMechanism area && area.areaPrefab)
                {
                    directional = area.areaPrefab.areaShape is BoxArea or LaserArea;
                    if (area.areaPrefab.areaShape is BoxArea) directionOrigin = origin.Value;
                }
                var delta = destination - directionOrigin;
                if (!BasePositionResolver.Finite(delta) || !BasePositionResolver.Finite(delta.sqrMagnitude)) failure = SpatialFailure.NonFiniteResult;
                else if (directional && delta.sqrMagnitude < BasePositionResolver.DirectionEpsilonSquared) failure = SpatialFailure.SpatialDirectionUndefined;
                else
                {
                    prepared = input.WithSpatial(origin, destination);
                    AttackLog.Write(input.Trace, "DestinationResolved", $"source={input.Source} mechanism={input.Mech.GetType().Name} target={input.Target} settings={settings.position} {values} destination={destination}");
                    return true;
                }
            }
            AttackLog.Write(input.Trace, "ExecutionAborted", $"reason={failure} source={input.Source} mechanism={input.Mech?.GetType().Name} mode={input.Mode} target={input.Target} destinationMode={settings.mode} settings={settings.position} {values} exitCause={input.Exit?.Cause}");
            return false;
        }
    }
}
