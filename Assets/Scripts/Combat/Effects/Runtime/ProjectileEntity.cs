using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves.ObjectEntity;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Spatial;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using global::NoWebGame.Movement.Physics;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Systems.Time;




using UnityEngine;
using GameTime = global::NoWebGame.Systems.Time.Time;

namespace NoWebGame.Combat.Effects.Runtime
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.ObjectEntity", "Assembly-CSharp", "ProjectileEntity")]
    public class ProjectileEntity : ObjectPrefab
    {
        private const byte LegacyMinimumLife = 15;
        private int _lifeTick;
        private int _speed;
        private FixedVector2 _location;
        private ThinMotor _motor;
        private TravelMotion _travel;
        private TravelCompletion _completion;
        private TravelPhysicsObserver _observer;
        private Ticker _ticker;
        private CastContext _ctx;
        private ProjectileMechanism _settings;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private Collider2D col;
        public TravelResult? Result => _completion?.Result;
        public int UniqueHitCount => _travel?.UniqueHitCount ?? 0;

        public void Init(CastContext ctx)
        {
            if (_completion != null || ctx.Mech is not ProjectileMechanism settings) return;
            if (!SpatialExecution.TryPrepare(ctx, out ctx, out _)) { Destroy(gameObject); return; }
            _ctx = ctx; _settings = settings; _completion = new TravelCompletion();
            rb = rb ? rb : GetComponent<Rigidbody2D>();
            col = col ? col : GetComponent<Collider2D>();
            _speed = settings.speed;
            _location = new FixedVector2(rb.position);
            _motor = new ThinMotor(rb, col) { wallMask = LayerMask.GetMask("Walls&Obstacles"), enemyMask = LayerMask.GetMask("Foe") };
            _travel = _motor.CreateTravel(ctx, _speed, settings.penetrative,
                entity => SkillUtils.ActivateChain(settings.onHit, ctx, SkillTarget.ForEntity(entity), trigger: "onHit"));
            _observer = GetComponent<TravelPhysicsObserver>();
            if (!_observer) _observer = gameObject.AddComponent<TravelPhysicsObserver>();
            _observer.AfterPhysics = AfterPhysics;
            _observer.Disabled = publish => Cancel(ExitCause.ExternallyDestroyed, publish);
            _ticker = GameTime.Ticker;
            if (!_ctx.Destination.HasValue && !_ctx.Target.IsValid) { Cancel(ExitCause.TargetLost); return; }
            // 기존 콘텐츠의 15 Tick 미만 즉시 만료는 Legacy 모드에만 유지합니다.
            if (settings.lifeTick == 0 || (settings.callbackMode == ExitCallbackMode.Legacy && settings.lifeTick < LegacyMinimumLife))
            { Finish(Timeout()); return; }
            if (_ticker == null) { Cancel(ExitCause.Cancelled); return; }
            _ticker.OnTick += TickHandler;
        }

        private void TickHandler(long tick)
        {
            if (Result.HasValue) return;
            // 이동 구간에서 확인한 명중을 시간 만료나 도착 겹침 검사로 덮어쓰지 않습니다.
            if (_travel.AwaitingPhysics) { _lifeTick++; return; }
            if (!_ctx.Destination.HasValue && !_ctx.Target.IsValid) { Cancel(ExitCause.TargetLost); return; }
            if (_lifeTick >= _settings.lifeTick) { Finish(Timeout()); return; }
            var result = _motor.Move(_travel);
            _lifeTick++;
            if (result.HasValue) Finish(result.Value);
        }

        public void AfterPhysics()
        {
            if (_travel == null || Result.HasValue) return;
            _location = new FixedVector2(rb.position);
            var result = _travel.AfterPhysics();
            if (result.HasValue) Finish(result.Value);
        }
        private TravelResult Timeout() => new(
            _settings.timeoutPolicy == TimeoutPolicy.Completed
                ? ExitKind.Completed : ExitKind.Interrupted, ExitCause.TimeExpired, rb.position);

        public void Cancel(ExitCause cause = ExitCause.Cancelled, bool publish = true)
        {
            if (_completion == null) return;
            Finish(new TravelResult(ExitKind.Interrupted, cause, rb ? rb.position : (Vector2)transform.position), publish);
        }
        private void Finish(TravelResult result, bool publish = true)
        {
            _completion.TryFinish(result, () =>
            {
                if (_ticker != null) _ticker.OnTick -= TickHandler;
                if (_observer) { _observer.AfterPhysics = null; _observer.Disabled = null; }
            }, final =>
            {
                AttackLog.Write(_ctx.Trace, "TravelEnded", $"source={_ctx.Source} destination={_ctx.Destination} kind={final.Kind} cause={final.Cause} position={final.Position} hit={AttackLog.Identity(final.HitEntity)} publish={publish}");
                try
                {
                    if (publish && !TravelPhysicsObserver.IsShuttingDown)
                        TravelCallbacks.Publish(_ctx, final, _settings.callbackMode,
                            _settings.onExpire, _settings.onCompleted, _settings.onInterrupted);
                }
                finally { if (gameObject) Destroy(gameObject); }
            });
        }
        private void OnDisable() => Cancel(ExitCause.ExternallyDestroyed,
            !TravelPhysicsObserver.IsShuttingDown && gameObject.scene.isLoaded);
        private void OnDestroy() => Cancel(ExitCause.ExternallyDestroyed,
            !TravelPhysicsObserver.IsShuttingDown && gameObject.scene.isLoaded);
    }
}
