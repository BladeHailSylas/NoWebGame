using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Actions;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Combat.Stats;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Movement;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;











using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using GameTime = global::NoWebGame.Systems.Time.Time;

namespace ExitLab
{
    /// <summary>짧은 에셋 지속시간은 유지하면서 절대 시각만 커지는 경계를 검사합니다.</summary>
    public static class TimeSystemChecks
    {
        private static readonly List<string> Results = new();
        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); Results.Add("PASS " + name); }
        private static void SetTick(Ticker ticker, long tick) =>
            typeof(Ticker).GetProperty("TickCount").SetValue(ticker, tick);
        private static void SetTime(Ticker ticker, DelayScheduler scheduler)
        {
            typeof(GameTime).GetProperty("Ticker").SetValue(null, ticker);
            typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, scheduler);
        }
        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); } catch (T) { Check(true, name); return; }
            throw new Exception("Expected " + typeof(T).Name + ": " + name);
        }
        private sealed class Probe : DummyMechanism
        {
            public int Count;
            protected override void ExecuteCore(CastContext ctx) { Count++; }
        }

        public static void RunWithRegression()
        {
            Run();
            BasePositionChecks.RunWithRegression();
            TargetContractChecks.Run();
        }

        public static void Run()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Validation copy only.");
            Results.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var oldTicker = GameTime.Ticker; var oldScheduler = GameTime.DelayScheduler;
            var oldInstance = Ticker.Instance; var oldSchedulerInstance = DelayScheduler.Instance;
            try
            {
                ClockChecks(); SchedulerChecks();
                foreach (var start in new[] { 65534L, (long)int.MaxValue - 1 }) IntegrationChecks(start);
                Debug.Log($"TimeSystemChecks PASS {Results.Count}");
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                SetTime(oldTicker, oldScheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, oldInstance);
                DelayScheduler.Instance = oldSchedulerInstance;
                File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../phase1-time-tests.txt"), Results);
            }
        }

        private static void ClockChecks()
        {
            var ticker = new Ticker(); var events = 0; long last = 0;
            ticker.OnTick += tick => { events++; last = tick; };
            for (var i = 0; i < 65540; i++) ticker.Step();
            Check(events == 65540 && last == 65540 && ticker.TickCount == 65540, "65535 boundary emits every tick without wrap");
            var calls = 0; long calledAt = 0;
            ticker.Schedule(3, tick => { calls++; calledAt = tick; });
            ticker.Step(); ticker.Step(); Check(calls == 0, "Schedule waits full duration");
            ticker.Step(); ticker.Step(); Check(calls == 1 && calledAt == 65543, "Schedule fires once at current plus duration");
            ticker.Schedule(1, _ => throw new InvalidOperationException("probe"));
            Throws<InvalidOperationException>(() => ticker.Step(), "scheduled exception remains observable");
            ticker.Step(); Check(true, "throwing one-shot is detached before callback");
            Throws<ArgumentOutOfRangeException>(() => ticker.Schedule(0, _ => { }), "zero one-shot rejected");
            SetTick(ticker, long.MaxValue);
            Throws<OverflowException>(() => ticker.Step(), "long boundary fails instead of wrapping");
            Check(ticker.TickCount == long.MaxValue, "overflow leaves clock unchanged");
        }

        private static void SchedulerChecks()
        {
            var scheduler = new DelayScheduler();
            foreach (var start in new[] { 65534L, (long)int.MaxValue - 1 })
            {
                var id = scheduler.Start(start, 4);
                Check(!scheduler.IsCompleted(id, start + 3), "delay not early at " + start);
                Check(scheduler.GetRemaining(id, start + 3) == 1, "remaining agrees at " + start);
                Check(scheduler.IsCompleted(id, start + 4), "delay completes across boundary at " + start);
                scheduler.ModifyDelta(id, -2);
                Check(!scheduler.IsCompleted(id, start + 4) && scheduler.GetRemaining(id, start + 4) == 2, "negative delta postpones");
                scheduler.ModifyDelta(id, 3);
                Check(scheduler.IsCompleted(id, start + 3), "positive delta accelerates");
                scheduler.Remove(id);
                Check(!scheduler.IsCompleted(id, start + 10) && scheduler.GetRemaining(id, start + 10) == 0, "removed handle cannot complete");
            }
            var zero = scheduler.Start(65535, 0);
            Check(scheduler.IsCompleted(zero, 65535), "zero delay completes on scheduling tick");
            var other = new DelayScheduler(); var otherId = other.Start(0, 1);
            other.Remove(zero);
            Check(!zero.Equals(otherId) && other.IsCompleted(otherId, 1), "foreign handle cannot remove new scheduler entry");
            Throws<ArgumentOutOfRangeException>(() => scheduler.Start(0, -1), "negative duration rejected");
            Throws<OverflowException>(() => scheduler.Start(long.MaxValue, 1), "unrepresentable deadline rejected");
            var edge = scheduler.Start(long.MaxValue - 1, 1); scheduler.ModifyDelta(edge, 3);
            Check(scheduler.IsCompleted(edge, long.MaxValue) && scheduler.GetRemaining(edge, long.MaxValue) == 0, "positive observation overflow is safe");
        }

        private static void IntegrationChecks(long start)
        {
            var ticker = new Ticker(); SetTick(ticker, start); SetTime(ticker, new DelayScheduler());
            var go = new GameObject("time actor"); var service = new GameObject("time collector");
            var finite = ScriptableObject.CreateInstance<VariableDefinition>();
            var periodic = ScriptableObject.CreateInstance<VariableDefinition>();
            var permanent = ScriptableObject.CreateInstance<VariableDefinition>();
            var buff = ScriptableObject.CreateInstance<BuffDefinition>();
            var cc = ScriptableObject.CreateInstance<CCDefinition>();
            CommandCollector collector = null;
            try
            {
                var actor = go.AddComponent<RelativeTargetTestEntity>();
                var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
                var collider = go.AddComponent<CircleCollider2D>();
                var context = new Context(actor, go, go.transform, null, null, null, null);
                context.RegisterScheduler(GameTime.DelayScheduler);
                var storage = new VariableStorage(); var manager = new StackManager(context, storage);
                finite.duration = 3; periodic.isPeriodic = true; periodic.periodTick = 2; periodic.maxStacks = 2;
                permanent.duration = 65535;
                finite.exclusiveGroup = Array.Empty<global::NoWebGame.Combat.Stacks.Definitions.ExclusiveGroup>();
                periodic.exclusiveGroup = Array.Empty<global::NoWebGame.Combat.Stacks.Definitions.ExclusiveGroup>();
                permanent.exclusiveGroup = Array.Empty<global::NoWebGame.Combat.Stacks.Definitions.ExclusiveGroup>();
                var finiteKey = new StackKey(finite); var periodicKey = new StackKey(periodic); var permanentKey = new StackKey(permanent);
                manager.ApplyStack(finiteKey, 1, start); manager.ApplyStack(periodicKey, 1, start); manager.ApplyStack(permanentKey, 1, start);
                manager.Tick(start + 2);
                Check(storage.Has(finite) && storage.GetVariable(periodic).Amount == 2, "Stack period and unexpired variable at " + start);
                manager.Tick(start + 3);
                Check(!storage.Has(finite) && storage.Has(permanent), "Stack expiration and permanent sentinel at " + start);
                manager.ConsumeVariable(periodicKey, 1, start + 3); manager.Tick(start + 5);
                Check(storage.GetVariable(periodic).Amount == 2, "recharge after consumption at " + start);

                service.AddComponent<TargetResolver>(); collector = service.AddComponent<CommandCollector>();
                // 일반 MonoBehaviour의 수명 함수는 EditMode에서 직접 호출해야 합니다.
                typeof(CommandCollector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(collector, null);
                var probe = new Probe(); JsonUtility.FromJsonOverwrite("{\"delayTicks\":3}", probe);
                collector.EnqueueCommand(new SkillCommand(go.transform, TargetMode.TowardsCoordinate, default, probe, default, SkillTarget.At(Vector2.right)));
                ticker.Step(); ticker.Step(); ticker.Step();
                Check(probe.Count == 0, "Collector retains existing schedule-based delay at " + start);
                ticker.Step(); ticker.Step(); Check(probe.Count == 1, "Collector dispatches once across boundary at " + start);

                var stats = new StatsBridge(context, new BaseStatsContainer(100, 0, 0, 0, 100, 0, 1000));
                var mover = new Mover(context, stats, body, collider);
                var act = new ActBridge(mover, null);
                context.RegisterStats(stats); context.RegisterAct(act);
                buff.Type = EffectType.DamageBoost; buff.Value = 10; buff.duration = 3;
                cc.Type = EffectType.Stunned; cc.duration = 3;
                manager.ApplyStack(new StackKey(buff), 1, start);
                manager.ApplyStack(new StackKey(cc), 1, start);
                manager.Tick(start + 2);
                Check(stats.Stats.AttackDamage == 110 && !act.CanAttack, "Buff and CC remain before deadline at " + start);
                manager.Tick(start + 3);
                Check(stats.Stats.AttackDamage == 100 && act.CanAttack && act.CanMove, "Buff and CC expire across boundary at " + start);
                var dash = new DashMechanism { durationTicks = 3, callbackMode = ExitCallbackMode.Classified };
                var ctx = new CastContext(dash, go.transform, SkillTarget.At(new Vector2(100, 0)), default);
                act.AddDashContract(new DashContract(ctx, 3, 60, false, false, new(), new(), false));
                Check(mover.IsDashing, "Dash accepts long absolute deadline at " + start);
                mover.DashTick(ticker.TickCount + 3, 0);
                Check(!mover.IsDashing && mover.LastDashResult?.Cause == ExitCause.TimeExpired, "Dash expires across boundary at " + start);
            }
            finally
            {
                if (collector != null) typeof(CommandCollector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(collector, null);
                Object.DestroyImmediate(service); Object.DestroyImmediate(go);
                Object.DestroyImmediate(finite); Object.DestroyImmediate(periodic); Object.DestroyImmediate(permanent);
                Object.DestroyImmediate(buff); Object.DestroyImmediate(cc);
            }
        }
    }
}
