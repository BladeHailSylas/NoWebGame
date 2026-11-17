using UIs;

namespace Systems.Time
{
    public static class Time
    {
        public static DelayScheduler DelayScheduler { get; internal set; }
        public static Ticker Ticker { get; internal set; }
        private static DebugOverlay _debugOverlay;
        private static Ticker _observedTicker;
        public static void Initialize()
        {
            if (_observedTicker != null) _observedTicker.OnTick -= TickHandler;
            _observedTicker = Ticker;
            if (_observedTicker != null) _observedTicker.OnTick += TickHandler;
            _debugOverlay = DebugOverlay.Instance;
        }

        internal static void Release(Ticker ticker, DelayScheduler scheduler)
        {
            if (_observedTicker == ticker && _observedTicker != null)
            {
                _observedTicker.OnTick -= TickHandler;
                _observedTicker = null;
                _debugOverlay = null;
            }
            if (Ticker == ticker) Ticker = null;
            if (DelayScheduler == scheduler) DelayScheduler = null;
            if (Systems.Time.Ticker.Instance == ticker) Systems.Time.Ticker.Instance = null;
            if (Systems.Time.DelayScheduler.Instance == scheduler) Systems.Time.DelayScheduler.Instance = null;
        }

        private static void TickHandler(long tick)
        {
            // Unity 오브젝트는 파괴 후에도 C# 참조가 남을 수 있습니다.
            if (!_debugOverlay) _debugOverlay = DebugOverlay.Instance;
            if (_debugOverlay && DelayScheduler != null)
                _debugOverlay.UpdateOverlay(tick, DelayScheduler.GetAllEntries());
        }
    }
}
