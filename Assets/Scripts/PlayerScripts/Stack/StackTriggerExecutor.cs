using System;
using Moves;
using Systems.Data;
using UnityEngine;

namespace PlayerScripts.Stack
{
    /// <summary>
    /// 상태 없는 Stack 실행 경계입니다. 수량·수명·소비는 Manager만 소유합니다.
    /// 루트 Cast Commit을 생성하지 않고 준비된 Mechanism을 직접 한 번 실행합니다.
    /// </summary>
    internal static class StackTriggerExecutor
    {
        internal static bool TryPrepare(INewMechanism mechanism, TargetMode mode, Transform caster,
            int payload, string source, out CastContext prepared)
        {
            prepared = default;
            try
            {
                if (mechanism == null)
                { Debug.LogWarning("StackTrigger preparation rejected: MissingMechanism"); return false; }
                Vector2? origin = caster ? (Vector2)caster.position : null;
                var input = new CastContext(mechanism, caster, null, new DamageData(DamageType.Normal, payload),
                    default, mode, originCastPosition: origin,
                    trace: AttackTrace.Root(caster).Command().Effect(), source: source);
                if (SpatialExecution.TryPrepare(input, out prepared, out var failure)) return true;
                Debug.LogWarning($"StackTrigger preparation rejected: {failure}");
                return false;
            }
            catch (Exception error)
            {
                // 준비 예외는 미소비입니다. Manager에 예외를 던져 후속 큐 처리를 막지 않습니다.
                Debug.LogException(error);
                return false;
            }
        }

        internal static void ExecutePrepared(CastContext prepared)
        {
            // 호출 전에 소비가 확정되어야 합니다. 실패해도 복구하거나 자동 재실행하지 않습니다.
            try { prepared.Mech.Execute(prepared); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
