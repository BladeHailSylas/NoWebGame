using System;
using UnityEngine;

namespace NoWebGame.Combat.Spatial
{
    public enum BasePositionRepresentation { Polar = 0 }
    public enum BasePositionAnchor { CasterPosition = 0, InputTargetPosition = 1, OriginCastPosition = 2, ExitPosition = 3 }
    public enum BasePositionDirection { WorldRight = 0, CasterToInputTarget = 1, OriginToInputTarget = 2 }
    public enum DestinationMode { Disabled = 0, BasePosition = 1 }

    [Serializable]
    public struct BasePositionData
    {
        public BasePositionRepresentation representation;
        public BasePositionAnchor anchor;
        public BasePositionDirection direction;
        [Tooltip("도 단위. 양수는 월드 XY 평면에서 반시계 방향입니다.")] public float angleDegrees;
        [Tooltip("월드 단위, 0 이상. 0이면 방향과 각도를 해석하지 않습니다.")] public float distance;
        public override string ToString() => $"{representation}/{anchor}/{direction} angle={angleDegrees} distance={distance}";
    }
    [Serializable]
    public struct DestinationSettings
    {
        public DestinationMode mode;
        public BasePositionData position;
    }

    /// <summary>순수 해석기에 필요한 값만 전달합니다. 커서/Transform/Physics를 조회하지 않습니다.</summary>
    public readonly struct BasePositionInputs
    {
        public readonly Vector2? Caster, InputTarget, Origin, Exit;
        public BasePositionInputs(Vector2? caster, Vector2? inputTarget, Vector2? origin, Vector2? exit)
        { Caster = caster; InputTarget = inputTarget; Origin = origin; Exit = exit; }
        public override string ToString() => $"caster={Caster} inputTarget={InputTarget} origin={Origin} exit={Exit}";
    }
    public enum SpatialFailure
    {
        None, UnsupportedRepresentation, InvalidDistance, MissingCasterPosition, MissingInputTargetPosition,
        MissingOriginCastPosition, MissingExitPosition, UnsupportedAnchor, UnsupportedDirection,
        NonFiniteInput, NonFiniteAngle, DegenerateDirection, NonFiniteResult,
        UnsupportedDestinationMode, UnsupportedDestinationMechanism, SpatialDirectionUndefined, MechanismMismatch
    }
    public static class BasePositionResolver
    {
        public const float DirectionEpsilonSquared = 0.000001f;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Read(Vector2? input, SpatialFailure missing, out Vector2 value, out SpatialFailure failure)
        {
            value = input.GetValueOrDefault();
            failure = !input.HasValue ? missing : !Finite(value) ? SpatialFailure.NonFiniteInput : SpatialFailure.None;
            return failure == SpatialFailure.None;
        }
        public static bool TryResolve(BasePositionData data, BasePositionInputs inputs, out Vector2 result, out SpatialFailure failure)
        {
            result = default; failure = SpatialFailure.None;
            if (data.representation != BasePositionRepresentation.Polar) { failure = SpatialFailure.UnsupportedRepresentation; return false; }
            if (!Finite(data.distance) || data.distance < 0) { failure = SpatialFailure.InvalidDistance; return false; }
            Vector2 point;
            switch (data.anchor)
            {
                case BasePositionAnchor.CasterPosition: if (!Read(inputs.Caster, SpatialFailure.MissingCasterPosition, out point, out failure)) return false; break;
                case BasePositionAnchor.InputTargetPosition: if (!Read(inputs.InputTarget, SpatialFailure.MissingInputTargetPosition, out point, out failure)) return false; break;
                case BasePositionAnchor.OriginCastPosition: if (!Read(inputs.Origin, SpatialFailure.MissingOriginCastPosition, out point, out failure)) return false; break;
                case BasePositionAnchor.ExitPosition: if (!Read(inputs.Exit, SpatialFailure.MissingExitPosition, out point, out failure)) return false; break;
                default: failure = SpatialFailure.UnsupportedAnchor; return false;
            }
            // (0,0)도 정상 좌표입니다. 거리 0에서는 사용하지 않는 방향과 각도를 검증하지 않습니다.
            if (data.distance == 0) { result = point; return true; }
            if (!Finite(data.angleDegrees)) { failure = SpatialFailure.NonFiniteAngle; return false; }
            Vector2 direction = Vector2.right;
            if (data.direction != BasePositionDirection.WorldRight)
            {
                Vector2 from;
                if (data.direction == BasePositionDirection.CasterToInputTarget)
                { if (!Read(inputs.Caster, SpatialFailure.MissingCasterPosition, out from, out failure)) return false; }
                else if (data.direction == BasePositionDirection.OriginToInputTarget)
                { if (!Read(inputs.Origin, SpatialFailure.MissingOriginCastPosition, out from, out failure)) return false; }
                else { failure = SpatialFailure.UnsupportedDirection; return false; }
                if (!Read(inputs.InputTarget, SpatialFailure.MissingInputTargetPosition, out var target, out failure)) return false;
                direction = target - from;
                if (!Finite(direction) || !Finite(direction.sqrMagnitude)) { failure = SpatialFailure.NonFiniteInput; return false; }
                if (direction.sqrMagnitude < DirectionEpsilonSquared) { failure = SpatialFailure.DegenerateDirection; return false; }
                direction.Normalize();
            }
            var radians = (data.angleDegrees % 360f) * Mathf.Deg2Rad;
            var c = Mathf.Cos(radians); var s = Mathf.Sin(radians);
            result = point + new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c) * data.distance;
            if (!Finite(result)) { failure = SpatialFailure.NonFiniteResult; return false; }
            return true;
        }
    }
}
