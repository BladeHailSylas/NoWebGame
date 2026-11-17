#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Moves;
using Moves.Mechanisms;
using Moves.ObjectEntity;
using PlayerScripts.Acts;
using PlayerScripts.Skills;
using Systems.Data;
using Systems.Time;
using Systems.SubSystems;
using UnityEngine;
using GameTime = Systems.Time.Time;

namespace ExitLab
{
    public enum ExitCase { T01, T02, T06, T07, T14, TargetShape }
    public enum ExitActor { Projectile, Dash }
    public enum ExitClock { ManualTickThenPhysics, ProductionCoroutine }
    public enum ExitTarget { TransformOnly, EntityAndCollider, ChildCollider }

    /// <summary>
    /// 편집기 전용 수동 실험실. 기존 Mechanism/Motor는 수정하지 않습니다.
    /// Play 진입만으로는 시작하지 않으며, 한 번 실행한 뒤 반드시 Play를 종료합니다.
    /// </summary>
    public sealed class ExitLabHarness : MonoBehaviour
    {
        public ExitCase testId = ExitCase.T01;
        public ExitActor actor = ExitActor.Projectile;
        public ExitClock clock = ExitClock.ManualTickThenPhysics;
        public ExitTarget targetShape = ExitTarget.TransformOnly;
        public ProjectileEntity projectilePrefab;
        public Transform coordinateTarget;
        public Transform entityTarget;
        public Transform childColliderTarget;
        public Transform intermediateEnemy;
        [Tooltip("T14 및 TargetShape의 속도. T01/T02/T06/T07은 명세 preset을 사용합니다.")]
        public int speed = 6;
        public Vector2 targetPosition = new Vector2(3, 0);
        public bool penetrative;

        private Ticker ticker;
        private Mover mover;
        private ProjectileEntity projectile;
        private Rigidbody2D body;
        private Transform target;
        private CommandCollector collector;
        private Marker hitMarker, expireMarker;
        private StreamWriter output;
        private bool started, faulted;
        private int fixedSteps, hitEnqueued, expireEnqueued;
        private SimulationMode2D previousSimulation;
        private float previousFixedDelta, previousTimeScale;
        private bool previousTriggers, previousStartInside, previousAutoSync;
        private ushort endTick;

        // 읽기 전용 private 필드 관측. 필드명이 바뀌면 조용히 추측하지 않고 실패합니다.
        internal static object Read(object instance, string field)
        {
            if (instance == null) return null;
            var info = instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(instance.GetType().Name, field);
            return info.GetValue(instance);
        }

