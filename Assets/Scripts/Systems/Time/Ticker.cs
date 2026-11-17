using System;

namespace Systems.Time
{
    /// <summary>전투 세션의 단조 증가 시계. 지속시간과 달리 절대 Tick은 long으로 전달합니다.</summary>
    public sealed class Ticker
    {
        public const byte TicksPerSecond = 60;
        public const byte TickIntervalMs = 1000 / TicksPerSecond;
        public static Ticker Instance { get; internal set; }
        public event Action<long> OnTick;
        public long TickCount { get; private set; }

        public Ticker() { Instance ??= this; }

        public void Schedule(byte ticksFromNow, Action<long> action)
        {
            if (ticksFromNow == 0) throw new ArgumentOutOfRangeException(nameof(ticksFromNow));
            if (action == null) throw new ArgumentNullException(nameof(action));
            var targetTick = checked(TickCount + ticksFromNow);
            OnTick += Handler;
            void Handler(long currentTick)
            {
                if (currentTick < targetTick) return;
                // 콜백이 예외를 던져도 같은 예약이 다음 Tick에 다시 실행되지 않습니다.
                OnTick -= Handler;
                action(currentTick);
            }
        }

        public void Step()
        {
            // long의 한계에서도 0으로 되돌아가지 않고 명시적으로 실패합니다.
            TickCount = checked(TickCount + 1);
            OnTick?.Invoke(TickCount);
        }
    }
}
