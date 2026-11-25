using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Combat.Spatial;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Combat.Stats;
using global::NoWebGame.Systems.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;








using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExitLab
{
    /// <summary>S3 누적/준비/소비/직접 실행 경계 및 재진입 검사입니다.</summary>
    [InitializeOnLoad]
    public static class StackTriggerChecks
    {
        private static readonly List<string> Results = new();
        private static readonly List<string> Errors = new();
        private static string _expected;
        private static int _expectedCount;
        static StackTriggerChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("StackS3.Run", false)) return;
                SessionState.SetBool("StackS3.Run", false);
                try { Run(); StackBuffChecks.Run(); StackTransitionChecks.Run(); StackBaselineChecks.Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunPlayMode()
        {
            Guard(); SessionState.SetBool("StackS3.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.EnterPlaymode();
        }
        private static void Guard()
        { if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker")) throw new Exception("Validation copy only"); }
        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); Results.Add("PASS " + name); }
        private static object Get(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Observe(string message, string trace, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            if (_expected != null && message.Contains(_expected)) { _expectedCount++; return; }
            Errors.Add(message);
        }
        private static void ExpectLog(string text, Action action)
        {
            _expected = text; _expectedCount = 0;
            try { action(); Check(_expectedCount == 1, "exactly one isolated exception: " + text); }
            finally { _expected = null; }
        }
        private static void Throws<T>(Action action, string name) where T : Exception
        { try { action(); } catch (T) { Check(true, name); return; } throw new Exception("Missing exception: " + name); }
        // 지원되는 공간 Mechanism을 상속하여 실제 공간 준비를 통과한 입력을 관측합니다.
        private sealed class Probe : TeleportMechanism
        {
            public readonly List<CastContext> Calls = new();
            public Action<CastContext> Action;
            protected override void ExecuteCore(CastContext ctx) { Calls.Add(ctx); Action?.Invoke(ctx); }
        }
        // 실행 준비 중 진단 콜백 실패/재진입을 주입합니다. 런타임에 테스트용 분기를 추가하지 않습니다.
        private sealed class PreparationLog : ILogHandler, IDisposable
        {
            private readonly ILogHandler _previous = Debug.unityLogger.logHandler;
            public Action Callback;
            public PreparationLog(Action callback) { Callback = callback; Debug.unityLogger.logHandler = this; }
            public void LogException(Exception exception, Object context) => _previous.LogException(exception, context);
            public void LogFormat(LogType type, Object context, string format, params object[] args)
            {
                if (type == LogType.Warning && string.Format(format, args).Contains("StackTrigger preparation rejected"))
                { var callback = Callback; Callback = null; callback?.Invoke(); }
                _previous.LogFormat(type, context, format, args);
            }
            public void Dispose() => Debug.unityLogger.logHandler = _previous;
        }
        private sealed class Fixture : IDisposable
        {
            private readonly List<Object> _owned = new();
            public readonly DelayScheduler Scheduler = new();
            public readonly StackManager Manager;
            public readonly Transform Caster;
            public readonly StatsBridge Stats;
            public Fixture()
            {
                var go = new GameObject("S3 actor"); _owned.Add(go); Caster = go.transform; Caster.position = new Vector3(2, 3, 0);
                var owner = go.AddComponent<TemporaryAITarget>();
                var context = new Context(owner, go, Caster, null, null, null, null); context.RegisterScheduler(Scheduler);
                Stats = new StatsBridge(context, new BaseStatsContainer(100, 0, 100, 0, 100, 0, 0)); context.RegisterStats(Stats);
                Manager = new StackManager(context, new VariableStorage());
            }
            public TriggerableDefinition Def(INewMechanism mechanism, int threshold = 3, int max = 3, ushort duration = 5)
            {
                var d = ScriptableObject.CreateInstance<TriggerableDefinition>(); _owned.Add(d);
                d.threshold = threshold; d.maxStacks = max; d.duration = duration;
                d.effect = new MechanismData { mechanism = mechanism, mode = TargetMode.TowardsSelf }; return d;
            }
            public Dictionary<StackKey, StackStatus> States => (Dictionary<StackKey, StackStatus>)Get(Manager, "_stackStorage");
            public Dictionary<StackKey, StackMetadata> Payloads => (Dictionary<StackKey, StackMetadata>)Get(Manager, "_metadata");
            public void Apply(StackKey key, int amount, long tick, int payload = 0) => Manager.ApplyStack(key, amount, tick, metadata: new StackMetadata(payload));
            public bool Empty(StackKey key) => !States.ContainsKey(key) && !Payloads.ContainsKey(key);
            public void Dispose() { foreach (var obj in _owned) if (obj) Object.DestroyImmediate(obj); }
        }
        private static void Spatial(Probe probe, BasePositionAnchor anchor)
        { probe.ConfigureDestination(new DestinationSettings { mode = DestinationMode.BasePosition, position = new BasePositionData { anchor = anchor } }); }
        public static void Run()
        {
            Guard(); Results.Clear(); Errors.Clear(); var scheduler = DelayScheduler.Instance;
            Application.logMessageReceived += Observe;
            try
            {
                AccumulateAndConsume(); RetryAndExpiry(); Failures(); Reentry();
                Check(Errors.Count == 0, "no unexpected Unity errors: " + string.Join(";", Errors));
                Debug.Log("StackTriggerChecks PASS " + Results.Count);
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                DelayScheduler.Instance = scheduler; Application.logMessageReceived -= Observe;
                File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../../stack-s3-results.txt")), Results);
            }
        }
        private static void AccumulateAndConsume()
        {
            using var f = new Fixture(); var p = new Probe(); var d = f.Def(p); var k = new StackKey(d, "A", f.Caster);
            f.Apply(k, 1, 0, 7); f.Apply(k, 1, 1, 2);
            Check(p.Calls.Count == 0 && f.States[k].Amount == 2 && f.Payloads[k].Metadata == 9, "below threshold accumulates quantity and payload without execution");
            p.Action = ctx => Check(f.Empty(k) && f.Scheduler.GetAllEntries().Count == 0 && ctx.Damage.Attack == 12, "payload snapshot survives full consumption before Execute");
            f.Apply(k, 10, 2, 3);
            Check(p.Calls.Count == 1 && f.Empty(k), "overshoot executes once without carry");
            var input = p.Calls[0];
            Check(input.Caster == f.Caster && input.OriginCastPosition == new Vector2(2, 3) && !input.Target.TryGetPosition(out _) && input.Mode == TargetMode.TowardsSelf, "direct contract retains applier Origin, unresolved Target and mode");
            Check(input.Destination == null && input.SwitchSelection == null && input.Var.Variable == null && input.Damage.Type == global::NoWebGame.Systems.Data.DamageType.Normal && input.Damage.Value == 1 && input.Trace.AttackId != 0, "direct payload defaults and trace; no Switch receipt");
            Check(f.Stats.Stats.Mana == 100, "direct trigger spends no root mana");
            p.Action = null; f.Manager.Tick(100);
            Check(p.Calls.Count == 1 && f.Empty(k), "consumed trigger never retries on Tick");
            f.Apply(k, 1, 101, 4); f.Apply(k, -1, 102, 50);
            Check(f.Empty(k) && f.Scheduler.GetAllEntries().Count == 0, "negative-to-zero ends payload and reservation without firing");
            f.Apply(k, 0, 103, 9); Check(f.Empty(k), "absent zero request creates no payload");
            var b = new StackKey(d, "B", f.Caster); f.Apply(k, 1, 104, 1); f.Apply(b, 1, 104, 2); f.Manager.Tick(109);
            Check(f.Empty(k) && f.Empty(b), "independent keys expire quantity and payload");
            f.Apply(k, 3, 110, 6); Check(p.Calls.Count == 2 && p.Calls[1].Damage.Attack == 6, "reapply after expiry has no old payload");
        }
        private static void RetryAndExpiry()
        {
            using var f = new Fixture(); var p = new Probe(); Spatial(p, BasePositionAnchor.InputTargetPosition);
            var d = f.Def(p); var k = new StackKey(d, "A", f.Caster);
            f.Apply(k, 3, 0, 7);
            Check(p.Calls.Count == 0 && f.States[k].Amount == 3 && f.Payloads[k].Metadata == 7, "missing Target spatial preparation preserves full stack");
            Spatial(p, BasePositionAnchor.CasterPosition); f.Manager.Tick(1);
            Check(p.Calls.Count == 0 && f.States[k].Amount == 3, "Tick does not retry after preparation becomes possible");
            f.Apply(k, 1, 2, 2);
            Check(p.Calls.Count == 1 && p.Calls[0].Damage.Attack == 9 && p.Calls[0].Destination == new Vector2(2, 3) && f.Empty(k), "capped new request retries with added payload and prepared Destination");
            Spatial(p, BasePositionAnchor.InputTargetPosition); f.Apply(k, 3, 3, 5);
            Spatial(p, BasePositionAnchor.CasterPosition); f.Apply(k, 0, 4);
            Check(p.Calls.Count == 2 && p.Calls[1].Damage.Attack == 5 && f.Empty(k), "zero-amount zero-payload request retries existing accumulation");
            Spatial(p, BasePositionAnchor.InputTargetPosition); f.Apply(k, 3, 5, 8); f.Apply(k, 0, 6); f.Manager.Tick(10);
            Check(f.States.ContainsKey(k) && f.Payloads[k].Metadata == 8, "failed retry refreshes duration without consuming");
            f.Manager.Tick(11); Check(f.Empty(k) && f.Scheduler.GetAllEntries().Count == 0, "failed preparation eventually expires whole cycle");
            Spatial(p, BasePositionAnchor.CasterPosition); f.Apply(k, 3, 12, 1);
            Check(p.Calls.Count == 3 && p.Calls[2].Damage.Attack == 1, "new cycle after failed expiry starts fresh");
            d.maxStacks = 10; Spatial(p, BasePositionAnchor.InputTargetPosition); f.Apply(k, 6, 13, 6);
            Spatial(p, BasePositionAnchor.CasterPosition); f.Apply(k, -2, 14, 1);
            Check(p.Calls.Count == 4 && p.Calls[3].Damage.Attack == 7 && f.Empty(k), "negative request still above threshold retries once");
        }
        private static void Failures()
        {
            using var f = new Fixture(); var p = new Probe(); var d = f.Def(p); var k = new StackKey(d, "A", f.Caster);
            d.effect = null; f.Apply(k, 3, 0, 2);
            Check(f.States[k].Amount == 3 && f.Payloads[k].Metadata == 2, "null effect is preparation rejection, not consumption");
            d.effect = new MechanismData(); f.Apply(k, 0, 1);
            Check(f.States[k].Amount == 3 && f.Payloads[k].Metadata == 2, "null mechanism preserves accumulation");
            d.effect.mechanism = p; d.threshold = 0; f.Apply(k, 0, 2);
            Check(p.Calls.Count == 0 && f.States[k].Amount == 3, "nonpositive threshold cannot fire");
            d.threshold = 4; f.Apply(k, 0, 3);
            Check(p.Calls.Count == 0 && f.States[k].Amount == 3, "threshold above maximum cannot fire");
            d.threshold = 3; f.Apply(k, 0, 4); Check(p.Calls.Count == 1 && f.Empty(k), "fixed definition retries on new request");
            f.Apply(k, 1, 5, int.MaxValue); var old = f.States[k]; f.Scheduler.ModifyDelta(old.DelayId, -2);
            Throws<OverflowException>(() => f.Apply(k, 1, 6, 1), "payload overflow rejects complete request");
            Check(f.States[k].Amount == 1 && f.Payloads[k].Metadata == int.MaxValue && f.States[k].DelayId.Equals(old.DelayId) && f.Scheduler.GetRemaining(old.DelayId, 5) == 7, "overflow preserves exact state, payload and corrected timer");
            Throws<OverflowException>(() => f.Apply(k, 1, long.MaxValue), "reservation overflow rejects request");
            Check(f.States[k].Amount == 1 && f.Payloads[k].Metadata == int.MaxValue && f.Scheduler.GetAllEntries().Count == 1, "reservation failure leaves no new payload or delay");
            f.Manager.Tick(12); Check(f.Empty(k), "failed requests leave original expiry functional");
            d.effect = null;
            using (var hook = new PreparationLog(() => { f.Manager.ApplyStack(k, 0, 13); throw new Exception("S3ExpectedPreparation"); }))
                ExpectLog("S3ExpectedPreparation", () => f.Apply(k, 3, 13, 5));
            Check(f.States[k].Amount == 3 && f.Payloads[k].Metadata == 5 && p.Calls.Count == 1, "preparation callback exception keeps cycle unconsumed");
            d.effect = new MechanismData { mechanism = p }; f.Manager.Tick(14);
            Check(p.Calls.Count == 2 && p.Calls[1].Damage.Attack == 5 && f.Empty(k), "preparation reentry retries same unconsumed cycle next Tick");
            p.Action = _ => throw new Exception("S3ExpectedExecution");
            ExpectLog("S3ExpectedExecution", () => f.Apply(k, 3, 15, 7));
            Check(f.Empty(k) && p.Calls.Count == 3, "direct execution exception remains consumed and does not escape");
            p.Action = null; f.Manager.Tick(20); Check(p.Calls.Count == 3, "execution exception never auto-retries");
            // 본문이 효과를 만들지 않고 반환해도 준비 성공/소비를 되돌리지 않습니다.
            d.effect.mechanism = new TeleportMechanism(); f.Apply(k, 3, 21, 9);
            Check(f.Empty(k), "body returning without teleport target does not restore consumed stack");
        }
        private static void Reentry()
        {
            using var f = new Fixture(); var p = new Probe(); var d = f.Def(p, 1); var k = new StackKey(d, "A", f.Caster);
            p.Action = _ =>
            {
                if (p.Calls.Count != 1) return;
                f.Manager.ApplyStack(k, 1, 0, 9, new StackMetadata(4)); f.Manager.Tick(0);
                Check(f.Empty(k), "Execute reentry cannot mutate old consumed cycle or swap buffer");
                throw new Exception("S3ExpectedReentry");
            };
            ExpectLog("S3ExpectedReentry", () => f.Apply(k, 1, 0, 2));
            d.threshold = 3; f.Manager.Tick(1);
            Check(p.Calls.Count == 1 && f.States[k].Amount == 1 && f.Payloads[k].Metadata == 4 && f.Scheduler.GetRemaining(f.States[k].DelayId, 1) == 9, "deferred direct request starts new cycle with override and fresh payload");
            f.Apply(k, 2, 2, 1); Check(p.Calls.Count == 2 && p.Calls[1].Damage.Attack == 5 && f.Empty(k), "new cycle survives previous execution exception and cleanup");
            d.threshold = 1; p.Action = _ => { if (p.Calls.Count == 3) f.Manager.EnqueueStack(k, 1, new StackMetadata(8)); };
            f.Manager.EnqueueStack(k, 1, new StackMetadata(6)); f.Manager.EnqueueStack(k, 1, new StackMetadata(7)); f.Manager.Tick(3);
            Check(p.Calls.Count == 4 && p.Calls[2].Damage.Attack == 6 && p.Calls[3].Damage.Attack == 7 && f.Empty(k), "two original queue requests may each execute one fresh cycle in same Tick");
            f.Manager.Tick(4); Check(p.Calls.Count == 5 && p.Calls[4].Damage.Attack == 8, "Enqueue during execution waits until next Tick");
            var other = new StackKey(d, "B", f.Caster); d.threshold = 2; f.Apply(other, 1, 5, 1);
            p.Action = _ => throw new Exception("S3ExpectedQueue");
            f.Manager.EnqueueStack(k, 2, new StackMetadata(9));
            var waiting = new StackKey(d, "C", f.Caster); f.Manager.EnqueueStack(waiting, 1, new StackMetadata(10));
            ExpectLog("S3ExpectedQueue", () => f.Manager.Tick(10));
            Check(f.Empty(k) && f.Empty(other) && f.States[waiting].Amount == 1 && f.Payloads[waiting].Metadata == 10, "execution failure does not stop next request or other expired key");
            f.Manager.Tick(15); Check(f.Empty(waiting) && p.Calls.Count == 6, "failed queue item never replays and remaining key expires");
        }
    }
}