        public void Begin()
        {
            if (!Application.isPlaying || started) return;
            if (UnityEngine.SceneManagement.SceneManager.sceneCount != 1 ||
                gameObject.scene.path != "Assets/ExitLab/Generated/ProjectileDashExit.unity")
                throw new InvalidOperationException("ExitLab: 생성된 격리 씬 하나만 열어야 합니다.");
            if (UnityEditor.EditorSettings.enterPlayModeOptionsEnabled &&
                (UnityEditor.EditorSettings.enterPlayModeOptions & UnityEditor.EnterPlayModeOptions.DisableDomainReload) != 0)
                throw new InvalidOperationException("ExitLab: Reload Domain을 활성화해야 합니다.");
            if (LayerMask.NameToLayer("Walls&Obstacles") != 3 || LayerMask.NameToLayer("Foe") != 7)
                throw new InvalidOperationException("ExitLab: 기존 레이어 설정이 명세와 다릅니다.");
            if (!projectilePrefab || !coordinateTarget || !entityTarget || !childColliderTarget || !intermediateEnemy)
                throw new InvalidOperationException("ExitLab: fixture 참조 누락. Build 메뉴로 별도 씬을 생성하세요.");
            if (FindObjectsByType<TickerRunner>(FindObjectsSortMode.None).Length != 0 ||
                FindObjectsByType<CommandCollector>(FindObjectsSortMode.None).Length != 0 ||
                Ticker.Instance != null || GameTime.Ticker != null)
                throw new InvalidOperationException("ExitLab: 별도 씬과 Domain Reload가 필요합니다. 기존 시간 시스템을 재사용하지 않습니다.");
            if ((testId == ExitCase.T06 && actor != ExitActor.Projectile) ||
                (testId == ExitCase.T07 && actor != ExitActor.Dash))
                throw new InvalidOperationException("T06은 Projectile, T07은 Dash를 선택하세요.");

            started = true;
            previousSimulation = Physics2D.simulationMode;
            previousFixedDelta = UnityEngine.Time.fixedDeltaTime;
            previousTimeScale = UnityEngine.Time.timeScale;
            previousTriggers = Physics2D.queriesHitTriggers;
            previousStartInside = Physics2D.queriesStartInColliders;
            previousAutoSync = Physics2D.autoSyncTransforms;
            UnityEngine.Time.fixedDeltaTime = 0.02f;
            UnityEngine.Time.timeScale = 1;
            Physics2D.queriesHitTriggers = true;
            Physics2D.queriesStartInColliders = true;
            Physics2D.autoSyncTransforms = false;
            Physics2D.simulationMode = clock == ExitClock.ManualTickThenPhysics
                ? SimulationMode2D.Script : SimulationMode2D.FixedUpdate;
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ExitLab"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{testId}_{actor}_{clock}.jsonl");
            output = new StreamWriter(file) { AutoFlush = true };
            Application.logMessageReceived += OnLog;

            // Production 모드만 원본 TickerRunner를 사용합니다. 첫 빈 Tick은 OnEnable에서 발생합니다.
            if (clock == ExitClock.ProductionCoroutine)
                new GameObject("ExitLab - Production Ticker").AddComponent<TickerRunner>();
            else
            {
                GameTime.Ticker = new Ticker();
                GameTime.DelayScheduler = new DelayScheduler();
                GameTime.Initialize();
            }
            ticker = GameTime.Ticker;
            if (!ReferenceEquals(ticker, Ticker.Instance))
                throw new InvalidOperationException("Ticker 인스턴스 불일치. Play를 종료하세요.");

            // Collector → 사전 관측 → 실제 이동 → 사후 관측의 구독 순서를 고정합니다.
            var services = new GameObject("ExitLab - Explicit Target Collector");
            services.AddComponent<TargetResolver>();
            collector = services.AddComponent<CommandCollector>();
            collector.CommandEnqueued += ObserveEnqueued;
            ticker.OnTick += BeforeTick;
            ConfigureTargets(out var runSpeed, out var runPenetrative);
            var caster = new GameObject("ExitLab - Caster (Default layer)");
            hitMarker = new Marker { owner = this, label = "onHit" };
            expireMarker = new Marker { owner = this, label = "onExpire" };
            var hits = Chain(hitMarker);
            var expires = Chain(expireMarker);
            endTick = checked((ushort)(ticker.TickCount + 120));
            if (actor == ExitActor.Projectile)
            {
                var mech = new ProjectileMechanism { projectilePrefab = projectilePrefab, lifeTick = 120,
                    speed = runSpeed, penetrative = runPenetrative, onHit = hits, onExpire = expires };
                // 원본 Execute → Instantiate → Init 경로를 그대로 사용합니다.
                mech.Execute(new CastContext(mech, caster.transform, target, default, originCastPosition: caster.transform.position));
                projectile = FindFirstObjectByType<ProjectileEntity>();
                body = projectile.GetComponent<Rigidbody2D>();
                projectile.gameObject.AddComponent<ExitLabLifecycle>().onDestroyed = () => Record("projectile-OnDestroy");
            }
            else
            {
                body = caster.AddComponent<Rigidbody2D>();
                body.interpolation = RigidbodyInterpolation2D.None;
                var col = caster.AddComponent<CircleCollider2D>();
                col.radius = 0.5f;
                col.isTrigger = true;
                // Mover의 Dash 경로는 Context/Stats를 사용하지 않습니다. MakeMove/Knockback은 호출 금지.
                mover = new Mover(null, null, body, col);
                var mech = new DashMechanism { durationTicks = 120, speed = runSpeed,
                    penetrative = runPenetrative, onHit = hits, onExpire = expires };
                var ctx = new CastContext(mech, caster.transform, target, default, originCastPosition: caster.transform.position);
                mover.StartDash(new DashContract(ctx, endTick, runSpeed, false, runPenetrative, hits, expires, false));
                ticker.OnTick += DashTick;
            }
            ticker.OnTick += AfterTick;
            // 배치 변경 후 한 번만 동기화합니다. 매 Tick 강제 동기화로 원본 스케줄을 바꾸지 않습니다.
            Physics2D.SyncTransforms();
            Record("configuration", $"speed={runSpeed}; life/duration=120; endTick={endTick}; fixedDelta=0.02; " +
                $"simulation={Physics2D.simulationMode}; triggers=true; startInside=true; autoSync=false; " +
                $"penetrative={runPenetrative}; targetObject={target.name}; maxTranslationSpeed={Physics2D.maxTranslationSpeed}; " +
                $"selfLayer={body.gameObject.layer}; trigger={body.GetComponent<Collider2D>().isTrigger}; interpolation={body.interpolation}; file={file}");
            Record("ready", "수동 물리는 Tick 1회 후 1/60초 Simulate. 원본 모드는 실시간 60Hz 지향/물리 0.02.");
            StartCoroutine(ObserveAfterFixed());
            Debug.Log("ExitLab 기록: " + file);
        }

