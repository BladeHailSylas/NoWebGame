using System;
using System.Collections.Generic;
using System.Linq;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Stack;
using Systems.Time;
using UnityEngine;
using Time = Systems.Time.Time;

namespace PlayerScripts.Skills
{
    /// <summary>루트 Commit과 Continuation 수집을 분리하며, 명령별 실패와 취소를 일회성으로 정리합니다.</summary>
    public class CommandCollector : MonoBehaviour, ICommandPort
    {
        private SkillRunner _runner;
        private DelayScheduler _scheduler;
        private Ticker _ticker;
        private bool _ready, _processing;
        private List<SkillCommand> _collecting = new();
        private List<SkillCommand> _resolving = new();
        private readonly List<ScheduledCommand> _scheduled = new();
        public long Generation { get; private set; }
        public static CommandCollector Instance { get; private set; }
        public event Action<SkillCommand> CommandEnqueued;

        private void OnEnable()
        {
            if (_ready) return;
            _scheduler = Time.DelayScheduler; _ticker = Time.Ticker;
            if (_scheduler == null || _ticker == null) { Debug.LogError("CommandCollector requires an active clock and scheduler.", this); return; }
            _runner = new SkillRunner(GetComponent<TargetResolver>(), new EffectDisplayer());
            _ticker.OnTick += TickHandler; _ready = true; Instance = this;
        }
        private void OnDisable()
        {
            _ready = false; Generation++;
            if (_ticker != null) _ticker.OnTick -= TickHandler;
            if (ReferenceEquals(Instance, this)) Instance = null;
            CeaseCommand("CollectorDisabled");
        }
        private void OnDestroy()
        {
            _ready = false;
            if (_ticker != null) _ticker.OnTick -= TickHandler;
            if (ReferenceEquals(Instance, this)) Instance = null;
            CeaseCommand("CollectorDestroyed");
        }
        internal bool CanAcceptRoot(DelayScheduler scheduler) => _ready && isActiveAndEnabled && _scheduler != null &&
            ReferenceEquals(_scheduler, scheduler) && ReferenceEquals(_ticker, Time.Ticker);
        public bool TryPrepareRoot(in SkillCommand request, out SkillCommand prepared, out string reason)
        {
            prepared = request;
            if (_runner == null) { reason = "CollectorNotInitialized"; return false; }
            return _runner.TryPrepareRoot(request, out prepared, out reason);
        }
        internal bool TryAcceptRoot(SkillCommand command, Action applyResources, Action rollbackResources, out string reason)
        {
            reason = "CollectorUnavailable";
            var root = command.Commit;
            if (!_ready || !isActiveAndEnabled || root == null || root.State != CastCommitState.Prepared) return false;
            var added = false;
            try
            {
                var invalid = root.ValidatePending();
                if (invalid != null) { reason = invalid; root.Cancel(invalid); return false; }
                root.DelayId = _scheduler.Start(root.CommitTick, command.Mech.DelayTicks);
                _collecting.Add(command); added = true;
                applyResources();
                root.Publish();
            }
            catch (Exception error)
            {
                try { rollbackResources(); }
                finally
                {
                    if (added) _collecting.RemoveAll(c => ReferenceEquals(c.Commit, root));
                    _scheduler.Remove(root.DelayId); root.Failed("CommitException");
                }
                reason = "CommitFailed:" + error.GetType().Name;
                AttackLog.Write(command.Trace, "CommitRejected", $"reason={reason} {root.Describe()}");
                return false;
            }
            reason = "Allowed";
            AttackLog.Write(command.Trace, "Committed", root.Describe());
            Notify(command);
            return true;
        }

