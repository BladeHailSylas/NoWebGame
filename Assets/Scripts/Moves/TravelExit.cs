using System;
using System.Collections.Generic;
using Systems.Data;
using UnityEngine;

namespace Moves
{
    // 숫자 0은 기존 직렬화 데이터에 새 필드가 없을 때의 호환 기본값입니다.
    public enum ExitCallbackMode { Legacy = 0, Classified = 1 }
    public enum TimeoutPolicy { Completed = 0, Interrupted = 1 }
    public enum ExitKind { Completed = 0, Interrupted = 1 }
    public enum ExitCause
    {
        PositionReached = 0, TargetHit = 1, EnemyHit = 2, TimeExpired = 3,
        WallBlocked = 4, MovementBlocked = 5, TargetLost = 6, TargetNotHit = 7,
        ReplacedByDash = 8, Teleported = 9, OwnerDisabled = 10,
        ExternallyDestroyed = 11, Cancelled = 12, DestinationReached = 13
    }
    public enum ChainTargetSource { Inherit = 0, ExitPosition = 1 }

    public readonly struct TravelResult
    {
        public readonly ExitKind Kind;
        public readonly ExitCause Cause;
        public readonly Vector2 Position;
        public readonly Entity HitEntity;
        public TravelResult(ExitKind kind, ExitCause cause, Vector2 position, Entity hitEntity = null)
        { Kind = kind; Cause = cause; Position = position; HitEntity = hitEntity; }
    }

    /// <summary>종료를 먼저 확정합니다. 재진입·OnDisable·Destroy가 다시 호출해도 부작용은 한 번입니다.</summary>
    public sealed class TravelCompletion
    {
        public TravelResult? Result { get; private set; }
        public bool TryFinish(TravelResult result, Action cleanup, Action<TravelResult> publish)
        {
            if (Result.HasValue) return false;
            Result = result;
            try { cleanup?.Invoke(); }
            finally { publish?.Invoke(result); }
            return true;
        }
    }

    public static class TravelCallbacks
    {
        public static void Publish(CastContext ctx, TravelResult result, ExitCallbackMode mode,
            List<MechanismData> legacy, List<MechanismData> completed, List<MechanismData> interrupted)
        {
            // 새 lifecycle 정리 경로가 기존 콘텐츠에 없던 onExpire를 추가 발생시키지 않게 합니다.
            if (mode == ExitCallbackMode.Legacy &&
                (result.Cause == ExitCause.OwnerDisabled || result.Cause == ExitCause.ExternallyDestroyed)) return;
            var chains = mode == ExitCallbackMode.Legacy ? legacy :
                result.Kind == ExitKind.Completed ? completed : interrupted;
            SkillUtils.ActivateChain(chains, ctx.WithExit(result), trigger: mode == ExitCallbackMode.Legacy ? "onExpire" : result.Kind == ExitKind.Completed ? "onCompleted" : "onInterrupted");
        }
    }
}