        private void ConfigureTargets(out int runSpeed, out bool runPenetrative)
        {
            coordinateTarget.gameObject.SetActive(false);
            entityTarget.gameObject.SetActive(false);
            childColliderTarget.gameObject.SetActive(false);
            intermediateEnemy.gameObject.SetActive(false);
            runSpeed = speed;
            runPenetrative = penetrative;
            var position = targetPosition;
            var shape = targetShape;
            if (testId == ExitCase.T01) { shape = ExitTarget.TransformOnly; position = new Vector2(actor == ExitActor.Projectile ? 0.05f : 0, 0); runSpeed = 6; runPenetrative = false; }
            if (testId == ExitCase.T02) { shape = ExitTarget.TransformOnly; position = new Vector2(0.4f, 0); runSpeed = 60; runPenetrative = false; }
            if (testId == ExitCase.T06) { shape = ExitTarget.EntityAndCollider; position = new Vector2(0.05f, 0); runSpeed = 6; runPenetrative = false; }
            if (testId == ExitCase.T07)
            {
                shape = ExitTarget.TransformOnly; position = new Vector2(3, 0); runSpeed = 6; runPenetrative = true;
                intermediateEnemy.position = new Vector3(0.7f, 0, 0);
                intermediateEnemy.gameObject.SetActive(true);
            }
            target = shape == ExitTarget.TransformOnly ? coordinateTarget :
                shape == ExitTarget.EntityAndCollider ? entityTarget : childColliderTarget;
            target.position = new Vector3(position.x, position.y, 0);
            target.gameObject.SetActive(true);
        }

        private static List<MechanismData> Chain(Marker marker) => new List<MechanismData>
        {
            new MechanismData { mechanism = marker, mode = TargetMode.TowardsEntity, requireRetarget = false }
        };
        private void DashTick(ushort tick) => mover.DashTick(tick, 0);
        private void BeforeTick(ushort tick) { Record("before-tick"); ObserveQuery(); }
        private void AfterTick(ushort tick) { Record("after-tick-before-physics"); }

        // 실제 등록 이벤트를 셉니다. 물리 이후에 등록된 Chain도 누락하거나 중복 집계하지 않습니다.
        private void ObserveEnqueued(SkillCommand command)
        {
            if (ReferenceEquals(command.Mech, hitMarker)) { hitEnqueued++; Record("enqueue-onHit"); }
            if (ReferenceEquals(command.Mech, expireMarker)) { expireEnqueued++; Record("enqueue-onExpire"); }
        }

