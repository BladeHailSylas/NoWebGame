using System;
using System.Collections.Generic;

namespace Systems.Time
{
    /// <summary>같은 세션의 절대 Tick으로 예약·조회합니다. 실행을 담당하지 않는 시간 판정 모듈입니다.</summary>
    public sealed class DelayScheduler
    {
        public static DelayScheduler Instance;
        private readonly Dictionary<DelayId, DelayEntry> _entries = new();
        // 다른 Scheduler의 옛 핸들이 새 예약을 조작하지 않도록 프로세스 안에서 번호를 공유합니다.
        private static long _nextId;

        public DelayScheduler() { Instance ??= this; }

        public DelayId Start(long currentTick, int baseDelayTick)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            if (baseDelayTick < 0) throw new ArgumentOutOfRangeException(nameof(baseDelayTick));
            var endTick = checked(currentTick + baseDelayTick);
            var id = new DelayId(checked(++_nextId));
            _entries.Add(id, new DelayEntry(endTick));
            return id;
        }

        public bool IsCompleted(DelayId id, long currentTick)
        {
            if (!_entries.TryGetValue(id, out var entry)) return false;
            return ObservedTick(currentTick, entry.DeltaTick) >= entry.EndTick;
        }

        /// <summary>양수는 만료를 앞당기고 음수는 늦춥니다. 조회로 값이 바뀌지 않습니다.</summary>
        public void ModifyDelta(DelayId id, int deltaTick)
        {
            if (!_entries.TryGetValue(id, out var entry)) return;
            entry.DeltaTick = checked(entry.DeltaTick + deltaTick);
            _entries[id] = entry;
        }

        public void Remove(DelayId id) => _entries.Remove(id);

        public long GetRemaining(DelayId id, long currentTick)
        {
            if (!_entries.TryGetValue(id, out var entry)) return 0;
            var observed = ObservedTick(currentTick, entry.DeltaTick);
            if (observed >= entry.EndTick) return 0;
            // 극단적인 음수 보정에서도 남은 시간 표시가 음수로 순환하지 않습니다.
            return observed < 0 && entry.EndTick > long.MaxValue + observed
                ? long.MaxValue : entry.EndTick - observed;
        }

        private static long ObservedTick(long currentTick, long delta)
        {
            if (currentTick < 0) throw new ArgumentOutOfRangeException(nameof(currentTick));
            return delta > 0 && currentTick > long.MaxValue - delta ? long.MaxValue : currentTick + delta;
        }

        public Dictionary<DelayId, (long, long)> GetAllEntries()
        {
            var result = new Dictionary<DelayId, (long, long)>();
            foreach (var (id, value) in _entries) result[id] = (value.EndTick, value.DeltaTick);
            return result;
        }

        private struct DelayEntry
        {
            public readonly long EndTick;
            public long DeltaTick;
            public DelayEntry(long endTick) { EndTick = endTick; DeltaTick = 0; }
        }
    }

    public readonly struct DelayId : IEquatable<DelayId>
    {
        private readonly long _value;
        public DelayId(long value) { _value = value; }
        public bool Equals(DelayId other) => _value == other._value;
        public override bool Equals(object obj) => obj is DelayId other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => $"DelayId({_value})";
    }
}
