using Moves;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using Systems.Data;
using UnityEngine;

namespace PlayerScripts.Acts
{
    /// <summary>
    /// Pure C# binder that stores the latest player input values and routes them to
    /// the appropriate player modules. The actual Unity input callbacks are
    /// invoked from <see cref="PlayerEntity"/>, keeping this component free from
    /// Unity lifecycle dependencies for easier testing.
    /// </summary>
    public sealed class ActBridge
    {
        private readonly Mover _mover;
        private readonly Attacker _attacker;
        private Vector2 _inputVector;
        private int _innoxiousCount;
        private int _immovableCount;
        private long _tick;
        public bool CanAttack => _innoxiousCount == 0 && !(_mover?.PreventingActivation ?? false);
        public bool CanMove => _immovableCount == 0;
    
        public ActBridge(Mover mover, Attacker attacker)
        {
            _mover = mover;
            _attacker = attacker;
        }

        /// <summary>
        /// Records movement input from the player.
        /// </summary>
        public void SetMovementInput(Vector2 input)
        {
            _inputVector = input;
        }

        /// <summary>
        /// Clears the cached movement input, usually when the input action is cancelled.
        /// </summary>
        public void ClearMovementInput()
        {
            _inputVector = Vector2.zero;
        }

        public void AddDashContract(DashContract contract)
        {
            // 새로 생성된 Player는 아직 Tick을 받지 않았을 수 있습니다.
            var now = Systems.Time.Time.Ticker?.TickCount ?? _tick;
            var dashContract = new DashContract(
                contract.Context, contract.EndTick + now, contract.Speed, contract.PreventActivation, contract.Penetrative,
                contract.OnHit, contract.OnExpire, contract.ExpireWhenUnexpected
            );
            _mover.StartDash(dashContract);
        }

        public void CancelDash(bool publish = true) => _mover.CancelDash(ExitCause.OwnerDisabled, publish);

        public void AddTeleportContract(TeleportContract tpc)
        {
            _mover.Teleport(tpc);
        }

        /// <summary>
        /// Called every tick by <see cref="PlayerEntity"/> to process movement.
        /// </summary>
        public void Tick(long tick)
        {
            _tick = tick;
            var immovable = _immovableCount > 0 ? (byte)1 : (byte)0;
            _mover.MakeMove(new FixedVector2(_inputVector), immovable);
            _mover.DashTick(tick, immovable);
            _attacker.Tick(tick, _innoxiousCount > 0 ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Prepares an attack for the specified slot. Preparation is separated from
        /// execution so we can later inject UI feedback or casting bars.
        /// </summary>
        public void PrepareAttack(SkillSlot slot)
        {
            _attacker.PrepareCast(slot, _innoxiousCount > 0 ? (byte)1 : (byte)0);
            //Preparing might be allowed, but block for now
        }

        /// <summary>
        /// Attempts to execute the attack bound to the provided slot.
        /// </summary>
        public void ExecuteAttack(SkillSlot slot)
        {
            var trace = _attacker.BeginInput(slot);
            if (_mover.PreventingActivation) { AttackLog.Write(trace, "InputRejected", "reason=DashPreventsActivation"); return; }
            _attacker.EnqueueCast(slot, null, trace);
        }

        /// <summary>AI의 논리적 Attack 입력. 기존 Dash/행동 불가/지연 처리 경로를 그대로 통과합니다.</summary>
        public void ExecuteAttack(SkillSlot slot, ExplicitAttackTarget target)
        {
            var trace = _attacker.BeginInput(slot, target?.Trace ?? default);
            if (_mover.PreventingActivation || !CanAttack || target == null)
            { AttackLog.Write(trace, "InputRejected", $"reason={(_mover.PreventingActivation ? "DashPreventsActivation" : !CanAttack ? "CrowdControl" : "MissingExplicitTarget")}"); return; }
            _attacker.EnqueueCast(slot, target, trace);
        }

        public void ApplyCC(CCData cc)
        {
            switch (cc.Type)
            {
                case EffectType.Rooted:
                    Immovable();
                    break;
                case EffectType.Suppressed:
                    Innoxious();
                    break;
                case EffectType.Stunned:
                case EffectType.Tumbled:
                    Immovable();
                    Innoxious();
                    break;
            }
        }
        public void RemoveCC(CCData cc)
        {
            switch (cc.Type)
            {
                case EffectType.Rooted:
                    _immovableCount = Mathf.Max(0, _immovableCount - 1);
                    break;
                case EffectType.Suppressed:
                    _innoxiousCount = Mathf.Max(0, _innoxiousCount - 1);
                    break;
                case EffectType.Stunned:
                case EffectType.Tumbled:
                    _innoxiousCount = Mathf.Max(0, _innoxiousCount - 1);
                    _immovableCount = Mathf.Max(0, _immovableCount - 1);
                    break;
            }
        }
        private void Immovable()
        {
            _immovableCount += 1;
        }

        private void Innoxious()
        {
            _innoxiousCount += 1;
            _attacker?.CancelPending("AttackDisabledByCC");
        }
    }

    public readonly struct CCData
    {
        public readonly EffectType Type;
        public readonly byte Value;

        public CCData(EffectType type, byte value)
        {
            Type = type;
            Value = value;
        }
    }
}