        public void StepOnce()
        {
            if (!started || faulted || clock != ExitClock.ManualTickThenPhysics) return;
            try
            {
                ticker.Step();
                var simulated = Physics2D.Simulate(1f / 60f);
                if (simulated) TravelPhysicsObserver.NotifyManualSimulationCompleted();
                fixedSteps++;
                Record("after-manual-physics", "Simulate returned=" + simulated);
            }
            catch (Exception e) { Debug.LogException(e); faulted = true; }
        }

        private void FixedUpdate()
        {
            if (started && clock == ExitClock.ProductionCoroutine) { fixedSteps++; Record("fixed-callback-before-physics"); }
        }
        private IEnumerator ObserveAfterFixed()
        {
            while (started)
            {
                yield return new WaitForFixedUpdate();
                if (clock == ExitClock.ProductionCoroutine) Record("after-fixed-yield");
            }
        }
        private void LateUpdate() { if (started) Record("late-update"); }

        // 별도 조회 결과이며 Motor 내부 hit의 직접 계측값이 아닙니다. 동기화/이동은 수행하지 않습니다.
        private void ObserveQuery()
        {
            if (!body || !target || (actor == ExitActor.Projectile && !projectile) ||
                (actor == ExitActor.Dash && Read(mover, "_dashContract") == null)) return;
            Vector2 delta;
            float radius;
            if (projectile)
            {
                var cache = (FixedVector2)Read(projectile, "_location");
                var velocity = new FixedVector2(((Vector2)target.position - cache.AsVector2).normalized * (int)Read(projectile, "_speed"));
                delta = Vector2.ClampMagnitude((Vector2)target.position - body.position, (int)Read(projectile, "_speed") / 60f);
                var bounds = body.GetComponent<Collider2D>().bounds;
                radius = Mathf.Min(bounds.extents.x, bounds.extents.y);
            }
            else
            {
                var contract = (DashContract)Read(mover, "_dashContract");
                delta = Vector2.ClampMagnitude((Vector2)target.position - body.position, contract.Speed / 60f);
                radius = .5f;
            }
            Record("query-input-derived", $"delta={delta.ToString("F6")}; radius={radius}; no guarantee MovePosition called");
            if (delta.sqrMagnitude <= 0) return;
            var hits = Physics2D.CircleCastAll(body.position, radius, delta.normalized, delta.magnitude,
                LayerMask.GetMask("Walls&Obstacles", "Foe"));
            foreach (var hit in hits)
            {
                var entity = EntityIdentity.FromCollider(hit.collider);
                Record("query-preview", $"collider={hit.collider.GetInstanceID()}; entity={(entity ? entity.GetInstanceID() : 0)}; " +
                    $"layer={hit.collider.gameObject.layer}; distance={hit.distance}; colliderTarget={entity && entity.transform == target}");
            }
        }

