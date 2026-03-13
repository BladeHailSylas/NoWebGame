using UIs;

namespace Systems.Time
{
    public static class Time
    {
        public static DelayScheduler DelayScheduler { get; internal set; }
        public static Ticker Ticker { get; internal set; }
        private static DebugOverlay _debugOverlay;
        public static void Initialize()
        {
            Ticker.OnTick += TickHandler;
            _debugOverlay = DebugOverlay.Instance;
        }

        private static void TickHandler(ushort tick)
        {
            _debugOverlay ??= DebugOverlay.Instance;
            var entries = DelayScheduler.GetAllEntries();
            _debugOverlay?.UpdateOverlay(tick, entries);
        }
    }
}