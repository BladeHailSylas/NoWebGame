namespace Systems.Time
{
    public static class Time
    {
        public static DelayScheduler DelayScheduler { get; internal set; }
        public static Ticker Ticker { get; internal set; }
    }
}