        [Serializable] private sealed class Row
        {
            public string phase, test, actor, clock, detail, rb, transform, targetPosition, cache, latestRequested;
            public int frame, tick, physicsStep, targetId, hitEnqueued, expireEnqueued, hitExecuted, expireExecuted, subscriptions;
            public bool projectileAlive, projectileSubscribed, dashContract, faulted;
        }
        private void Record(string phase, string detail = "")
        {
            if (output == null) return;
            var motor = projectile ? Read(projectile, "_motor") : mover != null ? Read(mover, "_motor") : null;
            var callbacks = ticker != null ? (Delegate)Read(ticker, "OnTick") : null;
            var projectileSubscribed = false;
            if (callbacks != null)
                foreach (var callback in callbacks.GetInvocationList())
                    if (projectile && ReferenceEquals(callback.Target, projectile) && callback.Method.Name == "TickHandler") projectileSubscribed = true;
            var row = new Row { phase = phase, test = testId.ToString(), actor = actor.ToString(), clock = clock.ToString(), detail = detail,
                frame = UnityEngine.Time.frameCount, tick = ticker?.TickCount ?? 0, physicsStep = fixedSteps,
                targetId = target ? target.GetInstanceID() : 0, targetPosition = target ? target.position.ToString("F6") : "null",
                rb = body ? body.position.ToString("F6") : "destroyed", transform = body ? body.transform.position.ToString("F6") : "destroyed",
                cache = projectile ? ((FixedVector2)Read(projectile, "_location")).AsVector2.ToString("F6") : "n/a",
                latestRequested = motor != null ? ((FixedVector2)Read(motor, "_pos")).AsVector2.ToString("F6") : "n/a",
                projectileAlive = projectile, projectileSubscribed = projectileSubscribed,
                dashContract = mover != null && Read(mover, "_dashContract") != null,
                hitEnqueued = hitEnqueued, expireEnqueued = expireEnqueued, hitExecuted = hitMarker?.executed ?? 0,
                expireExecuted = expireMarker?.executed ?? 0, subscriptions = callbacks?.GetInvocationList().Length ?? 0, faulted = faulted };
            output.WriteLine(JsonUtility.ToJson(row));
        }
        private void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            faulted = true;
            output?.WriteLine(JsonUtility.ToJson(new Row { phase = "first-or-later-error", detail = condition + "\n" + stack, faulted = true }));
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 580, 190), GUI.skin.box);
            GUILayout.Label($"ExitLab | {testId} | {actor} | {clock}");
            GUILayout.Label("Inspector에서 선택 후 시작. 매 사례마다 Play 종료/재진입.");
            if (!started && GUILayout.Button("선택한 사례 시작 (게임플레이 시험 시작)")) Begin();
            if (started && clock == ExitClock.ManualTickThenPhysics && GUILayout.Button("Tick 1회 → 물리 1회")) StepOnce();
            GUILayout.Label($"Tick={ticker?.TickCount ?? 0}, Hit 등록/실행={hitEnqueued}/{hitMarker?.executed ?? 0}, Expire={expireEnqueued}/{expireMarker?.executed ?? 0}");
            if (faulted) GUILayout.Label("오류 발생: Play를 종료하고 첫 오류를 기록하세요. 이후 결과는 정상 판정 불가.");
            GUILayout.EndArea();
        }
        private void OnDrawGizmos()
        {
            if (body) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(body.position, actor == ExitActor.Projectile ? 0.1f : 0.5f); }
            foreach (var item in new[] { coordinateTarget, entityTarget, childColliderTarget, intermediateEnemy })
            {
                if (!item || !item.gameObject.activeSelf) continue;
                Gizmos.color = item == coordinateTarget ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(item.position, item == coordinateTarget ? 0.05f : 0.2f);
            }
        }
        private void OnDestroy()
        {
            if (!started) return;
            Application.logMessageReceived -= OnLog;
            if (collector) collector.CommandEnqueued -= ObserveEnqueued;
            if (ticker != null) { ticker.OnTick -= BeforeTick; ticker.OnTick -= AfterTick; ticker.OnTick -= DashTick; }
            Record("harness-destroy", "Play 종료는 정상 Expire의 증거가 아닙니다.");
            output?.Dispose(); output = null;
            Physics2D.simulationMode = previousSimulation;
            Physics2D.queriesHitTriggers = previousTriggers;
            Physics2D.queriesStartInColliders = previousStartInside;
            Physics2D.autoSyncTransforms = previousAutoSync;
            UnityEngine.Time.fixedDeltaTime = previousFixedDelta;
            UnityEngine.Time.timeScale = previousTimeScale;
        }
        private sealed class Marker : INewMechanism
        {
            public ExitLabHarness owner;
            public string label;
            public int executed;
            public short CooldownTicks => 0;
            public byte DelayTicks => 0;
            public float MinRange => 0;
            public float MaxRange => 100;
            public LayerMask Mask => 0;
            public void Execute(CastContext ctx) { executed++; owner.Record("execute-" + label); }
        }
    }
}
#endif
