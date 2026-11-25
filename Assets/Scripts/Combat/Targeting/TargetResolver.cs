using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Entities.Core;
using global::NoWebGame.Input;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using global::NoWebGame.Systems.Data;





using UnityEngine;


namespace NoWebGame.Combat.Targeting
{
    /// <summary>
    /// TargetResolver의 입력 데이터 (시전자, 사거리, 모드 등)
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "PlayerScripts.Skills", "Assembly-CSharp", "TargetRequest")]
    public readonly struct TargetRequest
    {
        public readonly AttackTrace Trace;
        public readonly Transform Caster;
        public readonly float MinRange;
        public readonly float MaxRange;
        public readonly TargetMode Mode;
        public readonly LayerMask TargetMask;
        public readonly TargetPolicy Targeting;
        public readonly FixedVector2 CasterPos;

        public TargetRequest(Transform caster, float minRange, float maxRange, TargetMode mode, LayerMask targetMask, AttackTrace trace = default, TargetPolicy targeting = default)
        {
            Trace = trace;
            Targeting = targeting;
            Caster = caster;
            MinRange = minRange;
            MaxRange = maxRange;
            Mode = mode;
            TargetMask = targetMask;
            CasterPos = new FixedVector2(caster.position);
        }
    }
    /// <summary>
    /// TargetResolver의 반환 결과 (타깃 / 앵커 / 감지 여부)
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "PlayerScripts.Skills", "Assembly-CSharp", "TargetResolveResult")]
    public readonly struct TargetResolveResult
    {
        public readonly SkillTarget Target;
        public readonly FixedVector2 Anchor;
        public readonly bool Found;

        public TargetResolveResult(SkillTarget target, FixedVector2 anchor, bool found)
        {
            Target = target;
            Anchor = anchor;
            Found = found;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "PlayerScripts.Skills", "Assembly-CSharp", "TargetResolver")]
[DisallowMultipleComponent]
    public partial class TargetResolver : MonoBehaviour
    {
        [Header("Dependencies")] [Tooltip("커서 위치를 감지할 CursorResolver 모듈")]
        public CursorResolver cursorResolver;

        [Tooltip("디버그 로그 출력")] public bool debugLog = true;

        public TargetResolveResult ResolveTarget(TargetRequest request)
        {
            if (request.Targeting.IsRelative) return ResolveRelative(request);
            AttackLog.Write(request.Trace, "TargetQuery", $"mode={request.Mode} queryLayers={request.TargetMask.value} min={request.MinRange} max={request.MaxRange}");
            switch (request.Mode)
            {
                case TargetMode.TowardsEntity:
                {
                    return ResolveTowardsEntity(request);
                }

                case TargetMode.TowardsCursor:
                {
                    return ResolveTowardsCursor(request);
                }
                case TargetMode.TowardsMovement:
                case TargetMode.TowardsCoordinate:
                {
                    return new TargetResolveResult(default, request.CasterPos, false);
                }
                case TargetMode.TowardsSelf:
                {
                    return new TargetResolveResult(request.Caster, request.CasterPos, true);
                }
                default:
                {
                    AttackLog.Write(request.Trace, "TargetRejected", $"reason=UnsupportedMode mode={request.Mode}");
                    return new TargetResolveResult(default, request.CasterPos, false);
                }
            }
        }
        private TargetResolveResult ResolveTowardsEntity(TargetRequest req)
        {
            // 커서 위치에서 Collider 검색
            var screenPos = global::UnityEngine.Input.mousePosition;
            var worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;
            var hit = Physics2D.OverlapPoint(worldPos, req.TargetMask);
            if (!hit)
            {
                AttackLog.Write(req.Trace, "TargetRejected", $"reason=NoCollider queryLayers={req.TargetMask.value} point={worldPos}");
                return new TargetResolveResult(default, req.CasterPos, false);
            }

            var entity = EntityIdentity.FromCollider(hit);
            if (!EntityIdentity.IsValid(entity)) { AttackLog.Write(req.Trace, "TargetRejected", $"reason=InvalidEntity entity={AttackLog.Identity(entity)} collider={AttackLog.Identity(hit)}"); return new TargetResolveResult(default, req.CasterPos, false); }

            // 사거리 계산
            var distance = Vector2.Distance(req.CasterPos.AsVector2, entity.transform.position);
            if (distance > req.MaxRange || distance < req.MinRange)
            {
                AttackLog.Write(req.Trace, "TargetRejected", $"reason=Range distance={distance} min={req.MinRange} max={req.MaxRange}");
                return new TargetResolveResult(default, req.CasterPos, false);
            }

            if (!entity.targetable)
            {
                if(debugLog) Debug.Log("[TargetResolver] 타깃 불가능한 엔터티");
                return new TargetResolveResult(default, req.CasterPos, false);
            }
            AttackLog.Write(req.Trace, "TargetResolved", $"entity={AttackLog.Identity(entity)} distance={distance}");

            return new TargetResolveResult(
                SkillTarget.ForEntity(entity),
                FixedVector2.FromVector2(entity.transform.position),
                true);
        }
        private TargetResolveResult ResolveTowardsCursor(TargetRequest req)
        {
            // 1. 커서 월드 좌표 획득
            if (!cursorResolver.TryGetCursorWorld(out var cursorWorld, out _))
            {
                AttackLog.Write(req.Trace, "TargetRejected", "reason=CursorUnavailable");

                return new TargetResolveResult(default, req.CasterPos, false);
            }

            var casterPos = req.CasterPos.AsVector2;
            var toCursor = cursorWorld - casterPos;
            var distance = toCursor.magnitude;

            // 2. 최소 사거리 검사
            if (distance < req.MinRange)
            {
                //if (debugLog) Debug.Log($"[TargetResolver] 커서가 최소 사거리보다 가까움 ({distance:F2})");
                distance = req.MinRange;
            }

            // 3. 방향 계산
            var direction = toCursor.normalized;

            // 4. 최종 Anchor 위치 결정
            var anchorPos =
                distance > req.MaxRange
                    ? casterPos + direction * req.MaxRange
                    : cursorWorld;
            // 좌표는 GameObject를 빌리지 않고 고정 값으로 전달합니다.
            return new TargetResolveResult(SkillTarget.At(anchorPos), new FixedVector2(anchorPos), true);
        }

        public TargetResolveResult Detect(Transform caster, DetectMechanism detect, AttackTrace trace = default)
        {
            var request = new TargetRequest(caster, detect.MinRange, detect.MaxRange, detect.requiredMode, detect.Mask, trace, detect.Targeting);
            if (request.Targeting.IsRelative) return ResolveRelative(request);
            TargetResolveResult result;
            switch (request.Mode)
            {
                case TargetMode.TowardsEntity:
                {
                    result = ResolveTowardsEntity(request);
                    break;
                }

                case TargetMode.TowardsCursor:
                {
                    result = ResolveTowardsCursor(request);
                    break;
                }

                case TargetMode.TowardsMovement:
                case TargetMode.TowardsCoordinate:
                {
                    return new TargetResolveResult(default, new FixedVector2(0, 0), false);
                }
                default:
                {
                    AttackLog.Write(request.Trace, "TargetRejected", $"reason=UnsupportedMode mode={request.Mode}");
                    return new TargetResolveResult(default, new FixedVector2(0, 0), false);
                }
            }
            // 컴포넌트 조건은 DetectMechanism에서 검사합니다.
            // 조건 불일치 때문에 탐색한 Target을 버리면 실패 Chain이 이를 상속할 수 없습니다.
            return result;
        }
    }
}
