#if UNITY_EDITOR
using global::NoWebGame.Characters;
using global::NoWebGame.Combat.Effects.Runtime;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Entities.Core;
using global::NoWebGame.Entities.Player;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Movement;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Time;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;


using UnityEngine.SceneManagement;








using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using GameTime = global::NoWebGame.Systems.Time.Time;

namespace ExitLab
{
    /// <summary>실제 Unity Collider/물리를 사용하는 회귀 시험. 결과는 저장소 밖 감사 폴더에 기록합니다.</summary>
    public sealed class TravelRegressionRunner : MonoBehaviour
    {
        private readonly List<string> _results = new();
        private readonly List<string> _errors = new();
        private void Start() { StartCoroutine(Run()); }
        private IEnumerator Run()
        {
            Application.logMessageReceived += OnLog;
            var success = false;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Physics2D.queriesHitTriggers = true;
                Physics2D.queriesStartInColliders = true;
                Physics2D.autoSyncTransforms = false;
                GameTime.Ticker = new Ticker();
                GameTime.DelayScheduler = new DelayScheduler();
                ManualActorChecks(false);
                ManualActorChecks(true);
                LifecycleChecks();
                ChainChecks();
                PrefabExecutionCheck();
                PlayerIntegrationCheck();
                Check(_errors.Count == 0, "no runtime errors: " + string.Join(" | ", _errors));
                success = true;
            }
            catch (Exception error) { _results.Add("FAIL: " + error); }
            if (success)
            {
                var suite = ProductionChecks();
                while (true)
                {
                    bool more;
                    try { more = suite.MoveNext(); }
                    catch (Exception error) { _results.Add("FAIL: " + error); success = false; break; }
                    if (!more) break;
                    yield return suite.Current;
                }
                (suite as IDisposable)?.Dispose();
            }
            if (_errors.Count > 0) { success = false; _results.Add("FAIL runtime logs: " + string.Join(" | ", _errors)); }
            File.WriteAllLines("../audit-results/target-refactor/integration-regression.txt", _results);
            Application.logMessageReceived -= OnLog;
            yield return null;
            EditorApplication.Exit(success ? 0 : 1);
        }

