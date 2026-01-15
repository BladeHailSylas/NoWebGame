using System.Collections.Generic;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stats;
using Systems.Anchor;
using Systems.Data;
using Systems.SubSystems;
using UnityEngine;

namespace PlayerScripts.Acts
{
    /// <summary>
    /// Handles locomotion and vulnerability logic for the player character. The
    /// logic is kept free from MonoBehaviour dependencies so it can be simulated in
    /// unit tests.
    /// </summary>
    public sealed class Mover
    {
        private readonly Context _context;
        private readonly StatsBridge _stats;
        private readonly FixedMotor _motor;
        private readonly Teleporter _teleporter;
        private Vector2 _moveVector;
        private DashContract? _dashContract;
        private HashSet<Entity> _dashHits;
        //private 
        public bool PreventingActivation { get; private set; }

        public Mover(Context context, StatsBridge stats, Rigidbody2D rb, Collider2D col)
        {
            _context = context;
            _stats = stats;
            _motor = new FixedMotor(rb, col);
            _teleporter = new Teleporter(rb, col);
        }

        public void Teleport(TeleportContract tpc)
        {
            EndDash();
            if (_teleporter.TryTeleport(tpc))
            {
                _motor.Depenetrate();
            }
        }

        public void StartDash(DashContract dashContract)
        {
            if (_dashContract.HasValue)
            {
                EndDash();
            }
            _dashContract = dashContract;
            _dashHits = new HashSet<Entity>();
            if (_dashContract is { PreventActivation: true })
            {
                PreventingActivation = true;
            }
        }

        private void EndDash()
        {
            _dashHits = null;
            PreventingActivation = false;
            if (_dashContract is null) return;
            CastExpire(_dashContract.Value.Context);
            _dashContract = null;

        }

        /// <summary>
        /// Moves the player in the provided direction. Movement honours the effect
        /// system, so immobilizing conditions are respected.
        /// </summary>
        public void MakeMove(FixedVector2 move, byte immovableCount)
        {
            if (immovableCount > 0 || _dashContract.HasValue)
            {
                _moveVector = Vector2.zero;
                return;
            }
            _motor.Depenetrate(); //Always depenetrate, even when neutral(Enemies can overlap the player)
            _moveVector = move.AsVector2;
            if (!(_moveVector.sqrMagnitude > 1e-6f)) return;
            var speed = _stats.Stats.Speed;
            _motor.Move(move.Normalized * speed);
            _motor.Depenetrate();
        }

        public void DashTick(ushort tick, byte immovableCount)
        {
            if (immovableCount > 0 || !_dashContract.HasValue || tick >= _dashContract.Value.EndTick)
            {
                EndDash();
                _motor.Depenetrate();
                return;
            }
            var cont = _motor.TryDash(_dashContract.Value, _dashHits);
            if (cont) return;
            EndDash();
            _motor.Depenetrate();
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            //Future hook: integrate with locomotion buffer when implemented.
            _context.Logger.Info($"Knockback requested direction={direction}, force={force}.");
        }
        private void CastExpire(CastContext ctx)
        {
            if (ctx.Params is not DashParams param) return;
            if (param.onExpire.Count == 0)
            {
                if (!ctx.Target.TryGetComponent<SkillAnchor>(out var anchor)) return;
                AnchorRegistry.Instance.Return(anchor);
                return;
            }

            SkillUtils.ActivateFollowUp(param.onExpire, ctx);
        }
    }

    /// <summary>
    /// Transform, TargetMode, ushort, bool, bool, DamageData, List INewMechanism, List INewMechanism, bool
    /// </summary>
    public readonly struct DashContract
    {
        public readonly CastContext Context;
        public readonly ushort EndTick;
        public readonly int Speed;
        public readonly bool PreventActivation;
        public readonly bool Penetrative;
        public readonly List<MechanismRef> OnHit;
        public readonly List<MechanismRef> OnExpire;
        public readonly bool ExpireWhenUnexpected;
        public DashContract(CastContext context, ushort endTick, int speed, bool preventActivation,
            bool penetrative, List<MechanismRef> onHit, List<MechanismRef> onExpire,
            bool unexpected)
        {
            Context = context;
            EndTick = endTick;
            Speed = speed;
            PreventActivation = preventActivation;
            Penetrative = penetrative;
            OnHit = onHit;
            OnExpire = onExpire;
            ExpireWhenUnexpected = unexpected;
        }
    }

    public readonly struct TeleportContract
    {
        public readonly CastContext Context;

        public TeleportContract(CastContext ctx)
        {
            Context = ctx;
        }
    }
}
