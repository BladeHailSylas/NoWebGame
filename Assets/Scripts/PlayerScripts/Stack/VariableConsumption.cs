using System;
using Systems.Stacks;

namespace PlayerScripts.Stack
{
    /// <summary>Chain은 분기 자료와 함께 이 증표를 공유합니다. 한번 지불한 증표는 재소비하지 않습니다.</summary>
    public sealed class VariableConsumption
    {
        private readonly StackManager _owner;
        public StackKey Key { get; }
        public int Amount { get; }
        public bool Consumed { get; private set; }
        internal VariableConsumption(StackManager owner, StackKey key, int amount)
        { _owner = owner; Key = key; Amount = amount; }
        internal bool CanConsume => Consumed || (_owner != null && _owner.CanConsumeExact(Key, Amount));
        internal StackManager.ConsumptionChange Begin(long tick)
            => Consumed ? null : _owner.BeginConsumption(this, tick);
        internal void MarkConsumed() => Consumed = true;
    }
}
