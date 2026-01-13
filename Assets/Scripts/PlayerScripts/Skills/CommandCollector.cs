using System;
using System.Collections.Generic;
using System.Linq;
using Moves;
using UnityEngine;
using Systems.Time;
using Time = Systems.Time.Time;

namespace PlayerScripts.Skills
{
    /// <summary>
    /// CommandCollector (기술 딜레이 전용)
    ///
    /// 합의된 처리 모델:
    /// - EnqueueCommand는 "수집(collecting)"만 수행한다. (DelayScheduler.Start 호출 금지)
    /// - TickHandler 진입 시 collecting/resolving swap으로 이번 Tick 처리 대상을 확정한다.
    /// - resolving의 모든 Command에 대해 Delay를 스케줄하고 scheduled에 등록한다.
    /// - scheduled를 검사하여 Delay 완료된 Command는 즉시 실행(Activate)하고 executed에 담는다.
    /// - scheduled 순회가 끝난 뒤 executed를 통해 scheduled에서 제거한다. (foreach 안전)
    /// - 0틱 지연도 동일 파이프라인을 거쳐 "다음 Tick에서" 실행되도록 강제된다.
    /// </summary>
    public class CommandCollector : MonoBehaviour
    {
        private SkillRunner _runner;
        private DelayScheduler _scheduler;
        private Ticker _ticker;

        // 이중 버퍼: "수집 중" / "이번 Tick에 해결(Resolve)할 입력"
        private List<SkillCommand> _collecting = new();
        private List<SkillCommand> _resolving = new();

        // Delay 진행 중인 Command들
        private readonly List<ScheduledCommand> _scheduled = new();

        public static CommandCollector Instance { get; private set; }

        private void OnEnable()
        {
            _runner = new SkillRunner(GetComponent<TargetResolver>());
            _scheduler = Time.DelayScheduler;
            _ticker = Time.Ticker;

            _ticker.OnTick += TickHandler;
            Instance = this;
        }

        private void OnDisable()
        {
            _ticker.OnTick -= TickHandler;
        }

        /// <summary>
        /// 기술 시전 요청.
        /// - 여기서는 "허용 여부"를 판단하지 않음 (Attacker 책임)
        /// - DelayScheduler를 호출하지 않고, 이번 Tick의 collecting 버퍼에 수집만 한다.
        /// </summary>
        public void EnqueueCommand(SkillCommand cmd)
        {
            // TickHandler 실행 중에 호출되어도 안전:
            // TickHandler는 _resolving만 처리하며, Enqueue는 _collecting에만 쌓이기 때문.
            _collecting.Add(cmd);
            //Debug.Log($"Command {cmd.Mech} came from {cmd.Caster} towards {cmd.Target} using {cmd.TargetMode}");
        }

        private void TickHandler(ushort tick)
        {
            // 0) 이번 Tick 처리 입력 확정
            (_collecting, _resolving) = (_resolving, _collecting);
            
            // 1) resolving -> scheduled (Delay Schedule)
            if (_resolving.Count > 0)
            {
                foreach (var cmd in _resolving)
                {
                    // Params가 delayTick을 소유한다는 합의 반영
                    var delayTick = cmd.Params.DelayTicks;

                    // Start는 TickHandler에서만 호출한다 (시간 통보 모델)
                    var delayId = _scheduler.Start(tick, delayTick);

                    _scheduled.Add(new ScheduledCommand
                    {
                        Command = cmd,
                        DelayId = delayId
                    });
                }

                // 이번 Tick에 수집된 입력 처리 완료
                _resolving.Clear();
            }
            
            // 2) scheduled 검사 -> 즉시 실행 + executed 수집
            if (_scheduled.Count == 0) return;

            // foreach 안전을 위해 지역 리스트로 "이번 Tick에 실행된 것"을 수집
            var executedIndices = new List<int>(4);


            for (var i = 0; i < _scheduled.Count; i++)
            {
                var entry = _scheduled[i];

                if (!_scheduler.IsCompleted(entry.DelayId, tick))
                    continue;

                // Delay 종료 처리
                if (!entry.DelayId.Equals(default))
                    _scheduler.Remove(entry.DelayId);

                // 즉시 실행
                _runner.Activate(entry.Command);

                // 실행된 위치 기록
                executedIndices.Add(i);
            }

            
            // 3) executed 기반으로 scheduled 정리
            if (executedIndices.Count > 0)
            {
                // List.Remove는 요소 개수만큼 선형 탐색이지만,
                // scheduled 규모가 작을 것으로 예상되므로 단순함/안전성을 우선한다.
                for (var i = executedIndices.Count - 1; i >= 0; i--)
                {
                    var index = executedIndices[i];
                    _scheduled.RemoveAt(index);
                }

            }
        }

        /// <summary>
        /// 시전 중인 모든 기술 취소.
        /// (CC / 행동 불능 대응)
        /// - 진행 중이던 Delay 전부 제거
        /// - collecting/resolving/scheduled 전부 초기화
        /// </summary>
        public void CeaseCommand()
        {
            // scheduled에 등록된 Delay 제거
            foreach (var entry in _scheduled)
            {
                if (!entry.DelayId.Equals(default))
                    _scheduler.Remove(entry.DelayId);
            }

            _scheduled.Clear();
            _collecting.Clear();
            _resolving.Clear();
        }

        /// <summary>
        /// Delay가 이미 시작된 Command의 저장 단위.
        /// - struct로 둬서 GC를 줄이고, 값 비교(Remove)도 쉽게 한다.
        /// </summary>
        private struct ScheduledCommand : IEquatable<ScheduledCommand>
        {
            public SkillCommand Command;
            public DelayId DelayId;

            public bool Equals(ScheduledCommand other)
            {
                return Command.Equals(other.Command) && DelayId.Equals(other.DelayId);
            }

            public override bool Equals(object obj)
            {
                return obj is ScheduledCommand other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Command, DelayId);
            }
        }
    }
}
