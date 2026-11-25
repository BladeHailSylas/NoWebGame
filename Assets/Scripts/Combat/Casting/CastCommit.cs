using global::NoWebGame.Systems.Data;
using global::NoWebGame.Systems.Time;
using System;



namespace NoWebGame.Combat.Casting
{
    public enum CastCommitState { Prepared, Committed, Executing, ExecutionReturned, Cancelled, ExecutionException, DispatchAborted }

    /// <summary>루트만 소유합니다. Chain에는 상속하지 않으며 효과의 전체 수명도 나타내지 않습니다.</summary>
    public sealed class CastCommit
    {
        public long CommitTick { get; }
        public long DueTick { get; }
        public CastCommitState State { get; private set; }
        public string EndReason { get; private set; }
        public long CommitId { get; private set; }
        public SkillSlot Slot { get; private set; }
        public int ManaCost { get; private set; }
        internal DelayId DelayId;
        private Action _releasePending;
        private Func<string> _validatePending;
        internal CastCommit(long tick, byte delay, Action releasePending)
        { CommitTick = tick; DueTick = checked(tick + delay); _releasePending = releasePending; }
        internal void Publish() => State = CastCommitState.Committed;
        internal void Configure(long id, SkillSlot slot, int cost, Func<string> validatePending)
        { CommitId = id; Slot = slot; ManaCost = cost; _validatePending = validatePending; }
        internal string ValidatePending() => _validatePending?.Invoke();
        internal bool BeginExecution()
        {
            if (State != CastCommitState.Committed) return false;
            State = CastCommitState.Executing; Release(); return true;
        }
        internal void Returned()
        { if (State == CastCommitState.Executing) { State = CastCommitState.ExecutionReturned; EndReason = "RunnerReturned"; } }
        internal void Aborted(string reason)
        { if (State == CastCommitState.Executing) { State = CastCommitState.DispatchAborted; EndReason = reason; Release(); } }
        internal void Failed(string reason = "ExecutionException")
        {
            if (State is not (CastCommitState.Prepared or CastCommitState.Committed or CastCommitState.Executing)) return;
            State = CastCommitState.ExecutionException; EndReason = reason; Release();
        }
        internal bool Cancel(string reason = "Cancelled")
        {
            // 실행 중이거나 끝난 루트에 취소가 재진입해도 효과의 수명이나 종료 결과를 덮어쓰지 않습니다.
            if (State is not (CastCommitState.Prepared or CastCommitState.Committed)) return false;
            State = CastCommitState.Cancelled; EndReason = reason; Release(); return true;
        }
        internal string Describe() => $"commitId={CommitId} slot={Slot} manaCost={ManaCost} commitTick={CommitTick} dueTick={DueTick} state={State} reason={EndReason}";
        private void Release()
        {
            var release = _releasePending; _releasePending = null; _validatePending = null; release?.Invoke();
        }
    }
}
