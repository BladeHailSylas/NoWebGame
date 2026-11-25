using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Stacks.Runtime;




namespace NoWebGame.Combat.Casting
{
    /// <summary>큐 수락 때 선택한 분기와 소비 정책을 고정합니다. Variable 자료는 CastContext.Var에 남습니다.</summary>
    public sealed class SwitchSelection
    {
        public MechanismData Branch { get; }
        public bool Consume { get; }
        internal SwitchSelection(SwitchMechanism mechanism, SwitchVariable variable)
        {
            Consume = mechanism.Consume;
            var branch = mechanism.defaultFollowUp;
            if (variable.Variable && mechanism.cases != null)
                foreach (var item in mechanism.cases)
                    if (item?.variable == variable.Variable) { branch = item.followUp; break; }
            if (branch != null) Branch = new MechanismData { mechanism = branch.mechanism, mode = branch.mode,
                requireRetarget = branch.requireRetarget, respectBusy = branch.respectBusy, targetSource = branch.targetSource };
        }
        internal bool CanAccept(SwitchVariable variable) => !Consume || !variable.Variable ||
            (variable.Consumption != null && variable.Consumption.Key.def == variable.Variable &&
             variable.Consumption.Amount == variable.Amount && variable.Consumption.CanConsume);
        internal global::NoWebGame.Combat.Stacks.Runtime.StackManager.ConsumptionChange Begin(SwitchVariable variable, long tick)
            => Consume && variable.Variable ? variable.Consumption.Begin(tick) : null;
    }
}
