using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Characters;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using Systems.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using GameTime = Systems.Time.Time;

namespace ExitLab
{
    [InitializeOnLoad]
    public static class CastCommitChecks
    {
        private static readonly List<string> Results = new();
        private static readonly List<string> Errors = new();
        private static bool _expectedObserverError;
        private sealed class Probe : DummyMechanism
        {
            public int Count;
            public CastContext Last;
            public Action OnExecute;
            protected override void ExecuteCore(CastContext ctx) { Count++; Last = ctx; OnExecute?.Invoke(); }
        }
        private sealed class Fixture : IDisposable
        {
            public readonly List<Object> Owned = new();
            public readonly List<SkillCommand> Commands = new();
            public readonly Dictionary<SkillSlot, SkillBinding> Bindings = new();
            public readonly Ticker Clock;
            public readonly DelayScheduler Scheduler;
            public readonly CommandCollector Collector;
            public readonly TemporaryAITarget Caster;
            public readonly StatsBridge Stats;
            public readonly VariableStorage Storage;
            public readonly StackManager Stacks;
            public readonly Attacker Attacker;
            public Fixture()
            {
                Clock = new Ticker(); Scheduler = new DelayScheduler();
                typeof(GameTime).GetProperty("Ticker").SetValue(null, Clock);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, Scheduler);
                var services = Go("Commit services"); services.SetActive(false);
                var resolver = services.AddComponent<TargetResolver>();
                Collector = services.AddComponent<CommandCollector>(); services.SetActive(true);
                if (!Application.isPlaying) Call(Collector, "OnEnable");
                Collector.CommandEnqueued += Commands.Add;
                Caster = Go("Commit caster").AddComponent<TemporaryAITarget>();
                var context = new Context(Caster, Caster.gameObject, Caster.transform, resolver, Collector, null, new PlayerScripts.Core.Logger("Commit test"));
                context.RegisterScheduler(Scheduler);
                Stats = new StatsBridge(context, new BaseStatsContainer(100, 0, 100, 0, 1, 0, 0)); context.RegisterStats(Stats);
                Storage = new VariableStorage();
                Stacks = new StackManager(context, Storage); context.RegisterStackManager(Stacks);
                Attacker = new Attacker(context, Caster.transform, Bindings, Collector);
                // 실제 Summon 초기화처럼 Attacker 생성 뒤 등록해도 소비 경로가 유효해야 합니다.
                context.RegisterVariableStorage(Storage);
            }
            public GameObject Go(string name) { var go = new GameObject(name); Owned.Add(go); return go; }
            public SkillData Bind(INewMechanism mechanism, int cost = 0, SkillSlot slot = SkillSlot.Attack)
            {
                var skill = ScriptableObject.CreateInstance<SkillData>(); Owned.Add(skill); skill.ManaCost = cost;
                skill.mechanismData = new MechanismData { mechanism = mechanism, mode = TargetMode.TowardsSelf };
                Bindings[slot] = new SkillBinding { skillData = skill }; return skill;
            }
            public void Cast(params SkillSlot[] slots)
            {
                foreach (var slot in slots) Attacker.EnqueueCast(slot);
                Attacker.Tick(Clock.TickCount, 0);
            }
            public void Step(int count = 1) { for (var i = 0; i < count; i++) Clock.Step(); }
            public VariableDefinition Variable(bool periodic = false)
            {
                var def = ScriptableObject.CreateInstance<VariableDefinition>(); Owned.Add(def);
                def.maxStacks = 3; def.duration = ushort.MaxValue; def.isPeriodic = periodic; def.periodTick = 5;
                return def;
            }
            public void Dispose()
            {
                Collector.CeaseCommand();
                if (!Application.isPlaying) Call(Collector, "OnDisable");
                for (var i = Owned.Count - 1; i >= 0; i--) if (Owned[i]) Object.DestroyImmediate(Owned[i]);
            }
        }
        static CastCommitChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CastCommit.Run", false)) return;
                SessionState.SetBool("CastCommit.Run", false);
                try { Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunWithRegression() { Run(); CastPreparationChecks.RunWithRegression(); }
        public static void RunPlayMode()
        {
            Guard(); SessionState.SetBool("CastCommit.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.EnterPlaymode();
        }
        private static void Guard()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker")) throw new Exception("Validation copy only");
        }
        public static void Run()
        {
            Guard(); Results.Clear(); Errors.Clear();
            if (!Application.isPlaying) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var oldTicker = GameTime.Ticker; var oldScheduler = GameTime.DelayScheduler;
            var oldTickerInstance = Ticker.Instance; var oldSchedulerInstance = DelayScheduler.Instance;
            var oldCollector = CommandCollector.Instance;
            Application.logMessageReceived += Observe;
            try
            {
                ManaAndLocks(); Timing(); SwitchCosts(); FailureAndObservers(); Serialization();
                Check(Errors.Count == 0, "no unexpected runtime errors: " + string.Join(";", Errors));
                Debug.Log($"CastCommitChecks PASS {Results.Count}");
            }
            catch (Exception e) { Results.Add("FAIL " + e); throw; }
            finally
            {
                Application.logMessageReceived -= Observe;
                typeof(GameTime).GetProperty("Ticker").SetValue(null, oldTicker);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, oldScheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, oldTickerInstance); DelayScheduler.Instance = oldSchedulerInstance;
                typeof(CommandCollector).GetProperty("Instance").SetValue(null, oldCollector);
                File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../../cast-commit-" + (Application.isPlaying ? "play" : "edit") + ".txt")), Results);
            }
        }
        private static Probe MakeProbe(int delay = 0, int cooldown = 0)
        {
            var probe = new Probe(); JsonUtility.FromJsonOverwrite($"{{\"delayTicks\":{delay},\"cooldownTicks\":{cooldown}}}", probe); return probe;
        }
        private static void ManaAndLocks()
        {
            using var f = new Fixture(); var probe = MakeProbe(5, 2); var skill = f.Bind(probe, 30);
            Check(!f.Stats.TrySpendMana(-1) && !f.Stats.TrySpendMana(101) && f.Stats.Stats.Mana == 100, "negative / insufficient Mana leave value unchanged");
            Check(f.Stats.TrySpendMana(0) && f.Stats.Stats.Mana == 100, "zero Mana cost allowed");
            f.Attacker.PrepareCast(SkillSlot.Attack, 0);
            Check(f.Stats.Stats.Mana == 100 && f.Scheduler.GetAllEntries().Count == 0, "PrepareCast reserves and consumes nothing");
            f.Cast(SkillSlot.Attack, SkillSlot.Attack);
            Check(f.Commands.Count == 1 && f.Stats.Stats.Mana == 70 && f.Attacker.IsPending(SkillSlot.Attack), "same tick same slot commits once and spends immediately");
            var root = f.Commands[0].Commit;
            Check(root.State == CastCommitState.Committed && root.CommitTick == 0 && root.DueTick == 5, "root publishes Commit and absolute due tick");
            f.Step(2); f.Cast(SkillSlot.Attack);
            Check(f.Commands.Count == 1 && f.Stats.Stats.Mana == 70, "Pending blocks reactivation after shorter CD ends");
            f.Step(3);
            Check(probe.Count == 1 && !f.Attacker.IsPending(SkillSlot.Attack), "Pending ends on dispatch, not effect lifetime");
            f.Cast(SkillSlot.Attack); Check(f.Commands.Count == 2 && f.Stats.Stats.Mana == 40, "slot reusable after dispatch and CD");
            f.Collector.CeaseCommand();
            Check(f.Stats.Stats.Mana == 40 && f.Commands[1].Commit.State == CastCommitState.Cancelled && !f.Attacker.IsPending(SkillSlot.Attack), "cancel releases Pending without refund");
            f.Step(2); f.Bind(probe, 50, SkillSlot.Skill1); f.Cast(SkillSlot.Skill1);
            Check(f.Commands.Count == 2 && f.Stats.Stats.Mana == 40, "insufficient root Mana creates no command");
            skill.ManaCost = -1; f.Cast(SkillSlot.Attack);
            Check(f.Commands.Count == 2 && f.Stats.Stats.Mana == 40, "negative serialized cost rejected at runtime");
            f.Bind(MakeProbe(0, -1), 0); f.Cast(SkillSlot.Attack);
            Check(f.Commands.Count == 2, "negative cooldown rejected");
            using var g = new Fixture();
            g.Bind(MakeProbe(), 60); g.Bind(MakeProbe(), 60, SkillSlot.Skill1); g.Cast(SkillSlot.Attack, SkillSlot.Skill1);
            Check(g.Commands.Count == 1 && g.Stats.Stats.Mana == 40, "different slots compete for remaining Mana in input order");
        }
        private static void Timing()
        {
            using (var f = new Fixture())
            {
                var probe = MakeProbe(); f.Bind(probe, 10);
                // 입력 구독이 Collector 뒤에 있어도 다음 수집 시점부터 Delay를 다시 계산하지 않습니다.
                f.Clock.OnTick += tick => { if (tick == 1) f.Cast(SkillSlot.Attack, SkillSlot.Attack); };
                f.Step(); Check(probe.Count == 0 && f.Commands[0].Commit.DueTick == 1, "zero Delay does not execute inside input callback");
                f.Step(); Check(probe.Count == 1 && f.Stats.Stats.Mana == 90, "zero Delay dispatches at next available collector pass");
                f.Collector.EnqueueCommand(f.Commands[0]); f.Step();
                Check(probe.Count == 1, "committed root cannot be re-enqueued for free replay");
            }
            using (var f = new Fixture())
            {
                Call(f.Collector, "OnDisable");
                f.Clock.OnTick += tick => { if (tick == 1) f.Cast(SkillSlot.Attack); };
                Call(f.Collector, "OnEnable");
                var probe = MakeProbe(); f.Bind(probe, 10); f.Step();
                Check(probe.Count == 1 && f.Commands[0].Commit.CommitTick == 1, "input before collector permits same-tick zero Delay dispatch");
                f.Cast(SkillSlot.Attack);
                Check(f.Commands.Count == 1, "same Tick remains locked even after zero Delay dispatch releases Pending");
            }
            using (var f = new Fixture())
            {
                var probe = MakeProbe(2, 8); f.Bind(probe, 10); f.Cast(SkillSlot.Attack); f.Step(2); f.Cast(SkillSlot.Attack);
                Check(probe.Count == 1 && f.Commands.Count == 1 && !f.Attacker.IsPending(SkillSlot.Attack), "longer CD blocks after Pending ends");
                f.Step(6); f.Cast(SkillSlot.Attack); Check(f.Commands.Count == 2, "CD starts at CommitTick and expires exactly");
            }
            using (var f = new Fixture())
            {
                var probe = MakeProbe(3); f.Bind(probe); f.Cast(SkillSlot.Attack);
                typeof(Ticker).GetProperty("TickCount").SetValue(f.Clock, 9L); f.Step();
                Check(probe.Count == 1, "late collector does not restart committed Delay");
            }
        }
        private static SwitchMechanism Switch(VariableDefinition variable, INewMechanism branch, bool consume = true)
        {
            return new SwitchMechanism { Consume = consume, cases = new[] { new SwitchCase { variable = variable,
                followUp = new MechanismData { mechanism = branch, mode = TargetMode.TowardsSelf } } } };
        }
        private static void SwitchCosts()
        {
            using (var f = new Fixture())
            {
                var def = f.Variable(true); var a = new StackKey(def, "A"); var b = new StackKey(def, "B");
                f.Stacks.ApplyStack(a, 3, 0); f.Stacks.ApplyStack(b, 3, 0);
                var probe = MakeProbe(); var other = MakeProbe(); var sw = Switch(def, probe); JsonUtility.FromJsonOverwrite("{\"delayTicks\":3}", sw);
                f.Bind(sw, 10); f.Cast(SkillSlot.Attack);
                Check(!f.Storage.Has(a) && f.Storage.Has(b) && f.Commands[0].Var.Consumption.Key.Equals(a), "Switch consumes exact selected applier key at Commit");
                Check(f.Commands[0].Var.Amount == 3 && f.Commands[0].Var.Consumption.Consumed, "Switch preserves amount snapshot and paid receipt");
                Check(f.Scheduler.GetAllEntries().Count == 2, "full periodic Variable starts recharge at Commit alongside Delay");
                sw.cases[0].followUp.mechanism = other; f.Step(3); f.Step();
                Check(probe.Count == 1 && other.Count == 0 && probe.Last.Var.Amount == 3, "prepared Switch branch and amount remain stable through Delay");
                f.Stacks.Tick(5); Check(f.Storage.GetVariable(def).Amount == 1 && f.Storage.Has(b), "recharge uses CommitTick and other key remains intact");
            }
            using (var f = new Fixture())
            {
                var def = f.Variable(); var key = new StackKey(def); f.Stacks.ApplyStack(key, 3, 0);
                var probe = MakeProbe(); var child = Switch(def, probe); var parent = Switch(def, child, false);
                f.Bind(parent); f.Cast(SkillSlot.Attack);
                Check(f.Storage.Has(key) && !f.Commands[0].Var.Consumption.Consumed, "Consume=false preserves Variable at root Commit");
                f.Step(); Check(!f.Storage.Has(key) && f.Commands[0].Var.Consumption.Consumed, "first consuming child pays when accepted into queue");
                f.Stacks.ApplyStack(key, 2, f.Clock.TickCount); f.Step(2);
                Check(probe.Count == 1 && f.Storage.GetVariable(def).Amount == 2, "paid descendant does not consume newly gained Variable again");
            }
            using (var f = new Fixture())
            {
                var def = f.Variable(); var key = new StackKey(def); f.Stacks.ApplyStack(key, 3, 0);
                var first = f.Stacks.CaptureVariable(def); var second = f.Stacks.CaptureVariable(def);
                var sw = Switch(def, MakeProbe());
                SkillCommand Command(SwitchVariable v) => new(f.Caster.transform, TargetMode.TowardsSelf, default, sw, default, va: v);
                f.Collector.EnqueueCommand(Command(first)); f.Collector.EnqueueCommand(Command(second));
                Check(f.Commands.Count == 1 && !f.Storage.Has(key), "independent stale consumption receipts cannot spend same Variable twice");
                f.Collector.CeaseCommand(); Check(!f.Storage.Has(key), "accepted child cancellation does not refund Variable");
            }
            using (var f = new Fixture())
            {
                var def = f.Variable(); var key = new StackKey(def); f.Stacks.ApplyStack(key, 3, 0);
                var sw = Switch(def, MakeProbe()); f.Bind(sw, 101); f.Cast(SkillSlot.Attack);
                Check(f.Storage.GetVariable(def).Amount == 3 && f.Scheduler.GetAllEntries().Count == 0, "Mana failure leaves Variable and schedules unchanged");
                sw.Consume = false; f.Bind(sw, 0); f.Cast(SkillSlot.Attack); f.Step(2);
                Check(f.Storage.GetVariable(def).Amount == 3, "Consume=false no longer triggers Collector post-execution removal");
            }
            using (var f = new Fixture())
            {
                var def = f.Variable(); var key = new StackKey(def); f.Stacks.ApplyStack(key, 3, 0);
                var probe = MakeProbe(); var child = Switch(def, probe); var root = Switch(def, child, false);
                f.Bind(root, 10); f.Cast(SkillSlot.Attack);
                f.Stacks.ConsumeVariable(key, 3, 0); f.Step(3);
                Check(probe.Count == 0 && f.Commands.Count == 1 && f.Stats.Stats.Mana == 90, "unpaid continuation rechecks availability and root Mana remains spent");
            }
            using (var f = new Fixture())
            {
                var probe = MakeProbe(); var sw = new SwitchMechanism { cases = Array.Empty<SwitchCase>(),
                    defaultFollowUp = new MechanismData { mechanism = probe, mode = TargetMode.TowardsSelf } };
                f.Bind(sw, 10); f.Cast(SkillSlot.Attack); f.Step(2);
                Check(probe.Count == 1 && f.Stats.Stats.Mana == 90, "Switch default branch requires no Variable and child has no root cost");
                var def = f.Variable(); var before = f.Commands.Count;
                sw.Execute(new CastContext(sw, f.Caster.transform, SkillTarget.ForEntity(f.Caster), default, new SwitchVariable(def, 1)));
                Check(f.Commands.Count == before, "direct unpaid Switch with supplied Variable is explicitly rejected");
            }
        }
        private static void FailureAndObservers()
        {
            using (var f = new Fixture())
            {
                var probe = MakeProbe(2, 5); f.Bind(probe, 20); f.Cast(SkillSlot.Attack);
                f.Caster.targetable = false; f.Step(2);
                Check(probe.Count == 0 && f.Stats.Stats.Mana == 80 && !f.Attacker.IsPending(SkillSlot.Attack), "prepared target loss releases Pending without refunding Mana");
                f.Caster.targetable = true; f.Cast(SkillSlot.Attack);
                Check(f.Commands.Count == 1 && f.Stats.Stats.Mana == 80, "execution rejection does not refund cooldown");
            }
            using (var f = new Fixture())
            {
                var def = f.Variable(true); var key = new StackKey(def); f.Stacks.ApplyStack(key, 3, 0);
                var sw = Switch(def, MakeProbe()); f.Bind(sw, 10);
                typeof(Ticker).GetProperty("TickCount").SetValue(f.Clock, long.MaxValue - 1);
                f.Cast(SkillSlot.Attack);
                Check(f.Commands.Count == 0 && f.Stats.Stats.Mana == 100 && f.Storage.GetVariable(def).Amount == 3 &&
                    f.Scheduler.GetAllEntries().Count == 0 && !f.Attacker.IsPending(SkillSlot.Attack), "recharge overflow rolls back provisional root reservation without spending");
            }
            using (var f = new Fixture())
            {
                var def = f.Variable(true); var key = new StackKey(def); f.Stacks.ApplyStack(key, 2, 0);
                var before = f.Scheduler.GetAllEntries(); var receipt = f.Stacks.CaptureVariable(def).Consumption;
                object change = null;
                var root = (CastCommit)Activator.CreateInstance(typeof(CastCommit), BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { 0L, (byte)2, (Action)(() => { }) }, null);
                var cmd = new SkillCommand(f.Caster.transform, TargetMode.TowardsSelf, default, MakeProbe(2), default);
                cmd = (SkillCommand)typeof(SkillCommand).GetMethod("WithCommit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cmd, new object[] { root });
                Action apply = () => { change = typeof(VariableConsumption).GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(receipt, new object[] { 0L });
                    f.Stats.TrySpendMana(25); throw new InvalidOperationException("injected before publication"); };
                Action rollback = () => { if (change != null) Call(change, "Rollback");
                    typeof(StatsContainer).GetMethod("RestoreManaBeforeCommit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Stats.Stats, new object[] { 100 }); };
                var args = new object[] { cmd, apply, rollback, null };
                var accepted = (bool)typeof(CommandCollector).GetMethod("TryAcceptRoot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Collector, args);
                var after = f.Scheduler.GetAllEntries();
                Check(!accepted && f.Commands.Count == 0 && f.Stats.Stats.Mana == 100 && f.Storage.GetVariable(def).Amount == 2 && !receipt.Consumed,
                    "injected failure after resource mutation restores Mana and Variable before publication");
                Check(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var value) && value.Equals(pair.Value)),
                    "rollback preserves exact existing recharge handles and deadlines");
            }
            using (var f = new Fixture())
            {
                var probe = MakeProbe(); f.Bind(probe, 10); var observed = false;
                f.Collector.CommandEnqueued += cmd => { Check(f.Stats.Stats.Mana == 90 && cmd.Commit.State == CastCommitState.Committed && f.Attacker.IsPending(SkillSlot.Attack),
                    "observer sees fully committed resources and Pending"); throw new Exception("ExpectedCommitObserverFailure"); };
                f.Collector.CommandEnqueued += cmd => observed = true;
                _expectedObserverError = true; try { f.Cast(SkillSlot.Attack); } finally { _expectedObserverError = false; }
                f.Step(); Check(observed && probe.Count == 1 && f.Stats.Stats.Mana == 90, "throwing observer cannot undo or duplicate accepted Cast");
            }
            using (var f = new Fixture())
            {
                f.Bind(MakeProbe(), 10); f.Collector.enabled = false;
                if (!Application.isPlaying) Call(f.Collector, "OnDisable");
                f.Cast(SkillSlot.Attack);
                Check(f.Commands.Count == 0 && f.Stats.Stats.Mana == 100 && f.Scheduler.GetAllEntries().Count == 0, "unavailable collector never charges");
            }
        }
        private static void Serialization()
        {
            Check(JsonUtility.FromJson<SwitchMechanism>("{}").Consume, "missing serialized preserve flag means legacy Consume=true");
            var skill = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                skill.mechanismData = new MechanismData { mechanism = new SwitchMechanism { Consume = false,
                    defaultFollowUp = new MechanismData { mechanism = new SwitchMechanism { Consume = false } } } };
                var json = EditorJsonUtility.ToJson(skill);
                var restored = ScriptableObject.CreateInstance<SkillData>();
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(json, restored);
                    var outer = (SwitchMechanism)restored.mechanismData.mechanism;
                    Check(!outer.Consume && !((SwitchMechanism)outer.defaultFollowUp.mechanism).Consume && restored.ManaCost == 0, "nested SerializeReference preserves explicit Consume=false and default free Mana");
                }
                finally { Object.DestroyImmediate(restored); }
                if (!Application.isPlaying)
                {
                    var path = "Assets/ExitLab/Generated/CastCommitSerialization.asset";
                    if (AssetDatabase.LoadAssetAtPath<SkillData>(path)) AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(skill, path); AssetDatabase.SaveAssets();
                    Resources.UnloadAsset(skill);
                    // 옛 YAML과 동일하게 필드를 제거한 파일을 다시 가져옵니다. 원본 프로젝트에는 실행하지 않습니다.
                    var yaml = File.ReadAllText(path);
                    yaml = Regex.Replace(yaml, @"(?m)^\s*preserveVariable:.*\r?\n", "");
                    File.WriteAllText(path, yaml); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                    var outer = (SwitchMechanism)skill.mechanismData.mechanism;
                    Check(outer.Consume && ((SwitchMechanism)outer.defaultFollowUp.mechanism).Consume, "legacy nested Switch asset reload defaults to consuming");
                    outer.Consume = false; EditorUtility.SetDirty(skill); AssetDatabase.SaveAssets(); Resources.UnloadAsset(skill);
                    skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                    Check(!((SwitchMechanism)skill.mechanismData.mechanism).Consume, "explicit preserve option survives actual asset save and reload");
                    AssetDatabase.DeleteAsset(path); skill = null;
                }
            }
            finally { if (skill && !EditorUtility.IsPersistent(skill)) Object.DestroyImmediate(skill); }
        }
        private static void Call(object instance, string method) => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); Results.Add("PASS " + message); }
        private static void Observe(string message, string stack, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            if (_expectedObserverError && message.Contains("ExpectedCommitObserverFailure")) return;
            Errors.Add(message);
        }
    }
}
