#if UNITY_EDITOR
using global::NoWebGame.Characters;
using global::NoWebGame.Combat.Definitions;
using global::NoWebGame.Combat.Effects.Runtime;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Actions;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Combat.Spatial;
using global::NoWebGame.Combat.Stats;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Entities.Relations;
using global::NoWebGame.Movement;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Time;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;










using UnityEditor;
using UnityEngine;
using GameTime = global::NoWebGame.Systems.Time.Time;

namespace ExitLab
{
    public sealed class DestinationPlayModeRunner : MonoBehaviour
    {
        private sealed class Record : DummyMechanism
        {
            public int Count; public CastContext Last;
            protected override void ExecuteCore(CastContext ctx) { Count++; Last = ctx; }
        }
        private readonly List<string> _results = new();
        private readonly List<string> _errors = new();
        private void Check(bool ok, string name) { if (!ok) throw new Exception(name); _results.Add("PASS " + name); }
        private void Capture(string message, string stack, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) _errors.Add(message); }
        private IEnumerator Start()
        {
            Application.logMessageReceived += Capture;
            Physics2D.simulationMode = SimulationMode2D.Script; Physics2D.queriesHitTriggers = true; Physics2D.queriesStartInColliders = true;
            var success = false;
            try
            {
                // 기존 0 Tick 시나리오와 동일한 물리/종료 계약을 두 시간 경계에서도 확인합니다.
                foreach (var startTick in new[] { 0L, 65534L, (long)int.MaxValue - 1 })
                foreach (var dash in new[] { false, true })
                foreach (var scenario in new[] { "arrival", "lost", "target", "wall", "pierce", "nonpierce", "timeout", "blocked" })
                    RunCase(dash, scenario, startTick);
                Check(_errors.Count == 0, "no runtime errors: " + string.Join(" | ", _errors));
                success = true;
            }
            catch (Exception error) { _results.Add("FAIL " + error); }
            File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../destination-playmode-tests.txt"), _results);
            Debug.Log($"DestinationPlayMode {(success ? "PASS" : "FAIL")} {_results.Count}");
            Application.logMessageReceived -= Capture;
            yield return null;
            EditorApplication.Exit(success ? 0 : 1);
        }
        private void RunCase(bool dash, string scenario, long startTick)
        {
            var label = $"{(dash ? "Dash" : "Projectile")} {scenario} start={startTick}";
            var owned = new List<GameObject>();
            GameObject Go(string name, Vector2 position)
            { var go = new GameObject(name); go.transform.position = position; owned.Add(go); return go; }
            RelativeTargetTestEntity Actor(string name, Vector2 position, string layer)
            {
                var go = Go(name, position); go.layer = LayerMask.NameToLayer(layer);
                var actor = go.AddComponent<RelativeTargetTestEntity>();
                var child = Go(name + " collider", position); child.transform.SetParent(go.transform);
                var col = child.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = .15f;
                return actor;
            }
            GameTime.Ticker = new Ticker(); GameTime.DelayScheduler = new DelayScheduler();
            typeof(Ticker).GetProperty("TickCount").SetValue(GameTime.Ticker, startTick);
            typeof(Ticker).GetProperty("Instance").SetValue(null, GameTime.Ticker); DelayScheduler.Instance = GameTime.DelayScheduler;
            var services = Go("services", new Vector2(30, 30)); services.AddComponent<TargetResolver>(); var collector = services.AddComponent<CommandCollector>();
            var caster = Actor("caster", Vector2.zero, "Foe");
            var rb = caster.gameObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
            var col = caster.GetComponentInChildren<Collider2D>();
            var target = Actor("target", new Vector2(scenario == "target" ? 1 : 10, 0), "You");
            if (scenario is "pierce" or "nonpierce") Actor("bystander", Vector2.right, "You");
            if (scenario == "wall") { var wall = Go("wall", Vector2.right); wall.layer = LayerMask.NameToLayer("Walls&Obstacles"); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 2); }
            var context = new Context(caster, caster.gameObject, caster.transform, services.GetComponent<TargetResolver>(), collector, null, new global::NoWebGame.Combat.Runtime.Logger("DestinationTest"));
            context.RegisterScheduler(GameTime.DelayScheduler);
            var stats = new StatsBridge(context, new BaseStatsContainer(1000, 0, 0, 0, 100, 0, 1000)); context.RegisterStats(stats);
            var mover = new Mover(context, stats, rb, col);
            caster.Act = new ActBridge(mover, new Attacker(context, caster.transform, new Dictionary<SkillSlot, SkillBinding>(), collector));
            var hit = new Record(); var done = new Record(); var interrupted = new Record();
            List<MechanismData> Follow(Record record) => new() { new MechanismData { mechanism = record } };
            var speed = scenario == "blocked" ? 0 : scenario == "timeout" ? 1 : 60;
            var duration = (ushort)(scenario == "timeout" ? 1 : 20);
            NewMechanism mech;
            if (dash) mech = new DashMechanism { speed = speed, durationTicks = duration, penetrative = scenario != "nonpierce", callbackMode = ExitCallbackMode.Classified,
                timeoutPolicy = TimeoutPolicy.Interrupted, onHit = Follow(hit), onCompleted = Follow(done), onInterrupted = Follow(interrupted) };
            else
            {
                var data = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/ObjectInstances/SkillData/SkillData 2.asset");
                var prefab = ((ProjectileMechanism)data.mechanismData.mechanism).projectilePrefab;
                mech = new ProjectileMechanism { projectilePrefab = prefab, speed = speed, lifeTick = duration, penetrative = scenario != "nonpierce",
                    callbackMode = ExitCallbackMode.Classified, timeoutPolicy = TimeoutPolicy.Interrupted, onHit = Follow(hit), onCompleted = Follow(done), onInterrupted = Follow(interrupted) };
            }
            mech.ConfigureRelativeTargets(TargetRelationMask.Enemy);
            mech.ConfigureDestination(new DestinationSettings { mode = DestinationMode.BasePosition, position = new BasePositionData { distance = 3 } });
            try
            {
                mech.Execute(new CastContext(mech, caster.transform, SkillTarget.ForEntity(target), default, source: label));
                if (scenario == "lost") target.gameObject.SetActive(false);
                Physics2D.SyncTransforms();
                for (var i = 0; i < 25; i++)
                {
                    GameTime.Ticker.Step(); caster.Act.Tick(GameTime.Ticker.TickCount);
                    Physics2D.Simulate(1f / 60); mover.AfterDashPhysics();
                    foreach (var p in FindObjectsByType<ProjectileEntity>(FindObjectsSortMode.None)) p.AfterPhysics();
                }
                var failed = scenario is "wall" or "timeout" or "blocked";
                Check(done.Count == (failed ? 0 : 1) && interrupted.Count == (failed ? 1 : 0), label + " classified callback exactly once");
                var final = failed ? interrupted.Last : done.Last;
                var expected = scenario switch { "target" => ExitCause.TargetHit, "wall" => ExitCause.WallBlocked, "nonpierce" => ExitCause.EnemyHit,
                    "timeout" => ExitCause.TimeExpired, "blocked" => ExitCause.MovementBlocked, _ => ExitCause.DestinationReached };
                Check(final.Exit?.Cause == expected, label + " cause=" + expected);
                Check(hit.Count == (scenario is "target" or "pierce" or "nonpierce" ? 1 : 0), label + " actual onHit only");
                Check(!final.Destination.HasValue && final.Target.Matches(target), label + " child retains Target, not parent Destination");
                if (scenario is "arrival" or "lost" or "pierce") Check(Vector2.Distance(final.Exit.Value.Position, new Vector2(3, 0)) < .01f, label + " actual fixed endpoint");
            }
            finally
            {
                foreach (var p in FindObjectsByType<ProjectileEntity>(FindObjectsSortMode.None)) DestroyImmediate(p.gameObject);
                for (var i = owned.Count - 1; i >= 0; i--) if (owned[i]) DestroyImmediate(owned[i]);
            }
        }
    }
}
#endif
