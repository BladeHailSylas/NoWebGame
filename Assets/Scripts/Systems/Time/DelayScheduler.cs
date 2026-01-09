using System;
using System.Collections.Generic;

namespace Systems.Time
{
    /// <summary>
    /// DelayScheduler는 Tick 기반 시간 제약을 관리하는 순수 판정 모듈이다.
    /// - 행동을 실행하지 않는다
    /// - Tick을 증가시키지 않는다
    /// - 완료 여부만 계산한다
    /// </summary>
    public sealed class DelayScheduler
    {
        public static DelayScheduler Instance;
        private readonly Dictionary<DelayId, DelayEntry> _entries = new();
        private int _nextId = 1;

        public DelayScheduler()
        {
            Instance ??= this;
        }

        /// <summary>
        /// 새로운 Delay를 등록하고 DelayId를 반환한다.
        /// </summary>
        public DelayId Start(int currentTick, int baseDelayTick)
        {
            int endTick;

            // overflow 안전 처리
            try
            {
                checked
                {
                    endTick = currentTick + baseDelayTick;
                }
            }
            catch (OverflowException)
            {
                endTick = int.MaxValue;
            }

            var id = new DelayId(_nextId++);
            _entries[id] = new DelayEntry(endTick);

            return id;
        }

        /// <summary>
        /// 해당 Delay가 완료되었는지 판정한다.
        /// 순수 질의 함수이며 내부 상태를 변경하지 않는다.
        /// </summary>
        public bool IsCompleted(DelayId id, int currentTick)
        {
            if (!_entries.TryGetValue(id, out var entry)) return false; // 이미 제거되었거나 존재하지 않음
            int observedTick;
            try
            {
                checked
                {
                    observedTick = currentTick + entry.DeltaTick;
                }
            }
            catch (OverflowException)
            {
                observedTick = int.MaxValue;
            }

            return observedTick >= entry.EndTick;
        }

        /// <summary>
        /// Delay의 체감 시간을 조정한다.
        /// endTick은 변경하지 않고 deltaTick만 누적한다.
        /// </summary>
        public void ModifyDelta(DelayId id, int deltaTick)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return;

            entry.DeltaTick += deltaTick;
            _entries[id] = entry;
        }

        /// <summary>
        /// Delay를 제거한다.
        /// 제거된 DelayId는 다시 사용할 수 없다.
        /// </summary>
        public void Remove(DelayId id)
        {
            _entries.Remove(id);
        }

        /// <summary>
        /// 디버그 / UI 용: 남은 Tick 반환
        /// </summary>
        public int GetRemaining(DelayId id, int currentTick)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return 0;

            var observedTick = currentTick + entry.DeltaTick;
            var remaining = entry.EndTick - observedTick;
            return remaining < 0 ? 0 : remaining;
        }

        /// <summary>
        /// 내부 저장용 Delay 정보
        /// </summary>
        private struct DelayEntry
        {
            public readonly int EndTick;
            public int DeltaTick;

            public DelayEntry(int endTick)
            {
                EndTick = endTick;
                DeltaTick = 0;
            }
        }
    }

    /// <summary>
    /// DelayScheduler가 발행하는 시간 제약 핸들
    /// </summary>
    public readonly struct DelayId : IEquatable<DelayId>
    {
        private readonly int _value;

        public DelayId(int value)
        {
            _value = value;
        }

        public bool Equals(DelayId other) => _value == other._value;
        public override bool Equals(object obj) => obj is DelayId other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => $"DelayId({_value})";
    }
}