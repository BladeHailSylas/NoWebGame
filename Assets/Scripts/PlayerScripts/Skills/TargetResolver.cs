using Moves;
using Moves.Mechanisms;
using PlayerScripts.Core;
using Systems.Anchor;
using Systems.Data;
using UnityEngine;
using Utils;

namespace PlayerScripts.Skills
{
    /// <summary>
    /// TargetResolver의 입력 데이터 (시전자, 사거리, 모드 등)
    /// </summary>
    public readonly struct TargetRequest
    {
        public readonly Transform Caster;
        public readonly float MinRange;
        public readonly float MaxRange;
        public readonly TargetMode Mode;
        public readonly LayerMask TargetMask;
        public readonly FixedVector2 CasterPos;

        public TargetRequest(Transform caster, float minRange, float maxRange, TargetMode mode, LayerMask targetMask)
        {
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
    public readonly struct TargetResolveResult
    {
        public readonly Transform Target;
        public readonly FixedVector2 Anchor;
        public readonly bool Found;

        public TargetResolveResult(Transform target, FixedVector2 anchor, bool found)
        {
            Target = target;
            Anchor = anchor;
            Found = found;
        }
    }

    [DisallowMultipleComponent]
    public class TargetResolver : MonoBehaviour
    {
        [Header("Dependencies")] [Tooltip("커서 위치를 감지할 CursorResolver 모듈")]
        public CursorResolver cursorResolver;

        [Tooltip("디버그 로그 출력")] public bool debugLog = true;

        public TargetResolveResult ResolveTarget(TargetRequest request)
        {
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
                    return new TargetResolveResult(null, request.CasterPos, false);
                }
                case TargetMode.TowardsSelf:
                {
                    return new TargetResolveResult(request.Caster, request.CasterPos, true);
                }
                default:
                {
                    Debug.LogError("No good");
                    return new TargetResolveResult(null, request.CasterPos, false);
                }
            }
        }
        private TargetResolveResult ResolveTowardsEntity(TargetRequest req)
        {
            // 커서 위치에서 Collider 검색
            var screenPos = Input.mousePosition;
            var worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;

            var hit = Physics2D.OverlapPoint(worldPos, req.TargetMask);
            if (hit is null)
            {
                if (debugLog) Debug.Log("[TargetResolver] 커서 아래에 감지된 엔터티 없음");
                return new TargetResolveResult(null, req.CasterPos, false);
            }

            // 사거리 계산
            var distance = Vector2.Distance(req.CasterPos.AsVector2, hit.transform.position);
            if (distance > req.MaxRange || distance < req.MinRange)
            {
                if (debugLog) Debug.Log($"[TargetResolver] 사거리 조건 불충족 (거리 {distance:F2})");
                return new TargetResolveResult(null, req.CasterPos, false);
            }

            if (hit.transform.TryGetComponent<Entity>(out var entity) && !entity.targetable)
            {
                if(debugLog) Debug.Log("[TargetResolver] 타깃 불가능한 엔터티");
                return new TargetResolveResult(null, req.CasterPos, false);
            }
            if (debugLog) Debug.Log($"[TargetResolver] 타깃 '{hit.transform.name}' 확정 (거리 {distance:F2})");

            return new TargetResolveResult(
                hit.transform,
                FixedVector2.FromVector2(hit.transform.position),
                true);
        }
        private TargetResolveResult ResolveTowardsCursor(TargetRequest req)
        {
            // 1. 커서 월드 좌표 획득
            if (!cursorResolver.TryGetCursorWorld(out var cursorWorld, out _))
            {
                if (debugLog)
                    Debug.Log("[TargetResolver] 커서 좌표 감지 실패");

                return new TargetResolveResult(null, req.CasterPos, false);
            }

            var casterPos = req.CasterPos.AsVector2;
            var toCursor = cursorWorld - casterPos;
            var distance = toCursor.magnitude;

            // 2. 최소 사거리 검사
            if (distance < req.MinRange)
            {
                if (debugLog)
                    Debug.Log($"[TargetResolver] 커서가 최소 사거리보다 가까움 ({distance:F2})");

                return new TargetResolveResult(null, req.CasterPos, false);
            }

            // 3. 방향 계산
            var direction = toCursor.normalized;

            // 4. 최종 Anchor 위치 결정
            var anchorPos =
                distance > req.MaxRange
                    ? casterPos + direction * req.MaxRange
                    : cursorWorld;

            // 5. Anchor 대여
            var anchor = AnchorRegistry.Instance.Rent(
                owner: req.Caster,
                position: anchorPos
            );

            if (anchor is null)
            {
                Debug.LogWarning("[TargetResolver] Anchor 대여 실패");
                return new TargetResolveResult(null, req.CasterPos, false);
            }

            if (debugLog)
                Debug.Log($"[TargetResolver] TowardsCursor Anchor 배치 ({anchorPos})");

            // 6. Anchor를 Target으로 반환
            return new TargetResolveResult(
                target: anchor.transform,
                anchor: FixedVector2.FromVector2(anchorPos),
                found: true
            );
        }
        public TargetResolveResult Detect(Transform caster, DetectParams detect)
        {
            var request = new TargetRequest(caster, detect.MinRange, detect.MaxRange, detect.requiredMode, LayerMask.GetMask("Foe"));
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
                    return new TargetResolveResult(null, new FixedVector2(0, 0), false);
                }
                default:
                {
                    Debug.LogError("No good");
                    return new TargetResolveResult(null, new FixedVector2(0, 0), false);
                }
            }
            if (detect.requiredComponent is not null && !result.Target.TryGetComponent(detect.requiredComponent?.GetType(), out _))
            {
                return new TargetResolveResult(null, new FixedVector2(0, 0), false);
            }

            return result;
        }
    }
}