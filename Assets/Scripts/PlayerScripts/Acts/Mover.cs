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
        private TravelMotion _dashTravel;
        private TravelCompletion _dashCompletion;
        private readonly Rigidbody2D _body;
        private readonly TravelPhysicsObserver _observer;
        public TravelResult? LastDashResult { get; private set; }
        public bool IsDashing => _dashContract.HasValue;
        public int UniqueDashHitCount => _dashTravel?.UniqueHitCount ?? 0;
        public bool PreventingActivation { get; private set; }

        public Mover(Context context, StatsBridge stats, Rigidbody2D rb, Collider2D col)
        {
            _body = rb;
            _observer = rb.GetComponent<TravelPhysicsObserver>();
            if (!_observer) _observer = rb.gameObject.AddComponent<TravelPhysicsObserver>();
            _context = context;
            _stats = stats;
            _motor = new FixedMotor(rb, col);
            _teleporter = new Teleporter(rb, col);
        }

        public void Teleport(TeleportContract tpc)
        {
            if (!SpatialExecution.TryPrepare(tpc.Context, out var prepared, out _)) return;
            tpc = new TeleportContract(prepared);
            CancelDash(ExitCause.Teleported);
            if (_teleporter.TryTeleport(tpc))
            {
                _motor.Depenetrate();
            }
        }

        public void StartDash(DashContract dashContract)
        {
            if (!SpatialExecution.TryPrepare(dashContract.Context, out var prepared, out _)) return;
            dashContract = new DashContract(prepared, dashContract.EndTick, dashContract.Speed, dashContract.PreventActivation,
                dashContract.Penetrative, dashContract.OnHit, dashContract.OnExpire, dashContract.ExpireWhenUnexpected);
            CancelDash(ExitCause.ReplacedByDash);
            _dashContract = dashContract;
            LastDashResult = null;
            _dashCompletion = new TravelCompletion();
            _dashTravel = _motor.CreateTravel(dashContract);
            PreventingActivation = dashContract.PreventActivation;
            _observer.AfterPhysics = AfterDashPhysics;
            _observer.Disabled = publish => CancelDash(ExitCause.OwnerDisabled, publish);
            if (!dashContract.Context.Destination.HasValue && !dashContract.Context.Target.IsValid) CancelDash(ExitCause.TargetLost);
        }

        public void CancelDash(ExitCause cause = ExitCause.Cancelled, bool publish = true)
        {
            if (!_dashContract.HasValue) return;
            FinishDash(new TravelResult(ExitKind.Interrupted, cause, _body.position), publish);
        }

        private void FinishDash(TravelResult result, bool publish = true)
        {
            if (!_dashContract.HasValue) return;
            var contract = _dashContract.Value;
            var settings = contract.Context.Mech as DashMechanism;
            _dashCompletion.TryFinish(result, () =>
            {
                // 콜백이 새로운 Dash를 시작해도 이전 계약의 정리가 새 계약을 지우지 않습니다.
                if (_dashTravel.AwaitingPhysics && _body) _body.MovePosition(_body.position);
                _dashContract = null;
                LastDashResult = result;
                PreventingActivation = false;
                _observer.AfterPhysics = null;
                _observer.Disabled = null;
            }, final =>
            {
                AttackLog.Write(contract.Context.Trace, "TravelEnded", $"source={contract.Context.Source} destination={contract.Context.Destination} kind={final.Kind} cause={final.Cause} position={final.Position} hit={AttackLog.Identity(final.HitEntity)} publish={publish}");
                if (publish && settings != null && !TravelPhysicsObserver.IsShuttingDown)
                    TravelCallbacks.Publish(contract.Context, final, settings.callbackMode,
                        contract.OnExpire, settings.onCompleted, settings.onInterrupted);
            });
        }

        public void AfterDashPhysics()
        {
            if (!_dashContract.HasValue) return;
            var result = _dashTravel.AfterPhysics();
            if (result.HasValue) FinishDash(result.Value);
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
            var speed = _stats.GetSpeed();
            var vector = move.Normalized * speed;
            _motor.Move(vector);
            _motor.Depenetrate();
        }

        public void DashTick(long tick, byte immovableCount)
        {
            if (!_dashContract.HasValue) return;
            if (immovableCount > 0) { CancelDash(ExitCause.MovementBlocked); return; }
            if (_dashTravel.AwaitingPhysics) return;
            var contract = _dashContract.Value;
            if (!contract.Context.Destination.HasValue && !contract.Context.Target.IsValid) { CancelDash(ExitCause.TargetLost); return; }
            if (tick >= contract.EndTick)
            {
                var settings = contract.Context.Mech as DashMechanism;
                var kind = settings == null ||
                    settings.timeoutPolicy == TimeoutPolicy.Completed ? ExitKind.Completed : ExitKind.Interrupted;
                FinishDash(new TravelResult(kind, ExitCause.TimeExpired, _body.position));
                return;
            }
            var result = _motor.MoveDash(_dashTravel);
            if (result.HasValue) FinishDash(result.Value);
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            //Future hook: integrate with locomotion buffer when implemented.
            _context.Logger.Info($"Knockback requested direction={direction}, force={force}.");
        }
    }

    /// <summary>
    /// Transform, TargetMode, ushort, bool, bool, DamageData, List INewMechanism, List INewMechanism, bool
    /// </summary>
    public readonly struct DashContract
    {
        public readonly CastContext Context;
        public readonly long EndTick;
        public readonly int Speed;
        public readonly bool PreventActivation;
        public readonly bool Penetrative;
        public readonly List<MechanismData> OnHit;
        public readonly List<MechanismData> OnExpire;
        public readonly bool ExpireWhenUnexpected;
        public DashContract(CastContext context, long endTick, int speed, bool preventActivation,
            bool penetrative, List<MechanismData> onHit, List<MechanismData> onExpire,
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
