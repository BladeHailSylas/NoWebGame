using System.Threading;
using UnityEngine;

namespace Moves
{
    /// <summary>진단 전용 값입니다. 명령의 동등성/게임 규칙에 사용하지 않습니다.</summary>
    public readonly struct AttackTrace
    {
        public readonly long AttackId, CommandId, ParentCommandId, EffectId, DamageId;
        public readonly int CasterId;
        public readonly string Caster;
        private AttackTrace(long attack, long command, long parent, long effect, long damage, int casterId, string caster)
        { AttackId = attack; CommandId = command; ParentCommandId = parent; EffectId = effect; DamageId = damage; CasterId = casterId; Caster = caster; }
        public static AttackTrace Root(Transform caster) => new(AttackLog.NextId(), 0, 0, 0, 0,
            caster ? caster.GetInstanceID() : 0, AttackLog.Identity(caster));
        public AttackTrace Command() => new(AttackId, AttackLog.NextId(), CommandId, 0, 0, CasterId, Caster);
        public AttackTrace Effect() => new(AttackId, CommandId, ParentCommandId, AttackLog.NextId(), 0, CasterId, Caster);
        public AttackTrace Damage() => new(AttackId, CommandId, ParentCommandId, EffectId, AttackLog.NextId(), CasterId, Caster);
    }

    /// <summary>전역 '현재 공격' 없이 각 요청이 추적 값을 전달합니다. 일반 거절은 Warning이 아닙니다.</summary>
    public static class AttackLog
    {
        private static long _id, _sequence;
        public static bool Enabled { get; set; }
        public static bool Detail { get; set; }
        // 0은 전체, 그 외에는 Caster Transform의 InstanceID입니다.
        public static int CasterFilter { get; set; }
        internal static long NextId() => Interlocked.Increment(ref _id);
        public static string Identity(Object value) => value ? $"{value.name}#{value.GetInstanceID()}" : "null/destroyed";
        public static void Write(AttackTrace trace, string stage, string details, bool detail = false)
        {
            if (!Enabled || (detail && !Detail) || (CasterFilter != 0 && CasterFilter != trace.CasterId)) return;
            var tick = Systems.Time.Time.Ticker?.TickCount.ToString() ?? "none";
            Debug.Log($"[AttackTrace] seq={Interlocked.Increment(ref _sequence)} tick={tick} attack={trace.AttackId} command={trace.CommandId} parent={trace.ParentCommandId} effect={trace.EffectId} damage={trace.DamageId} caster={trace.Caster} stage={stage} {details}");
        }
        public static void Hit(CastContext ctx, Collider2D collider, string result)
        {
            var entity = collider ? collider.GetComponentInParent<Systems.Data.Entity>(true) : null;
            Write(ctx.Trace, "Hit", $"result={result} collider={Identity(collider)} colliderLayer={(collider ? collider.gameObject.layer : -1)} entity={Identity(entity)} entityLayer={(entity ? entity.gameObject.layer : -1)}");
        }
    }
}
