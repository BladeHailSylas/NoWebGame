using System;
using Systems.Time;

namespace PlayerScripts.Skills
{
    public enum CastCommitState { Prepared, Committed, Executing, ExecutionReturned, Cancelled, ExecutionException }

    /// <summary>루트만 소유합니다. Chain에는 상속하지 않으며 효과의 전체 수명도 나타내지 않습니다.</summary>
    public sealed class CastCommit
    {
        public long CommitTick { get; }
        public long DueTick { get; }
        public CastCommitState State { get; private set; }
        internal DelayId DelayId;
        private Action _releasePending;
        internal CastCommit(long tick, byte delay, Action releasePending)
        { CommitTick = tick; DueTick = checked(tick + delay); _releasePending = releasePending; }
        internal void Publish() => State = CastCommitState.Committed;
        internal void BeginExecution() { State = CastCommitState.Executing; Release(); }
        internal void Returned() => State = CastCommitState.ExecutionReturned;
        internal void Failed() { State = CastCommitState.ExecutionException; Release(); }
        internal void Cancel() { State = CastCommitState.Cancelled; Release(); }
        private void Release() { var release = _releasePending; _releasePending = null; release?.Invoke(); }
    }
}