        public void EnqueueCommand(SkillCommand cmd) => TryEnqueueCommand(cmd);
        public bool TryEnqueueCommand(SkillCommand cmd)
        {
            if (cmd.Commit != null) return Reject(cmd, "RootRequiresCommitAdmission");
            if (!_ready) return Reject(cmd, "CollectorUnavailable");
            if (!cmd.Caster || cmd.Mech == null) return Reject(cmd, "MissingCasterOrMechanism");
            StackManager.ConsumptionChange consumption = null;
            var added = false;
            try
            {
                if (cmd.Mech is SwitchMechanism sw)
                {
                    var selection = new SwitchSelection(sw, cmd.Var);
                    if (!selection.CanAccept(cmd.Var)) return Reject(cmd, "VariableUnavailable");
                    cmd = cmd.WithSwitchSelection(selection);
                }
                _collecting.Add(cmd); added = true;
                consumption = cmd.SwitchSelection?.Begin(cmd.Var, _ticker.TickCount);
                consumption?.Complete();
            }
            catch (Exception error)
            {
                try { consumption?.Rollback(); }
                finally { if (added) _collecting.RemoveAt(_collecting.Count - 1); }
                ReportException(cmd, "AdmissionException", error);
                return false;
            }
            Notify(cmd); return true;
        }
        private static bool Reject(SkillCommand cmd, string reason)
        { AttackLog.Write(cmd.Trace, "CommandDropped", $"reason={reason}"); return false; }
        private void Notify(SkillCommand cmd)
        {
            AttackLog.Write(cmd.Trace, "Enqueued", $"mechanism={cmd.Mech?.GetType().Name} target={cmd.Target} collector={GetInstanceID()} commit={cmd.Commit?.CommitTick}");
            if (CommandEnqueued == null) return;
            foreach (Action<SkillCommand> observer in CommandEnqueued.GetInvocationList())
                try { observer(cmd); }
                catch (Exception error)
                {
                    AttackLog.Write(cmd.Trace, "ObservationException", $"type={error.GetType().Name} {cmd.Commit?.Describe()}");
                    Debug.LogException(error);
                }
        }
        private void TickHandler(long tick)
        {
            if (!_ready || _processing) return;
            _processing = true;
            var generation = Generation;
            try
            {
                (_collecting, _resolving) = (_resolving, _collecting);
                // 콜백 전에 현재 항목을 분리합니다. 재진입 취소가 남은 목록을 바꿔도 열거자를 깨지 않습니다.
                while (_ready && generation == Generation && _resolving.Count > 0)
                {
                    var cmd = _resolving[0]; _resolving.RemoveAt(0);
                    var delay = default(DelayId);
                    ScheduledCommand scheduled = null;
                    try
                    {
                        if (!ValidateRoot(cmd)) continue;
                        delay = cmd.Commit?.DelayId ?? _scheduler.Start(tick, cmd.Mech.DelayTicks);
                        scheduled = new ScheduledCommand { Command = cmd, DelayId = delay };
                        _scheduled.Add(scheduled);
                        AttackLog.Write(cmd.Trace, "Scheduled", $"startTick={cmd.Commit?.CommitTick ?? tick} {cmd.Commit?.Describe()}");
                    }
                    catch (Exception error)
                    {
                        if (scheduled != null) _scheduled.Remove(scheduled);
                        _scheduler.Remove(delay);
                        if (cmd.Commit != null) _scheduler.Remove(cmd.Commit.DelayId);
                        ReportException(cmd, "ScheduleException", error);
                    }
                }
                for (var i = 0; _ready && generation == Generation && i < _scheduled.Count;)
                {
                    var entry = _scheduled[i];
                    try
                    {
                        if (entry.Command.Commit != null && entry.Command.Commit.State != CastCommitState.Committed)
                        { _scheduled.RemoveAt(i); _scheduler.Remove(entry.DelayId); continue; }
                        if (!_scheduler.IsCompleted(entry.DelayId, tick)) { i++; continue; }
                    }
                    catch (Exception error)
                    { _scheduled.RemoveAt(i); _scheduler.Remove(entry.DelayId); ReportException(entry.Command, "ScheduleException", error); continue; }
                    _scheduled.RemoveAt(i); _scheduler.Remove(entry.DelayId);
                    try
                    {
                        if (!ValidateRoot(entry.Command)) continue;
                        var root = entry.Command.Commit;
                        if (root != null && !root.BeginExecution()) continue;
                        AttackLog.Write(entry.Command.Trace, "Dispatch", $"tick={tick} {root?.Describe()}");
                        if (_runner.TryActivate(entry.Command)) root?.Returned();
                        else root?.Aborted("ExecutionRejected");
                        AttackLog.Write(entry.Command.Trace, "DispatchFinished", root?.Describe() ?? "continuation=true");
                    }
                    catch (Exception error) { ReportException(entry.Command, "ExecutionException", error); }
                }
            }
            finally { _processing = false; }
        }
        private bool ValidateRoot(SkillCommand cmd)
        {
            if (cmd.Commit == null) return true;
            if (cmd.Commit.State != CastCommitState.Committed) { _scheduler.Remove(cmd.Commit.DelayId); return false; }
            var invalid = cmd.Commit.ValidatePending();
            if (invalid == null) return true;
            CancelRoot(cmd, invalid); return false;
        }
        private static void ReportException(SkillCommand cmd, string stage, Exception error)
        {
            cmd.Commit?.Failed(stage);
            AttackLog.Write(cmd.Trace, stage, $"type={error.GetType().Name} message={error.Message} {cmd.Commit?.Describe()}");
            Debug.LogException(error);
        }
        private void CancelRoot(SkillCommand cmd, string reason)
        {
            _scheduler?.Remove(cmd.Commit.DelayId);
            if (cmd.Commit.Cancel(reason)) AttackLog.Write(cmd.Trace, "Cancelled", cmd.Commit.Describe());
        }
        public void CancelPending(Transform owner, string reason)
        {
            bool Match(SkillCommand cmd) => cmd.Commit != null && cmd.Caster == owner;
            var removed = _collecting.Concat(_resolving).Concat(_scheduled.Select(s => s.Command)).Where(Match).ToArray();
            _collecting.RemoveAll(c => Match(c)); _resolving.RemoveAll(c => Match(c));
            _scheduled.RemoveAll(s => Match(s.Command));
            foreach (var cmd in removed) CancelRoot(cmd, reason);
        }
        public void CeaseCommand() => CeaseCommand("CeaseAllCommands");
        private void CeaseCommand(string reason)
        {
            var commands = _collecting.Concat(_resolving).Concat(_scheduled.Select(s => s.Command)).ToArray();
            var delays = _scheduled.Select(s => s.DelayId).ToArray();
            _collecting.Clear(); _resolving.Clear(); _scheduled.Clear();
            foreach (var delay in delays) _scheduler?.Remove(delay);
            foreach (var cmd in commands)
            {
                if (cmd.Commit != null) CancelRoot(cmd, reason);
                else AttackLog.Write(cmd.Trace, "Cancelled", $"reason={reason} continuation=true");
            }
        }
        private sealed class ScheduledCommand
        {
            public SkillCommand Command;
            public DelayId DelayId;
        }
    }
}
