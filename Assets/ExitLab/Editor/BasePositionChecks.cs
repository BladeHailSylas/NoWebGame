using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Core;
using PlayerScripts.Acts;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using Systems.SubSystems;
using Systems.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using GameTime = Systems.Time.Time;

namespace ExitLab
{
    /// <summary>저장 에셋을 변경하지 않고 순수 계산, 실행 경계 및 실제 물리 이동을 확인합니다.</summary>
    public static class BasePositionChecks
    {
        private static readonly List<string> Results = new();
        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); Results.Add("PASS " + name); }
        private sealed class Probe : ProjectileMechanism
        {
            public int Count; public CastContext Last;
            protected override void ExecuteCore(CastContext ctx) { Count++; Last = ctx; }
        }
        private static DestinationSettings Settings(BasePositionAnchor anchor = BasePositionAnchor.CasterPosition, float distance = 3,
            BasePositionDirection direction = BasePositionDirection.WorldRight, float angle = 0) => new()
        { mode = DestinationMode.BasePosition, position = new BasePositionData { anchor = anchor, distance = distance, direction = direction, angleDegrees = angle } };
        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < .00001f;
        public static void RunWithRegression()
        {
            Run();
            RelativeTargetPipelineChecks.Run();
            AttackTraceChecks.Run();
        }
        [MenuItem("Tools/ExitLab/Check Base Position and Destination")]
        public static void Run()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Run only in a marked validation copy.");
            Results.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var simulation = Physics2D.simulationMode; var triggers = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script; Physics2D.queriesHitTriggers = true;
                MathChecks(); ExecutionChecks(); StackChecks(); TravelChecks(); QueryChecks(); SerializationChecks();
                Debug.Log($"BasePositionChecks PASS {Results.Count}");
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                Physics2D.simulationMode = simulation; Physics2D.queriesHitTriggers = triggers;
                File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../base-pos-tests.txt"), Results);
            }
        }
        private static void MathChecks()
        {
            var input = new BasePositionInputs(new Vector2(2, 3), new Vector2(5, 3), new Vector2(5, 1), new Vector2(8, 9));
            var points = new[] { new Vector2(2, 3), new Vector2(5, 3), new Vector2(5, 1), new Vector2(8, 9) };
            foreach (BasePositionAnchor anchor in Enum.GetValues(typeof(BasePositionAnchor)))
            foreach (BasePositionDirection direction in Enum.GetValues(typeof(BasePositionDirection)))
            foreach (var angle in new[] { 0f, 90f, -90f, 180f, 360f })
            {
                var data = Settings(anchor, 2, direction, angle).position;
                var theta = (angle + (direction == BasePositionDirection.OriginToInputTarget ? 90 : 0)) * Mathf.Deg2Rad;
                var expected = points[(int)anchor] + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * 2;
                Check(BasePositionResolver.TryResolve(data, input, out var result, out _) && Near(result, expected), $"polar {anchor}/{direction}/{angle}");
            }
            var zero = Settings(distance: 0, direction: (BasePositionDirection)999, angle: float.NaN).position;
            Check(BasePositionResolver.TryResolve(zero, new BasePositionInputs(Vector2.zero, null, null, null), out var origin, out _) && origin == Vector2.zero, "zero distance skips missing/invalid direction and angle");
            zero.anchor = BasePositionAnchor.ExitPosition;
            Check(!BasePositionResolver.TryResolve(zero, inputs: default, out _, out var missing) && missing == SpatialFailure.MissingExitPosition, "zero still requires anchor");
            foreach (var distance in new[] { -1f, float.NaN, float.PositiveInfinity })
                Check(!BasePositionResolver.TryResolve(Settings(distance: distance).position, input, out _, out var failure) && failure == SpatialFailure.InvalidDistance, "reject distance " + distance);
            var degenerate = new BasePositionInputs(Vector2.zero, Vector2.zero, Vector2.zero, null);
            Check(!BasePositionResolver.TryResolve(Settings(direction: BasePositionDirection.CasterToInputTarget).position, degenerate, out _, out var reason) && reason == SpatialFailure.DegenerateDirection, "no arbitrary fallback direction");
            Check(!BasePositionResolver.TryResolve(Settings(angle: float.PositiveInfinity).position, input, out _, out _), "nonfinite used angle rejected");
            Check(!BasePositionResolver.TryResolve(Settings(BasePositionAnchor.CasterPosition, float.MaxValue).position,
                new BasePositionInputs(new Vector2(float.MaxValue, 0), null, null, null), out _, out _), "overflow result rejected");
        }
        private static void ExecutionChecks()
        {
            typeof(GameTime).GetProperty("Ticker").SetValue(null, new Ticker());
            typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, new DelayScheduler());
            var caster = new GameObject("caster"); var target = new GameObject("target");
            var service = new GameObject("collector"); service.AddComponent<TargetResolver>(); var collector = service.AddComponent<CommandCollector>();
            // 일반 MonoBehaviour는 EditMode에서 자동 OnEnable을 실행하지 않습니다.
            typeof(CommandCollector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(collector, null);
            try
            {
                var entity = target.AddComponent<RelativeTargetTestEntity>();
                var probe = new Probe(); probe.ConfigureDestination(Settings(BasePositionAnchor.InputTargetPosition, 1));
                JsonUtility.FromJsonOverwrite("{\"delayTicks\":2}", probe);
                collector.EnqueueCommand(new SkillCommand(caster.transform, TargetMode.TowardsEntity, default, probe, default,
                    SkillTarget.ForEntity(entity), source: "Skill:fixture/root"));
                GameTime.Ticker.Step(); caster.transform.position = new Vector2(4, 0); target.transform.position = new Vector2(8, 0);
                for (var i = 0; i < 4; i++) GameTime.Ticker.Step();
                Check(probe.Count == 1 && probe.Last.Destination == new Vector2(9, 0), "resolve after delay using latest Target");
                Check(probe.Last.OriginCastPosition == new Vector2(4, 0), "initialize Origin at actual execution");
                target.transform.position = new Vector2(20, 0);
                Check(probe.Last.Destination == new Vector2(9, 0) && probe.Last.Target.Matches(entity), "snapshot fixed without replacing Entity Target");
                var child = new Probe(); child.ConfigureDestination(Settings(BasePositionAnchor.OriginCastPosition, 2));
                SkillUtils.ActivateChain(new[] { new MechanismData { mechanism = child } }, probe.Last, trigger: "test");
                caster.transform.position = new Vector2(30, 0); GameTime.Ticker.Step();
                Check(child.Count == 1 && child.Last.Destination == new Vector2(6, 0), "child resolves own settings and inherits original Origin");
                Check(child.Last.Source.Contains("/test[0]"), "diagnostic source path inherited");
                var exitChild = new Probe(); exitChild.ConfigureDestination(Settings(BasePositionAnchor.InputTargetPosition, 0));
                var exited = probe.Last.WithExit(new TravelResult(ExitKind.Completed, ExitCause.DestinationReached, new Vector2(11, 2)));
                SkillUtils.ActivateChain(new[] { new MechanismData { mechanism = exitChild, targetSource = ChainTargetSource.ExitPosition } }, exited);
                GameTime.Ticker.Step();
                Check(exitChild.Last.Target.Kind == TargetKind.Position && exitChild.Last.Destination == new Vector2(11, 2), "ExitPosition Target precedes Destination interpretation");
                Check(exited.Destination == probe.Last.Destination, "WithExit retains current execution snapshot");
                var bad = new Probe(); bad.ConfigureDestination(Settings(BasePositionAnchor.ExitPosition));
                collector.EnqueueCommand(new SkillCommand(caster.transform, TargetMode.TowardsEntity, default, bad, default, SkillTarget.ForEntity(entity)));
                GameTime.Ticker.Step(); GameTime.Ticker.Step();
                Check(bad.Count == 0, "Abort prevents execution and collector consumes command");
                var damage = new DamageMechanism(); damage.ConfigureDestination(Settings());
                Check(!damage.TryExecute(new CastContext(damage, caster.transform, SkillTarget.ForEntity(entity), default)), "Entity effect refuses Destination rather than replacing Target");
                var ray = new RayMechanism(); ray.ConfigureDestination(Settings(distance: 0));
                Check(!ray.TryExecute(new CastContext(ray, caster.transform, SkillTarget.ForEntity(entity), default)), "directional spatial execution at origin aborts");
                var direct = new Probe(); direct.ConfigureDestination(Settings(distance: 0));
                direct.Execute(new CastContext(direct, caster.transform, default, default));
                Check(direct.Count == 1 && direct.Last.Destination == (Vector2)caster.transform.position, "direct execution prepares with no Target when inputs permit");
                var copy = new Probe(); copy.ConfigureDestination(Settings());
                copy.Execute(new CastContext(copy, caster.transform, default, default));
                var first = copy.Last.Destination; caster.transform.position = Vector2.zero;
                copy.Execute(new CastContext(copy, caster.transform, default, default));
                Check(first != copy.Last.Destination, "shared mechanism stores no runtime Destination");
                var plain = new Probe(); plain.Execute(new CastContext(plain, caster.transform, SkillTarget.ForEntity(entity), default));
                Check(!plain.Last.Destination.HasValue, "Disabled remains Target-based");
            }
            finally { Object.DestroyImmediate(service); Object.DestroyImmediate(target); Object.DestroyImmediate(caster); }
        }
        private static void StackChecks()
        {
            var go = new GameObject("stack caster"); var def = ScriptableObject.CreateInstance<TriggerableDefinition>();
            try
            {
                var actor = go.AddComponent<RelativeTargetTestEntity>();
                var context = new Context(actor, go, go.transform, null, null, null, null); context.RegisterScheduler(new DelayScheduler());
                var manager = new StackManager(context, new VariableStorage());
                var storage = (Dictionary<StackKey, StackStatus>)typeof(StackManager).GetField("_stackStorage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                var metadata = (Dictionary<StackKey, StackMetadata>)typeof(StackManager).GetField("_metadata", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                var probe = new Probe(); probe.ConfigureDestination(Settings(BasePositionAnchor.ExitPosition));
                def.threshold = 1; def.effect = new MechanismData { mechanism = probe }; var key = new StackKey(def, "fixture", go.transform);
                storage[key] = new StackStatus(1, default);
                var method = typeof(StackManager).GetMethod("ResolveTrigger", BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(manager, new object[] { key, (ushort)0, new StackMetadata(7), 1 });
                Check(storage.ContainsKey(key) && metadata[key].Metadata == 7 && probe.Count == 0, "failed spatial trigger preserves stack and metadata");
                probe.ConfigureDestination(Settings());
                method.Invoke(manager, new object[] { key, (ushort)1, new StackMetadata(0), 1 });
                Check(!storage.ContainsKey(key) && metadata[key].Metadata == 0 && probe.Count == 1, "successful spatial trigger consumes once");
                Check(probe.Last.Source.StartsWith("Stack:") && probe.Last.OriginCastPosition == Vector2.zero, "direct stack source and origin recorded");
            }
            finally { Object.DestroyImmediate(def); Object.DestroyImmediate(go); }
        }
        private static void TravelChecks()
        {
            foreach (var fixedGoal in new[] { false, true })
            foreach (var lost in new[] { false, true })
            foreach (var pierce in new[] { false, true })
            {
                var caster = new GameObject("travel caster"); var target = new GameObject("travel target"); var body = new GameObject("body");
                try
                {
                    caster.layer = LayerMask.NameToLayer("Foe"); caster.AddComponent<RelativeTargetTestEntity>();
                    target.layer = LayerMask.NameToLayer("You"); var entity = target.AddComponent<RelativeTargetTestEntity>(); target.transform.position = new Vector2(3, 0);
                    var rb = body.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                    var col = body.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = .1f;
                    var mech = new ProjectileMechanism(); mech.ConfigureRelativeTargets(TargetRelationMask.Enemy);
                    if (fixedGoal) mech.ConfigureDestination(Settings(distance: 2));
                    var hits = 0; var travel = new TravelMotion(rb, col, new CastContext(mech, caster.transform, SkillTarget.ForEntity(entity), default), 60, pierce, TargetPolicy.WallLayers, 128, _ => hits++);
                    // Target 이동/소실 후에도 명시적 목적지는 처음 계산된 (2,0)입니다.
                    target.transform.position = new Vector2(4, 0); if (lost) target.SetActive(false);
                    Physics2D.SyncTransforms(); TravelResult? result = null;
                    for (var i = 0; i < 6 && !result.HasValue; i++) { result = travel.Advance(); Physics2D.Simulate(1f / 60); result ??= travel.AfterPhysics(); }
                    var expected = fixedGoal ? ExitCause.DestinationReached : lost ? ExitCause.TargetLost : ExitCause.TargetNotHit;
                    Check(result?.Cause == expected && hits == 0, $"fixed={fixedGoal} lost={lost} pierce={pierce}: {expected}, no synthetic hit");
                    if (fixedGoal) Check(result?.Kind == ExitKind.Completed && Near(rb.position, new Vector2(2, 0)), "fixed arrival world coordinate retained");
                    else if (!lost) Check(Near(rb.position, new Vector2(4, 0)), "Disabled follows updated Entity position");
                }
                finally { Object.DestroyImmediate(body); Object.DestroyImmediate(target); Object.DestroyImmediate(caster); }
            }
        }
        private static void QueryChecks()
        {
            var caster = new GameObject("query caster"); var selected = new GameObject("selected Target"); var spatial = new GameObject("spatial hit");
            var services = new GameObject("query services"); services.AddComponent<TargetResolver>(); var collector = services.AddComponent<CommandCollector>();
            typeof(CommandCollector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(collector, null);
            try
            {
                caster.layer = LayerMask.NameToLayer("Foe"); caster.AddComponent<RelativeTargetTestEntity>();
                selected.layer = spatial.layer = LayerMask.NameToLayer("You");
                selected.transform.position = Vector2.up * 2; spatial.transform.position = Vector2.right * 2;
                var target = selected.AddComponent<RelativeTargetTestEntity>(); var hit = spatial.AddComponent<RelativeTargetTestEntity>();
                selected.AddComponent<CircleCollider2D>().radius = .15f; spatial.AddComponent<CircleCollider2D>().radius = .15f;
                var child = new DummyMechanism(); var follows = new List<MechanismData> { new() { mechanism = child } };
                var queued = new List<SkillCommand>(); collector.CommandEnqueued += queued.Add;
                NewMechanism[] mechanisms = { new MeleeMechanism { angleDeg = 60, onHit = follows }, new HitscanMechanism { onHit = follows }, new RayMechanism { onHit = follows } };
                Physics2D.SyncTransforms();
                foreach (var mech in mechanisms)
                {
                    JsonUtility.FromJsonOverwrite("{\"maxRange\":3000}", mech); mech.ConfigureRelativeTargets(TargetRelationMask.Enemy); mech.ConfigureDestination(Settings());
                    queued.Clear(); mech.Execute(new CastContext(mech, caster.transform, SkillTarget.ForEntity(target), default));
                    Check(queued.Count == 1 && queued[0].Target.Matches(hit), mech.GetType().Name + " spatial direction changes while onHit carries actual Entity");
                }
                var rb = caster.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic; var col = caster.AddComponent<CircleCollider2D>(); col.radius = .1f;
                var teleport = new TeleportMechanism { ignoreEnemy = true }; JsonUtility.FromJsonOverwrite("{\"maxRange\":1000}", teleport); teleport.ConfigureDestination(Settings(distance: 5));
                var ctx = new CastContext(teleport, caster.transform, SkillTarget.ForEntity(target), default);
                Check(SpatialExecution.TryPrepare(ctx, out var prepared, out _) && prepared.Destination == new Vector2(5, 0), "Destination beyond MaxRange is not clamped during resolve");
                Check(new Teleporter(rb, col).TryTeleport(new TeleportContract(prepared)) && Near(rb.position, Vector2.right), "Teleport retains its independent execution distance limit");
            }
            finally { Object.DestroyImmediate(services); Object.DestroyImmediate(spatial); Object.DestroyImmediate(selected); Object.DestroyImmediate(caster); }
        }
        private static void SerializationChecks()
        {
            var old = new ProjectileMechanism(); JsonUtility.FromJsonOverwrite("{\"speed\":3,\"lifeTick\":60}", old);
            Check(old.Destination.mode == DestinationMode.Disabled, "missing serialized field defaults Disabled");
            old.ConfigureDestination(Settings(BasePositionAnchor.ExitPosition, 4, BasePositionDirection.OriginToInputTarget, 90));
            var asset = ScriptableObject.CreateInstance<SkillData>(); var copy = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                asset.mechanismData = new MechanismData { mechanism = old };
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(asset), copy);
                var loaded = (ProjectileMechanism)copy.mechanismData.mechanism;
                Check(loaded.Destination.mode == DestinationMode.BasePosition && loaded.Destination.position.anchor == BasePositionAnchor.ExitPosition && loaded.speed == 3, "managed-reference round trip preserves new and existing fields");
                Check((int)ExitCause.Cancelled == 12 && (int)ExitCause.DestinationReached == 13, "existing ExitCause values retained");
            }
            finally { Object.DestroyImmediate(asset); Object.DestroyImmediate(copy); }
        }
    }
}
