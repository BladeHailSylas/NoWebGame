using UnityEngine;
using SkillInterfaces;

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
        CasterPos = FixedVector2.FromVector2(caster.position);
    }
    public TargetRequest(Transform caster, TargetMode mode)
    {
        Caster = caster;
        MinRange = 0;
        MaxRange = float.MaxValue;
        Mode = mode;
        TargetMask = LayerMask.GetMask("Foe");
        CasterPos = FixedVector2.FromVector2(caster.position);
    }

    public TargetRequest(FixedVector2 caster, TargetMode mode)
    {
        Caster = null;
        MinRange = 0;
        MaxRange = float.MaxValue;
        Mode = mode;
        TargetMask = LayerMask.GetMask("Foe");
        CasterPos = caster;
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

    [Tooltip("임시 앵커 오브젝트의 프리팹 (없으면 자동 생성)")]
    public GameObject anchorPrefab;

    [Tooltip("디버그 로그 출력")] public bool debugLog = true;

    public TargetResolveResult ResolveTarget(TargetRequest request)
    {
        switch (request.Mode)
        {
            case TargetMode.TowardsEntity:
                return ResolveTowardsEntity(request);

            case TargetMode.TowardsCursor:
                return ResolveTowardsCursor(request);

            default:
                Debug.LogError("No good");
                return new TargetResolveResult(null, request.CasterPos, false);
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
            if (debugLog)
                Debug.Log("[TargetResolver] 커서 아래에 감지된 엔터티 없음");
            return new TargetResolveResult(null, req.CasterPos, false);
        }

        // 사거리 계산
        var distance = Vector2.Distance(req.CasterPos.AsVector2, hit.transform.position);
        if (distance > req.MaxRange || distance < req.MinRange)
        {
            if (debugLog)
                Debug.Log($"[TargetResolver] 사거리 조건 불충족 (거리 {distance:F2})");
            return new TargetResolveResult(null, req.CasterPos, false);
        }

        if (hit.transform.TryGetComponent<Entity>(out var entity) && !entity.targetable)
        {
            if(debugLog) Debug.Log("[TargetResolver] 타깃 불가능한 엔터티");
            return new TargetResolveResult(null, req.CasterPos, false);
        }
        if (debugLog)
            Debug.Log($"[TargetResolver] 타깃 '{hit.transform.name}' 확정 (거리 {distance:F2})");

        return new TargetResolveResult(
            hit.transform,
            FixedVector2.FromVector2(hit.transform.position),
            true);
    }
    private TargetResolveResult ResolveTowardsCursor(TargetRequest req)
    {
        // 커서 월드 좌표 얻기
        if (!cursorResolver.TryGetCursorWorld(out var worldPos, out var fixedPos))
        {
            if (debugLog)
                Debug.Log("[TargetResolver] 커서 좌표 감지 실패");
            return new TargetResolveResult(null, req.CasterPos, false);
        }

        // 사거리 검사
        var distance = Vector2.Distance(req.CasterPos.AsVector2, worldPos);
        if (distance > req.MaxRange || distance < req.MinRange)
        {
            if (debugLog)
                Debug.Log($"[TargetResolver] 커서 위치가 사거리 밖 (거리 {distance:F2})");
            return new TargetResolveResult(null, req.CasterPos, false);
        }

        // 임시 Anchor 오브젝트 생성
        var anchor = anchorPrefab is null
            ? Instantiate(anchorPrefab, worldPos, Quaternion.identity)
            : new GameObject("Anchor_Temp");

        anchor.transform.position = worldPos;

        if (debugLog)
            Debug.Log($"[TargetResolver] 커서 기준 앵커 생성 ({worldPos})");

        // 일정 시간 후 자동 제거
        Destroy(anchor, 0.1f);

        return new TargetResolveResult(anchor.transform, fixedPos, true);
    }
}