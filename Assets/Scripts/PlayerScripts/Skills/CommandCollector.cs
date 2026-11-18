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
    /// <summary>
    /// 루트는 Commit 시 예약한 Delay를 사용하고, 외부/Chain은 기존 수집 Tick에서 예약합니다.
    /// 이중 버퍼는 실행 중 생긴 자식이 같은 순회에서 재귀 실행되는 것을 방지합니다.
    /// </summary>
    public class CommandCollector : MonoBehaviour, ICommandPort
    {
        private SkillRunner _runner;
        private DelayScheduler _scheduler;
        private Ticker _ticker;
        private bool _ready;
        private List<SkillCommand> _collecting = new();
        private List<SkillCommand> _resolving = new();
        private readonly List<ScheduledCommand> _scheduled = new();
        public static CommandCollector Instance { get; private set; }
        public event Action<SkillCommand> CommandEnqueued;

        private void OnEnable()
        {
            _runner = new SkillRunner(GetComponent<TargetResolver>(), new EffectDisplayer());
            _scheduler = Time.DelayScheduler; _ticker = Time.Ticker;
            _ticker.OnTick += TickHandler; _ready = true; Instance = this;
        }
        private void OnDisable()
        {
            _ready = false;
            if (_ticker != null) _ticker.OnTick -= TickHandler;
            // 시전자별 수명 사건 연결은 Phase 4에서 수행합니다.
        }
        internal bool CanAcceptRoot(DelayScheduler scheduler) => _ready && isActiveAndEnabled &&
            ReferenceEquals(_scheduler, scheduler) && ReferenceEquals(_ticker, Time.Ticker);
        public bool TryPrepareRoot(in SkillCommand request, out SkillCommand prepared, out string reason)
        {
            prepared = request;
            if (_runner == null) { reason = "CollectorNotInitialized"; return false; }
            return _runner.TryPrepareRoot(request, out prepared, out reason);
        }

        // 단일 Unity 스레드에서만 호출합니다. 외부 콜백은 모든 내부 변경이 성공한 뒤 통지합니다.
        internal bool TryAcceptRoot(SkillCommand command, Action applyResources, Action rollbackResources, out string reason)
        {
            reason = "CollectorUnavailable";
            var root = command.Commit;
            if (!_ready || !isActiveAndEnabled || root == null || root.State != CastCommitState.Prepared) return false;
            var added = false;
            try
            {
                root.DelayId = _scheduler.Start(root.CommitTick, command.Mech.DelayTicks);
                _collecting.Add(command); added = true;
                applyResources();
                root.Publish();
            }
            catch (Exception error)
            {
                rollbackResources();
                if (added) _collecting.RemoveAt(_collecting.Count - 1);
                _scheduler.Remove(root.DelayId);
                reason = "CommitFailed:" + error.GetType().Name;
                return false;
            }
            reason = "Allowed";
            AttackLog.Write(command.Trace, "Committed", $"commitTick={root.CommitTick} dueTick={root.DueTick}");
            Notify(command);
            return true;
        }

        public void EnqueueCommand(SkillCommand cmd)
        {
            // Committed 루트 복사본을 다시 넣어 비용 없이 재실행할 수 없습니다.
            if (cmd.Commit != null) { AttackLog.Write(cmd.Trace, "CommandDropped", "reason=RootRequiresCommitAdmission"); return; }
            if (!_ready) { AttackLog.Write(cmd.Trace, "CommandDropped", "reason=CollectorUnavailable"); return; }
            StackManager.ConsumptionChange consumption = null;
            if (cmd.Mech is SwitchMechanism sw)
            {
                var selection = new SwitchSelection(sw, cmd.Var);
                if (!selection.CanAccept(cmd.Var)) { AttackLog.Write(cmd.Trace, "CommandDropped", "reason=VariableUnavailable"); return; }
                cmd = cmd.WithSwitchSelection(selection);
            }
            var added = false;
            try
            {
                _collecting.Add(cmd); added = true;
                consumption = cmd.SwitchSelection?.Begin(cmd.Var, _ticker.TickCount);
                consumption?.Complete();
            }
            catch
            {
                consumption?.Rollback();
                if (added) _collecting.RemoveAt(_collecting.Count - 1);
                throw;
            }
            Notify(cmd);
        }
        private void Notify(SkillCommand cmd)
        {
            // 관측 콜백의 예외로 이미 성립한 Commit을 되돌리지 않습니다.
            AttackLog.Write(cmd.Trace, "Enqueued", $"mechanism={cmd.Mech?.GetType().Name} target={cmd.Target} collector={GetInstanceID()} commit={cmd.Commit?.CommitTick}");
            if (CommandEnqueued == null) return;
            foreach (Action<SkillCommand> observer in CommandEnqueued.GetInvocationList())
                try { observer(cmd); }
                catch (Exception error) { Debug.LogException(error); }
        }
        private void TickHandler(long tick)
        {
            (_collecting, _resolving) = (_resolving, _collecting);
            foreach (var cmd in _resolving)
            {
                if (cmd.Commit != null && cmd.Commit.State != CastCommitState.Committed) continue;
                var delay = cmd.Commit?.DelayId ?? _scheduler.Start(tick, cmd.Mech.DelayTicks);
                _scheduled.Add(new ScheduledCommand { Command = cmd, DelayId = delay });
                AttackLog.Write(cmd.Trace, "Scheduled", $"delayTicks={cmd.Mech.DelayTicks} startTick={cmd.Commit?.CommitTick ?? tick}");
            }
            _resolving.Clear();
            for (var i = 0; i < _scheduled.Count;)
            {
                var entry = _scheduled[i];
                if (!_scheduler.IsCompleted(entry.DelayId, tick)) { i++; continue; }
                // Pending을 풀고 실행에 넘긴 명령을 다시 실행하지 않도록 먼저 큐에서 분리합니다.
                _scheduled.RemoveAt(i); _scheduler.Remove(entry.DelayId);
                entry.Command.Commit?.BeginExecution();
                try
                {
                    AttackLog.Write(entry.Command.Trace, "Dispatch", $"tick={tick}");
                    _runner.Activate(entry.Command);
                    entry.Command.Commit?.Returned();
                }
                catch { entry.Command.Commit?.Failed(); throw; }
            }
        }
        public void CeaseCommand()
        {
            foreach (var cmd in _collecting.Concat(_resolving).Concat(_scheduled.Select(s => s.Command)))
            {
                if (cmd.Commit != null) { _scheduler.Remove(cmd.Commit.DelayId); cmd.Commit.Cancel(); }
                AttackLog.Write(cmd.Trace, "Cancelled", "reason=CeaseAllCommands scope=AllCasters");
            }
            foreach (var entry in _scheduled) _scheduler.Remove(entry.DelayId);
            _scheduled.Clear(); _collecting.Clear(); _resolving.Clear();
        }
        private struct ScheduledCommand
        {
            public SkillCommand Command;
            public DelayId DelayId;
        }
    }
}