        private void ManualActorChecks(bool dash)
        {
            _results.Add("ACTOR: " + (dash ? "Dash" : "Projectile"));
            using (var f = new Fixture(dash))
            {
                f.Start(SkillTarget.At(new Vector2(.4f, 0)), 60);
                f.Tick();
                Check(!f.Result.HasValue, "T02 waits for physical movement");
                f.Physics();
                Check(f.Result?.Cause == ExitCause.PositionReached && Mathf.Abs(f.Result.Value.Position.x - .4f) < .001f, "T02 clamps and reaches position");
                f.Drain(); Check(f.Completed == 1 && f.Hits == 0 && f.Legacy == 0, "T02 exactly once classified chain");
            }
            using (var f = new Fixture(dash))
            {
                var e = f.Target(.05f, true);
                f.Start(SkillTarget.ForEntity(e)); f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.TargetHit && f.Hits == 1 && f.Completed == 1, "T06 hit plus proximity ends once");
                f.Cancel(); f.Drain();
                Check(f.Completed == 1 && f.Interrupted == 0, "T06 duplicate cancellation ignored");
            }
            foreach (var child in new[] { false, true })
            foreach (var pierce in new[] { false, true })
            using (var f = new Fixture(dash))
            {
                var e = f.Target(3, true, child);
                f.Start(SkillTarget.ForEntity(e), penetrative: pierce);
                for (var i = 0; i < 40; i++) f.Step();
                f.Drain();
                Check(f.Result?.Cause == ExitCause.TargetHit && f.Hits == 1 && f.Completed == 1,
                    $"TargetShape child={child} pierce={pierce}");
            }
            using (var f = new Fixture(dash))
            {
                var e = f.Target(.7f, true, true);
                var extra = new GameObject("second collider"); extra.transform.SetParent(e.transform, false);
                extra.layer = 7; extra.AddComponent<CircleCollider2D>().radius = .2f;
                f.Target(1.4f, true);
                f.Start(SkillTarget.At(new Vector2(3, 0)), penetrative: true);
                for (var i = 0; i < 40; i++) f.Step();
                f.Drain();
                Check(f.Result?.Cause == ExitCause.PositionReached && f.Hits == 2 && f.UniqueHitCount == 2,
                    "piercing continues and deduplicates per Entity");
            }
            using (var f = new Fixture(dash))
            {
                var e = f.Target(.4f, false);
                f.Start(SkillTarget.ForEntity(e), 60); f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.TargetNotHit && f.Interrupted == 1 && f.Hits == 0,
                    "Entity arrival without Collider is TargetNotHit, not fake onHit");
            }
            using (var f = new Fixture(dash))
            {
                var e = f.Target(.4f, false);
                f.Start(SkillTarget.ForEntity(e), 60); f.Tick();
                e.transform.position = new Vector2(2, 0); f.Physics();
                Check(!f.Result.HasValue && f.Hits == 0, "latest Entity position after physics prevents premature TargetNotHit");
                f.Step(); f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.TargetNotHit && f.Result.Value.Position.x > 1.9f, "tracking resumes after target moved");
            }
            using (var f = new Fixture(dash))
            {
                var e = f.Target(.7f, true, true);
                f.Start(SkillTarget.ForEntity(e), 60); f.Tick();
                e.transform.position = new Vector2(4, 0); f.Physics(); f.Drain();
                Check(f.Result?.Cause == ExitCause.TargetHit && f.Hits == 1 && f.Completed == 1,
                    "valid swept child Collider hit takes precedence over arrival overlap");
            }
            using (var f = new Fixture(dash))
            {
                var e = f.Target(4, false);
                f.Start(SkillTarget.ForEntity(e)); e.targetable = false; f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.TargetLost && f.Interrupted == 1 && f.Hits == 0, "TargetLost safely preserves invalid Entity");
                Check(f.LastInterrupted.Target.IsResolved && !f.LastInterrupted.Target.IsValid && f.LastInterrupted.Exit.HasValue,
                    "termination context preserves original target and exit position");
            }
            using (var f = new Fixture(dash))
            {
                f.Target(.7f, true); f.Start(SkillTarget.At(new Vector2(3, 0)), 60); f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.EnemyHit && f.Completed == 1 && f.Hits == 1, "non-target nonpiercing hit completes");
            }
            using (var f = new Fixture(dash))
            {
                f.Wall(.6f); f.Target(1.2f, true); f.Start(SkillTarget.At(new Vector2(3, 0)), 120, true); f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.WallBlocked && f.Hits == 0 && f.Interrupted == 1, "wall prevents hits behind it");
            }
            foreach (var legacy in new[] { false, true })
            foreach (var timeout in new[] { TimeoutPolicy.Completed, TimeoutPolicy.Interrupted })
            using (var f = new Fixture(dash))
            {
                f.Start(SkillTarget.At(new Vector2(100, 0)), lifetime: 16, legacy: legacy, timeout: timeout);
                for (var i = 0; i < 20; i++) f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.TimeExpired && (legacy ? f.Legacy == 1 && f.Completed + f.Interrupted == 0 :
                    f.Legacy == 0 && (timeout == TimeoutPolicy.Completed ? f.Completed == 1 : f.Interrupted == 1)),
                    $"timeout and exclusive callbacks legacy={legacy} policy={timeout}");
            }
        }

        private void LifecycleChecks()
        {
            using (var f = new Fixture())
            {
                var e = f.Target(.05f, true);
                f.Start(SkillTarget.ForEntity(e), legacy: true); f.Step(); f.Drain();
                Check(f.Hits == 1 && f.Legacy == 1 && f.ExitRegistrations == 1 && f.Completed + f.Interrupted == 0,
                    "T06 original Legacy onExpire registers and executes once");
            }
            using (var f = new Fixture(true))
            {
                f.Target(.7f, true);
                f.Start(SkillTarget.At(new Vector2(3, 0)), 6, true, legacy: true);
                for (var tick = 0; tick < 10; tick++) f.Step();
                Check(f.Body.position.x > .9f && f.Hits == 1 && !f.Result.HasValue,
                    "T07 original single enemy: 10 ticks move and hit once");
                for (var tick = 10; tick < 40; tick++) f.Step();
                Check(f.Result?.Cause == ExitCause.PositionReached && f.HitRegistrations == 1 && f.Legacy == 1 && f.ExitRegistrations == 1,
                    "T07 original single enemy: reaches goal and drains Legacy chain once");
            }
            foreach (var dash in new[] { false, true })
            {
                using (var f = new Fixture(dash))
                {
                    var e = f.Target(.7f, true, true);
                    f.Start(SkillTarget.ForEntity(e), 60, lifetime: 2, timeout: TimeoutPolicy.Interrupted);
                    f.Tick(); f.Tick(); f.Tick(); f.Physics(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.TargetHit && f.ExitRegistrations == 1 && f.HitRegistrations == 1,
                        $"swept hit survives expiry while awaiting physics {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    f.Start(SkillTarget.At(Vector2.zero)); f.Step(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.PositionReached && f.ExitRegistrations == 1, $"zero distance {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    var e = f.Target(0, false); f.Start(SkillTarget.ForEntity(e)); f.Step(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.TargetNotHit && f.HitRegistrations == 0, $"zero distance Entity {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    f.Start(SkillTarget.At(new Vector2(.4f, 0)), 60);
                    f.Tick(); f.Tick(); f.Tick();
                    Check(!f.Result.HasValue && f.Body.position == Vector2.zero, $"multiple ticks before physics {dash}");
                    f.Physics(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.PositionReached && f.ExitRegistrations == 1, $"single physics completion {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    f.Start(SkillTarget.At(new Vector2(4, 0)));
                    f.Body.gameObject.SetActive(false); f.Drain();
                    Check(f.Result?.Kind == ExitKind.Interrupted && f.Interrupted == 1 && f.ExitRegistrations == 1,
                        $"external disable exactly once {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    f.Start(SkillTarget.At(new Vector2(4, 0)), speed: 0); f.Step(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.MovementBlocked && f.ExitRegistrations == 1, $"zero speed does not stall {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    var e = f.Target(.4f, false); f.Start(SkillTarget.ForEntity(e), 60); f.Tick();
                    f.Physics(() => e.transform.position = new Vector2(2, 0));
                    Check(!f.Result.HasValue && f.HitRegistrations == 0, $"latest position changed after Simulate {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    var e = f.Target(.4f, true, true);
                    e.GetComponentInChildren<Collider2D>().transform.localPosition = new Vector2(0, 3);
                    f.Start(SkillTarget.ForEntity(e), 60); f.Step(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.TargetNotHit && f.Hits == 0, $"matching Transform with offset Collider is not a hit {dash}");
                }
                foreach (var lifetime in new ushort[] { 0, 1 })
                using (var f = new Fixture(dash))
                {
                    f.Start(SkillTarget.At(new Vector2(100, 0)), lifetime: lifetime, timeout: TimeoutPolicy.Interrupted);
                    f.Step(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.TimeExpired && f.Interrupted == 1 && f.ExitRegistrations == 1,
                        $"short lifetime {lifetime}, actor={dash}");
                }
                using (var f = new Fixture(dash))
                {
                    f.Start(SkillTarget.At(new Vector2(4, 0)));
                    Object.DestroyImmediate(f.Body.gameObject); f.Drain();
                    Check(f.Result?.Kind == ExitKind.Interrupted && f.ExitRegistrations == 1, $"external Destroy once {dash}");
                }
                using (var f = new Fixture(dash))
                {
                    var e = f.Target(4, false); f.Start(SkillTarget.ForEntity(e)); Object.DestroyImmediate(e.gameObject);
                    f.Step(); f.Drain();
                    Check(f.Result?.Cause == ExitCause.TargetLost && f.Interrupted == 1, $"destroyed target after launch {dash}");
                }
            }
            using (var f = new Fixture(true))
            {
                f.Start(SkillTarget.At(new Vector2(4, 0))); f.Immovable = 1; f.Step(); f.Drain();
                Check(f.Result?.Cause == ExitCause.MovementBlocked && !f.Mover.PreventingActivation && !f.Mover.IsDashing,
                    "CC clears Dash contract and activation lock");
            }
            using (var f = new Fixture(true))
            {
                f.Start(SkillTarget.At(new Vector2(4, 0))); f.Tick();
                var teleport = new TeleportMechanism();
                f.Mover.Teleport(new TeleportContract(new CastContext(teleport, f.Body.transform, SkillTarget.At(Vector2.zero), default,
                    originCastPosition: f.Body.transform.position)));
                f.Physics(); f.Drain();
                Check(f.Result?.Cause == ExitCause.Teleported && f.Body.position == Vector2.zero && f.ExitRegistrations == 1,
                    "Teleport cancellation removes pending Dash movement");
            }
            using (var f = new Fixture(true))
            {
                f.Start(SkillTarget.At(new Vector2(4, 0)));
                var mech = (DashMechanism)f.Context.Mech;
                var next = new CastContext(mech, f.Context.Caster, SkillTarget.At(new Vector2(.2f, 0)), default,
                    originCastPosition: f.Context.Caster.position);
                f.Mover.StartDash(new DashContract(next, GameTime.Ticker.TickCount + 120, 60, true, true, mech.onHit, mech.onExpire, false));
                f.Step(); f.Drain();
                Check(f.Interrupted == 1 && f.LastInterrupted.Exit?.Cause == ExitCause.ReplacedByDash && f.Completed == 1,
                    "Dash replacement terminates old contract and preserves new one");
            }
        }

        private void ChainChecks()
        {
            using (var f = new Fixture())
            {
                var e = f.Target(3, false); f.Start(SkillTarget.ForEntity(e)); e.targetable = false; f.Step(); f.Drain();
                var endPoint = default(Vector2); var seen = false; var live = false;
                var positional = new Marker { action = ctx => { seen = true; live = ctx.Target.TryGetPosition(out endPoint); } };
                SkillUtils.ActivateChain(new[] { new MechanismData { mechanism = positional, targetSource = ChainTargetSource.ExitPosition } }, f.LastInterrupted);
                SkillUtils.ActivateChain(new[] { new MechanismData { mechanism = new DamageMechanism() } }, f.LastInterrupted);
                f.Drain();
                Check(seen && live && endPoint == f.Result.Value.Position, "TargetLost follow-up explicitly uses exit Position");
                Check(_errors.Count == 0, "invalid Entity damage follow-up is safe");
                var old = endPoint;
                f.Context.Caster.position = new Vector2(5, 0);
                SkillUtils.ActivateChain(new[] { new MechanismData { mechanism = positional, mode = TargetMode.TowardsSelf,
                    requireRetarget = true, targetSource = ChainTargetSource.ExitPosition } }, f.LastInterrupted);
                f.Drain();
                Check(endPoint == new Vector2(5, 0) && endPoint != old, "explicit retarget takes precedence over exit Position");
            }
            using (var f = new Fixture())
            {
                var e = f.Target(2, false);
                var observedEntity = Vector2.zero; var observedPosition = Vector2.zero;
                var entityMarker = new Marker { delay = 2, action = ctx => ctx.Target.TryGetPosition(out observedEntity) };
                var positionMarker = new Marker { delay = 2, action = ctx => ctx.Target.TryGetPosition(out observedPosition) };
                CommandBridge.Enqueue(new SkillCommand(f.Caster, TargetMode.TowardsEntity, default, entityMarker, default, SkillTarget.ForEntity(e)));
                CommandBridge.Enqueue(new SkillCommand(f.Caster, TargetMode.TowardsCoordinate, default, positionMarker, default, SkillTarget.At(new Vector2(2, 0))));
                f.Tick(); e.transform.position = new Vector2(5, 0); f.Tick(); f.Tick(); f.Tick();
                Check(observedEntity == new Vector2(5, 0) && observedPosition == new Vector2(2, 0), "delayed Entity tracks and delayed Position stays fixed");
            }
        }

        private void PlayerIntegrationCheck()
        {
            var go = new GameObject("synthetic Player"); go.SetActive(false);
            var spec = ScriptableObject.CreateInstance<CharacterSpec>();
            spec.baseHp = 100; spec.CharacterVariables = new();
            try
            {
                go.AddComponent<Rigidbody2D>(); go.AddComponent<CircleCollider2D>().isTrigger = true;
                go.AddComponent<TargetResolver>(); go.AddComponent<CommandCollector>();
                var player = go.AddComponent<PlayerEntity>(); player.InstallSpec(spec); go.SetActive(true);
                var mover = (Mover)typeof(PlayerEntity).GetField("_mover", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                var mech = new DashMechanism { durationTicks = 120, speed = 60, callbackMode = ExitCallbackMode.Classified };
                mech.Execute(new CastContext(mech, go.transform, SkillTarget.At(new Vector2(.4f, 0)), default,
                    originCastPosition: go.transform.position));
                GameTime.Ticker.Step(); Physics2D.Simulate(1f / 60); TravelPhysicsObserver.NotifyManualSimulationCompleted();
                Check(mover.LastDashResult?.Cause == ExitCause.PositionReached,
                    "DashMechanism -> PlayerEntity -> ActBridge -> Mover integration (" + mover.LastDashResult?.Cause + ")");
                mech.Execute(new CastContext(mech, go.transform, SkillTarget.At(new Vector2(4, 0)), default,
                    originCastPosition: go.transform.position));
                go.SetActive(false);
                Check(mover.LastDashResult?.Cause == ExitCause.OwnerDisabled && !mover.IsDashing, "Player disable clears Dash");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(spec); }
        }

        private void PrefabExecutionCheck()
        {
            using (var f = new Fixture())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExitLab/Generated/MinimalProjectile.prefab");
                Check(prefab != null, "existing ExitLab projectile prefab available");
                var completions = 0;
                var mech = new ProjectileMechanism { projectilePrefab = prefab.GetComponent<ProjectileEntity>(), speed = 60, lifeTick = 120,
                    callbackMode = ExitCallbackMode.Classified,
                    onCompleted = new List<MechanismData> { new() { mechanism = new Marker { action = _ => completions++ } } } };
                mech.Execute(new CastContext(mech, f.Caster, SkillTarget.At(new Vector2(.4f, 0)), default,
                    originCastPosition: f.Caster.position));
                var projectile = Object.FindFirstObjectByType<ProjectileEntity>();
                try
                {
                    f.Step(); f.Drain();
                    Check(projectile.Result?.Cause == ExitCause.PositionReached && completions == 1,
                        "ProjectileMechanism Execute -> existing prefab Instantiate -> Init");
                    var callback = (Delegate)typeof(Ticker).GetField("OnTick", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GameTime.Ticker);
                    var remains = false;
                    if (callback != null) foreach (var item in callback.GetInvocationList()) if (ReferenceEquals(item.Target, projectile)) remains = true;
                    Check(!remains, "finished Projectile has no Tick subscription");
                }
                finally { if (projectile) { projectile.Cancel(ExitCause.Cancelled, false); Object.DestroyImmediate(projectile.gameObject); } }
            }
        }

        private IEnumerator ProductionChecks()
        {
            foreach (var dash in new[] { false, true })
            using (var f = new Fixture(dash))
            {
                var scene = SceneManager.CreateScene("Travel unload " + dash);
                f.Start(SkillTarget.At(new Vector2(4, 0)));
                SceneManager.MoveGameObjectToScene(f.Body.gameObject, scene);
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (!unload.isDone) yield return null;
                f.Drain();
                Check(f.ExitRegistrations == 0, $"scene unload does not enqueue gameplay chain {dash}");
            }
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
            var service = new GameObject("Production Ticker"); service.AddComponent<TickerRunner>();
            try
            {
                foreach (var dash in new[] { false, true })
                foreach (var entity in new[] { false, true })
                using (var f = new Fixture(dash))
                {
                    var target = entity ? SkillTarget.ForEntity(f.Target(.4f, false)) : SkillTarget.At(new Vector2(.4f, 0));
                    f.Start(target, 60);
                    var deadline = UnityEngine.Time.realtimeSinceStartup + 5;
                    while (!f.Result.HasValue && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                    Check(f.Result?.Cause == (entity ? ExitCause.TargetNotHit : ExitCause.PositionReached),
                        $"Production T14 actor={dash} entity={entity}");
                    var tick = GameTime.Ticker.TickCount;
                    while (GameTime.Ticker.TickCount < tick + 3 && UnityEngine.Time.realtimeSinceStartup < deadline) yield return null;
                    Check(f.ExitRegistrations == 1 && f.Completed + f.Interrupted == 1 && f.Hits == 0,
                        $"Production chain drain actor={dash} entity={entity}");
                }
            }
            finally { Object.DestroyImmediate(service); Physics2D.simulationMode = SimulationMode2D.Script; }
        }

        private void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
            _results.Add("PASS: " + label);
        }
        private void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(condition);
        }

        [Serializable]
        public sealed class Marker : INewMechanism
        {
            public Action<CastContext> action;
            public byte delay;
            public short CooldownTicks => 0;
            public byte DelayTicks => delay;
            public float MinRange => 0;
            public float MaxRange => 100;
            public LayerMask Mask => 1 << 7;
            public void Execute(CastContext ctx) => action?.Invoke(ctx);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<GameObject> objects = new();
            private readonly GameObject caster;
            private readonly bool dash;
            private readonly CommandCollector collector;
            public Transform Caster => caster.transform;
            public CastContext Context;
            public Mover Mover;
            public byte Immovable;
            public int HitRegistrations, ExitRegistrations;
            public Rigidbody2D Body;
            public int UniqueHitCount => dash ? Mover.UniqueDashHitCount : Projectile.UniqueHitCount;
            public ProjectileEntity Projectile;
            public TravelResult? Result => dash ? Mover.LastDashResult : Projectile.Result;
            public int Hits, Completed, Interrupted, Legacy;
            public CastContext LastInterrupted;
            public Fixture(bool dash = false)
            {
                this.dash = dash;
                caster = Make("caster");
                var services = Make("collector");
                services.AddComponent<TargetResolver>(); collector = services.AddComponent<CommandCollector>();
                if (dash) GameTime.Ticker.OnTick += DashTick;
            }
            private GameObject Make(string name) { var go = new GameObject(name); objects.Add(go); return go; }
            public Entity Target(float x, bool collider, bool child = false)
            {
                var go = Make("target"); go.transform.position = new Vector2(x, 0); go.layer = 7;
                var entity = go.AddComponent<ExitLabEntity>();
                if (collider)
                {
                    var host = go;
                    if (child) { host = new GameObject("child"); host.transform.SetParent(go.transform, false); host.layer = 7; }
                    var col = host.AddComponent<CircleCollider2D>(); col.radius = .2f; col.isTrigger = true;
                }
                return entity;
            }
            public void Wall(float x)
            {
                var wall = Make("wall"); wall.layer = 3; wall.transform.position = new Vector2(x, 0);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 3);
            }
            public void Start(SkillTarget target, int speed = 6, bool penetrative = false, ushort lifetime = 120,
                bool legacy = false, TimeoutPolicy timeout = TimeoutPolicy.Completed)
            {
                var go = Make(dash ? "dash body" : "projectile"); Body = go.AddComponent<Rigidbody2D>();
                var collider = go.AddComponent<CircleCollider2D>(); collider.radius = dash ? .5f : .1f; collider.isTrigger = true;
                var onHit = Chain(_ => Hits++); var onExpire = Chain(_ => Legacy++);
                var onCompleted = Chain(_ => Completed++);
                var onInterrupted = Chain(ctx => { Interrupted++; LastInterrupted = ctx; });
                collector.CommandEnqueued += cmd => {
                    if (ReferenceEquals(cmd.Mech, onHit[0].mechanism)) HitRegistrations++;
                    else if (ReferenceEquals(cmd.Mech, onExpire[0].mechanism) || ReferenceEquals(cmd.Mech, onCompleted[0].mechanism) ||
                        ReferenceEquals(cmd.Mech, onInterrupted[0].mechanism)) ExitRegistrations++;
                };
                if (dash)
                {
                    var mech = new DashMechanism { speed = speed, penetrative = penetrative, durationTicks = lifetime,
                        callbackMode = legacy ? ExitCallbackMode.Legacy : ExitCallbackMode.Classified, timeoutPolicy = timeout,
                        onHit = onHit, onExpire = onExpire, onCompleted = onCompleted, onInterrupted = onInterrupted };
                    var ctx = new CastContext(mech, caster.transform, target, default, originCastPosition: caster.transform.position);
                    Context = ctx;
                    Mover = new Mover(null, null, Body, collider);
                    Mover.StartDash(new DashContract(ctx, GameTime.Ticker.TickCount + lifetime, speed, true, penetrative, onHit, onExpire, false));
                }
                else
                {
                    Projectile = go.AddComponent<ProjectileEntity>();
                    var mech = new ProjectileMechanism { speed = speed, penetrative = penetrative, lifeTick = lifetime,
                        callbackMode = legacy ? ExitCallbackMode.Legacy : ExitCallbackMode.Classified, timeoutPolicy = timeout,
                        onHit = onHit, onExpire = onExpire, onCompleted = onCompleted, onInterrupted = onInterrupted };
                    Context = new CastContext(mech, caster.transform, target, default, originCastPosition: caster.transform.position);
                    Projectile.Init(Context);
                }
                Physics2D.SyncTransforms();
            }
            private static List<MechanismData> Chain(Action<CastContext> action) => new() { new() { mechanism = new Marker { action = action } } };
            private void DashTick(long tick) { Mover?.DashTick(tick, Immovable); }
            public void Cancel() { if (dash) Mover.CancelDash(); else Projectile.Cancel(); }
            public void Tick() => GameTime.Ticker.Step();
            public void Physics(Action afterSimulation = null)
            {
                Physics2D.SyncTransforms();
                if (!Physics2D.Simulate(1f / 60)) throw new Exception("Simulation failed");
                afterSimulation?.Invoke();
                TravelPhysicsObserver.NotifyManualSimulationCompleted();
            }
            public void Step() { Tick(); Physics(); }
            public void Drain() { Step(); Step(); }
            public void Dispose()
            {
                if (dash) { GameTime.Ticker.OnTick -= DashTick; Mover?.CancelDash(ExitCause.Cancelled, false); }
                if (Projectile) Projectile.Cancel(ExitCause.Cancelled, false);
                for (var i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
#endif
