using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Characters;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Utils;
using Object = UnityEngine.Object;
using GameTime = Systems.Time.Time;

namespace ExitLab
{
    /// <summary>실제 Attacker/Resolver/Collector를 사용합니다. OS 마우스 대신 검증 카메라만 이동합니다.</summary>
    [InitializeOnLoad]
    public static class CastPreparationChecks
    {
        private sealed class Probe : ProjectileMechanism
        {
            public int Count;
            public CastContext Last;
            protected override void ExecuteCore(CastContext ctx) { Count++; Last = ctx; }
        }
        private static readonly List<string> Results = new();
        private static readonly List<Object> Owned = new();
        private static readonly List<SkillCommand> Queued = new();
        private static Camera _camera;
        private static CommandCollector _collector;
        private static SkillRunner _runner;
        private static TemporaryAITarget _caster, _one, _two;
        private static Attacker _attacker;
        private static SkillData _skill;
        private static Dictionary<SkillSlot, SkillBinding> _bindings;
        private static int _queries;
        private static readonly List<string> Errors = new();
        static CastPreparationChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CastPreparation.Run", false)) return;
                SessionState.SetBool("CastPreparation.Run", false);
                try { Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunPlayMode()
        {
            Guard();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool("CastPreparation.Run", true);
            EditorApplication.EnterPlaymode();
        }
        public static void RunWithRegression() { Run(); TimeSystemChecks.RunWithRegression(); }
        private static void Guard()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Validation copy only.");
        }
        public static void Run()
        {
            Guard(); Results.Clear(); Owned.Clear(); Queued.Clear();
            if (!Application.isPlaying) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var oldTicker = GameTime.Ticker; var oldScheduler = GameTime.DelayScheduler;
            var oldTickerInstance = Ticker.Instance; var oldSchedulerInstance = DelayScheduler.Instance;
            var oldCollector = CommandCollector.Instance;
            var oldLogging = AttackLog.Enabled; var oldFilter = AttackLog.CasterFilter;
            _queries = 0; Errors.Clear(); AttackLog.Enabled = true; AttackLog.CasterFilter = 0;
            Application.logMessageReceived += ObserveLog;
            try
            {
                SetTime(new Ticker(), new DelayScheduler());
                _camera = Go("test camera").AddComponent<Camera>();
                _camera.tag = "MainCamera"; _camera.orthographic = true; _camera.transform.position = new Vector3(0, 0, -10);
                var services = Go("test services"); services.SetActive(false);
                var cursor = services.AddComponent<CursorResolver>(); cursor.mainCamera = _camera;
                var resolver = services.AddComponent<TargetResolver>(); resolver.cursorResolver = cursor;
                _collector = services.AddComponent<CommandCollector>();
                services.SetActive(true);
                if (!Application.isPlaying) Invoke(_collector, "OnEnable");
                _collector.CommandEnqueued += Queued.Add;
                _runner = new SkillRunner(resolver);
                _caster = Entity("caster", "Foe", Vector2.zero);
                _one = Entity("one", "You", Vector2.right);
                _two = Entity("two", "You", Vector2.right * 2);
                _skill = ScriptableObject.CreateInstance<SkillData>(); Owned.Add(_skill);
                var spec = ScriptableObject.CreateInstance<CharacterSpec>(); Owned.Add(spec);
                var context = new Context(_caster, _caster.gameObject, _caster.transform, resolver, _collector, spec, new PlayerScripts.Core.Logger("preparation test"));
                context.RegisterScheduler(GameTime.DelayScheduler);
                context.RegisterStats(new StatsBridge(context, new BaseStatsContainer(100, 0, 0, 0, 100, 0, 0)));
                context.RegisterVariableStorage(new VariableStorage());
                _bindings = new Dictionary<SkillSlot, SkillBinding>();
                _attacker = new Attacker(context, _caster.transform, _bindings, _collector);
                InputAndSpatial();
                InvalidTargets();
                ExplicitAI();
                DetectAndChain();
                RootModeSource();
                Check(Errors.Count == 0, "no runtime errors during preparation checks: " + string.Join("; ", Errors));
                Check(true, "completed in " + (Application.isPlaying ? "PlayMode" : "EditMode"));
                Debug.Log($"CastPreparationChecks: PASS ({Results.Count} assertions)");
            }
            catch (Exception error) { Results.Add("FAIL: " + error); throw; }
            finally
            {
                if (_collector)
                {
                    _collector.CeaseCommand();
                    if (!Application.isPlaying) Invoke(_collector, "OnDisable");
                }
                for (var i = Owned.Count - 1; i >= 0; i--) if (Owned[i]) Object.DestroyImmediate(Owned[i]);
                SetTime(oldTicker, oldScheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, oldTickerInstance);
                DelayScheduler.Instance = oldSchedulerInstance;
                typeof(CommandCollector).GetProperty("Instance").SetValue(null, oldCollector);
                Application.logMessageReceived -= ObserveLog;
                AttackLog.Enabled = oldLogging; AttackLog.CasterFilter = oldFilter;
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../cast-preparation-" + (Application.isPlaying ? "play" : "edit") + ".txt"));
                File.WriteAllLines(output, Results);
            }
        }
        private static void InputAndSpatial()
        {
            var probe = MakeProbe(); Bind(probe, TargetMode.TowardsEntity);
            Aim(_one.transform.position);
            _attacker.PrepareCast(SkillSlot.Attack, 0);
            Check(Queued.Count == 0 && _queries == 0, "PrepareCast never queries or queues Target");
            _attacker.EnqueueCast(SkillSlot.Attack);
            Check(_queries == 0, "input enqueue does not query Target");
            Aim(_two.transform.position);
            _attacker.Tick(GameTime.Ticker.TickCount, 0);
            Check(Queued.Count == 1 && Queued[0].Target.Matches(_two), "Target selected at Tick, not PrepareCast or EnqueueCast");
            Check(_queries == 1, "actual input processing queries exactly once");
            var root = Queued[0];
            Check(root.Preparation == CastPreparationState.RootTargetPrepared && !root.OriginCastPosition.HasValue && probe.Count == 0,
                "Target preparation neither executes nor captures Origin");
            Check(_runner.TryPrepareRoot(root, out var repeated, out _) && repeated.Trace.CommandId == root.Trace.CommandId && repeated.Target.Equals(root.Target),
                "preparing twice retains command identity and Target");
            GameTime.Ticker.Step();
            Aim(_one.transform.position); _two.transform.position = new Vector2(20, 0); _caster.transform.position = new Vector2(4, 2);
            GameTime.Ticker.Step(); Check(probe.Count == 0, "existing two-tick Delay retained");
            GameTime.Ticker.Step();
            Check(probe.Count == 1 && probe.Last.Target.Matches(_two), "prepared player Entity executes beyond range without cursor reselection");
            Check(_queries == 1, "prepared execution never queries again");
            Check(probe.Last.OriginCastPosition == new Vector2(4, 2) && probe.Last.Destination == new Vector2(20, 0),
                "Origin captured at execution; Entity Destination uses live position of same identity");
            Check(probe.Last.Trace.CommandId == root.Trace.CommandId && probe.Last.Source == root.Source, "preparation preserves tracing and source");
            Reset(); probe = MakeProbe(); Bind(probe, TargetMode.TowardsCursor); Aim(new Vector2(2, 1));
            Input(); var fixedTarget = Queued[0].Target;
            Check(fixedTarget.Kind == TargetKind.Position, "cursor root captures a Position value");
            Aim(new Vector2(-2, 1)); _caster.transform.position = new Vector2(1, 1); Step(3);
            Check(fixedTarget.TryGetPosition(out var position) && probe.Last.Target.Equals(fixedTarget) &&
                Vector2.Distance(probe.Last.Destination.Value, position) < .001f && probe.Last.OriginCastPosition == new Vector2(1, 1),
                "Position remains fixed while Origin uses execution-time caster");
            Reset(); Bind(probe, TargetMode.TowardsEntity); Aim(new Vector2(10, 10)); Input();
            Check(Queued.Count == 0, "missing ordinary Target rejected before Collector");
        }
        private static void InvalidTargets()
        {
            Reset(); var probe = MakeProbe(); Bind(probe, TargetMode.TowardsEntity); Aim(_one.transform.position); Input();
            _one.gameObject.SetActive(false); Aim(_two.transform.position); Step(3);
            Check(probe.Count == 0, "lost prepared player Target does not retarget");
            Reset(); Aim(_one.transform.position); Input(); _one.gameObject.layer = LayerMask.NameToLayer("Foe"); Step(3);
            Check(probe.Count == 0, "prepared relative player Target rechecks faction");
            Reset(); Aim(_one.transform.position); Input(); _one.targetable = false; Step(3);
            Check(probe.Count == 0, "prepared player Target rechecks targetability");
        }
        private static void ExplicitAI()
        {
            Reset(); var probe = MakeProbe(); var current = true;
            var explicitTarget = new ExplicitAttackTarget(_caster, _one, TargetRelationMask.Enemy, () => current);
            SkillCommand Request() => new(_caster.transform, TargetMode.TowardsEntity, default, probe, default,
                SkillTarget.ForEntity(_one), explicitTarget: explicitTarget);
            Check(_runner.TryPrepareRoot(Request(), out var prepared, out _), "AI prepares with valid range");
            _one.transform.position = new Vector2(20, 0); _runner.Activate(prepared);
            Check(probe.Count == 1, "prepared AI also executes beyond maximum range");
            _one.transform.position = new Vector2(.1f, 0); _runner.Activate(prepared);
            Check(probe.Count == 2, "prepared AI also executes below minimum selection range");
            _runner.Activate(Request());
            Check(probe.Count == 2 && !_runner.TryPrepareRoot(Request(), out _, out _), "deferred explicit command and initial preparation retain range validation");
            _one.transform.position = Vector2.right; current = false; _runner.Activate(prepared);
            Check(probe.Count == 2, "prepared AI lifetime still expires");
            current = true; _one.gameObject.layer = LayerMask.NameToLayer("Foe"); _runner.Activate(prepared);
            Check(probe.Count == 2, "prepared AI faction still checked");
            _one.gameObject.layer = LayerMask.NameToLayer("You"); _one.gameObject.SetActive(false); _runner.Activate(prepared);
            Check(probe.Count == 2, "prepared AI inactive target still rejected");
            _one.gameObject.SetActive(true); _caster.enabled = false; _runner.Activate(prepared);
            Check(probe.Count == 2, "prepared AI disabled caster still rejected"); _caster.enabled = true;
        }
        private static void DetectAndChain()
        {
            Reset(); var child = MakeProbe(); var success = MakeProbe();
            var detect = new DetectMechanism { requiredMode = TargetMode.TowardsEntity,
                onNotFound = new[] { new MechanismData { mechanism = child } },
                onDetected = new[] { new MechanismData { mechanism = success } } };
            JsonUtility.FromJsonOverwrite("{\"maxRange\":3000}", detect); detect.ConfigureRelativeTargets(TargetRelationMask.Enemy);
            Bind(detect, TargetMode.TowardsCursor); Aim(new Vector2(10, 10)); Input();
            var missing = Queued[0];
            Check(!missing.Target.IsResolved && missing.Preparation == CastPreparationState.RootTargetPrepared && missing.TargetMode == TargetMode.TowardsEntity,
                "Detect miss is prepared with requiredMode, not rejected");
            Queued.Clear(); Aim(_one.transform.position); _runner.Activate(missing);
            Check(Queued.Count == 1 && ReferenceEquals(Queued[0].Mech, child), "Detect miss cannot acquire newly available Target after preparation");
            Check(Queued[0].Preparation == CastPreparationState.Deferred, "Detect child has independent deferred preparation state");
            Reset(); Aim(_one.transform.position); Input(); var found = Queued[0]; Queued.Clear(); _one.targetable = false;
            _runner.Activate(found);
            Check(Queued.Count == 1 && ReferenceEquals(Queued[0].Mech, child) && Queued[0].Target.Entity == _one,
                "Detect lost Target uses failure branch and preserves identity for child");
            Reset(); Aim(_one.transform.position); Input(); found = Queued[0]; Queued.Clear(); _one.transform.position = new Vector2(30, 0);
            _runner.Activate(found);
            Check(Queued.Count == 1 && ReferenceEquals(Queued[0].Mech, success), "Detect found Target remains found beyond selection range");
            Reset(); var origin = new Vector2(7, 8); var exit = new TravelResult(ExitKind.Interrupted, ExitCause.WallBlocked, new Vector2(6, 5));
            var ctx = new CastContext(child, _caster.transform, SkillTarget.ForEntity(_one), default, originCastPosition: origin, exit: exit);
            SkillUtils.ActivateChain(new[] {
                new MechanismData { mechanism = child },
                new MechanismData { mechanism = child, targetSource = ChainTargetSource.ExitPosition },
                new MechanismData { mechanism = child, requireRetarget = true, mode = TargetMode.TowardsEntity, targetSource = ChainTargetSource.ExitPosition }
            }, ctx);
            Check(Queued.Count == 3 && Queued.TrueForAll(c => c.Preparation == CastPreparationState.Deferred && c.OriginCastPosition == origin),
                "Chain preserves origin but never inherits root preparation state");
            Check(Queued[0].Target.Matches(_one) && Queued[1].Target.TryGetPosition(out var p) && p == exit.Position && !Queued[2].Target.IsResolved,
                "Chain inherit / exit / retarget priority preserved");
            Aim(_two.transform.position); _runner.Activate(Queued[2]);
            Check(child.Last.Target.Matches(_two) && child.Last.OriginCastPosition == origin && child.Last.Destination == (Vector2)_two.transform.position,
                "Chain retarget uses execution-time selection and own Destination");
        }
        private static Probe MakeProbe()
        {
            var probe = new Probe(); JsonUtility.FromJsonOverwrite("{\"minRange\":250,\"maxRange\":3000,\"delayTicks\":2}", probe);
            probe.ConfigureRelativeTargets(TargetRelationMask.Enemy);
            probe.ConfigureDestination(new DestinationSettings { mode = DestinationMode.BasePosition,
                position = new BasePositionData { anchor = BasePositionAnchor.InputTargetPosition } }); return probe;
        }
        private static void Bind(INewMechanism mechanism, TargetMode mode)
        {
            _skill.mechanismData = new MechanismData { mechanism = mechanism, mode = mode };
            // 두 Mode를 의도적으로 다르게 설정하여 모든 경로가 옛 슬롯 값을 무시하는지 확인합니다.
            _bindings[SkillSlot.Attack] = new SkillBinding { skillData = _skill,
                mode = mode == TargetMode.TowardsEntity ? TargetMode.TowardsCursor : TargetMode.TowardsEntity, slot = SkillSlot.Attack };
        }
        private static void RootModeSource()
        {
            Reset(); var probe = MakeProbe(); Bind(probe, TargetMode.TowardsEntity);
            var target = new ExplicitAttackTarget(_caster, _one, TargetRelationMask.Enemy);
            _attacker.EnqueueCast(SkillSlot.Attack, target); _attacker.Tick(GameTime.Ticker.TickCount, 0);
            Check(Queued.Count == 1 && Queued[0].Target.Matches(_one), "explicit AI accepts SkillData Entity despite legacy Cursor slot");
            Reset(); Bind(probe, TargetMode.TowardsCursor);
            _attacker.EnqueueCast(SkillSlot.Attack, target); _attacker.Tick(GameTime.Ticker.TickCount, 0);
            Check(Queued.Count == 0, "explicit AI rejects SkillData Cursor despite legacy Entity slot");
            Reset();
            var switcher = new SwitchMechanism { cases = Array.Empty<SwitchCase>() };
            JsonUtility.FromJsonOverwrite("{\"maxRange\":3000}", switcher);
            Bind(switcher, TargetMode.TowardsCursor); Aim(new Vector2(1, 1)); Input();
            Check(Queued.Count == 1 && Queued[0].TargetMode == TargetMode.TowardsCursor && Queued[0].Target.Kind == TargetKind.Position,
                "Switch root also uses SkillData Mode on empty ground");
            Reset();
        }
        private static void Input() { _attacker.EnqueueCast(SkillSlot.Attack); _attacker.Tick(GameTime.Ticker.TickCount, 0); }
        private static void Reset()
        {
            _collector.CeaseCommand(); Queued.Clear(); _caster.transform.position = Vector2.zero;
            _one.gameObject.SetActive(true); _one.targetable = true; _one.gameObject.layer = LayerMask.NameToLayer("You");
            _one.transform.position = Vector2.right; _two.transform.position = Vector2.right * 2;
        }
        private static void Aim(Vector2 position)
        {
            var current = (Vector2)_camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            _camera.transform.position += (Vector3)(position - current); Physics2D.SyncTransforms();
        }
        private static TemporaryAITarget Entity(string name, string layer, Vector2 position)
        {
            var go = Go(name); go.layer = LayerMask.NameToLayer(layer); go.transform.position = position;
            go.AddComponent<CircleCollider2D>().radius = .2f; return go.AddComponent<TemporaryAITarget>();
        }
        private static GameObject Go(string name) { var go = new GameObject(name); Owned.Add(go); return go; }
        private static void Step(int count) { for (var i = 0; i < count; i++) GameTime.Ticker.Step(); }
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void SetTime(Ticker ticker, DelayScheduler scheduler)
        {
            typeof(GameTime).GetProperty("Ticker").SetValue(null, ticker);
            typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, scheduler);
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Results.Add("PASS " + message); }
        private static void ObserveLog(string message, string stack, LogType type)
        {
            if (message.Contains("stage=TargetQuery")) _queries++;
            if (type is LogType.Error or LogType.Exception or LogType.Assert) Errors.Add(message);
        }
    }
}
