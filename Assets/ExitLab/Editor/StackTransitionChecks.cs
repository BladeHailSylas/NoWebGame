using global::NoWebGame.Characters;
using global::NoWebGame.Combat.Definitions;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Actions;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Runtime;
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
using GameTime = global::NoWebGame.Systems.Time.Time;

namespace ExitLab
{
    /// <summary>S1 수량/CC/예약/재진입 계약. 예상 예외와 실제 결함을 별도 기록합니다.</summary>
    [InitializeOnLoad]
    public static class StackTransitionChecks
    {
        private static readonly List<string> Results = new();
        private static readonly List<string> Errors = new();
        private static string _expectedError;
        private static int _expectedLogs;
        static StackTransitionChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("StackS1.Run", false)) return;
                SessionState.SetBool("StackS1.Run", false);
                try { Run(); StackBaselineChecks.Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunPlayMode()
        {
            Guard(); SessionState.SetBool("StackS1.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void Guard()
        { if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker")) throw new Exception("Validation copy only"); }
        private static object Get(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Check(bool condition, string name)
        { if (!condition) throw new Exception(name); Results.Add("PASS " + name); }
        private static void Throws<T>(Action action, string name) where T : Exception
        { try { action(); } catch (T) { Check(true, name); return; } throw new Exception("Missing exception: " + name); }
        private static void Observe(string message, string trace, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            if (_expectedError != null && message.Contains(_expectedError)) { _expectedLogs++; return; }
            Errors.Add(message);
        }
        private static void ExpectLog(string text, Action action)
        {
            _expectedError = text; _expectedLogs = 0;
            try { action(); Check(_expectedLogs == 1, "isolated exactly one expected failure: " + text); }
            finally { _expectedError = null; }
        }
        private sealed class Probe : DummyMechanism
        {
            public Action Action;
            public int Calls;
            protected override void ExecuteCore(CastContext ctx) { Calls++; Action?.Invoke(); }
        }
        private sealed class Fixture : IDisposable
        {
            public readonly List<Object> Owned = new();
            public readonly Ticker Clock = new();
            public readonly DelayScheduler Scheduler = new();
            public readonly CommandCollector Collector;
            public readonly TemporaryAITarget Entity;
            public readonly Context Context;
            public readonly StackManager Manager;
            public readonly StatsBridge Stats;
            public readonly ActBridge Act;
            public readonly Attacker Attacker;
            public readonly Dictionary<SkillSlot, SkillBinding> Bindings = new();
            public readonly List<SkillCommand> Commands = new();
            public Fixture()
            {
                typeof(GameTime).GetProperty("Ticker").SetValue(null, Clock);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, Scheduler);
                var services = Go("S1 services"); services.SetActive(false);
                var resolver = services.AddComponent<TargetResolver>();
                Collector = services.AddComponent<CommandCollector>(); services.SetActive(true);
                Collector.CommandEnqueued += Commands.Add;
                Entity = Go("S1 actor").AddComponent<TemporaryAITarget>();
                Context = new Context(Entity, Entity.gameObject, Entity.transform, resolver, Collector, null, new global::NoWebGame.Combat.Runtime.Logger("S1"));
                Context.RegisterScheduler(Scheduler);
                Stats = new StatsBridge(Context, new BaseStatsContainer(100, 0, 100, 0, 100, 0, 0)); Context.RegisterStats(Stats);
                Attacker = new Attacker(Context, Entity.transform, Bindings, Collector);
                Act = new ActBridge(null, Attacker); Context.RegisterAct(Act);
                var storage = new VariableStorage(); Context.RegisterVariableStorage(storage);
                Manager = new StackManager(Context, storage); Context.RegisterStackManager(Manager);
            }
            private GameObject Go(string name) { var go = new GameObject(name); Owned.Add(go); return go; }
            public T Def<T>() where T : ScriptableObject { var d = ScriptableObject.CreateInstance<T>(); Owned.Add(d); return d; }
            public CCDefinition CC(EffectType type, ushort duration = 3)
            { var d = Def<CCDefinition>(); d.Type = type; d.duration = duration; d.maxStacks = 1000; return d; }
            public Dictionary<StackKey, StackStatus> States => (Dictionary<StackKey, StackStatus>)Get(Manager, "_stackStorage");
            public int AttackCount => (int)Get(Act, "_innoxiousCount");
            public int MoveCount => (int)Get(Act, "_immovableCount");
            public object Contribution(StackKey key) => ((System.Collections.IDictionary)Get(Manager, "_ccContributions"))[key];
            public void Release(object contribution) => typeof(ActBridge).GetMethod("ReleaseCC", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Act, new[] { contribution });
            public void Cast(Probe p)
            {
                JsonUtility.FromJsonOverwrite("{\"delayTicks\":5,\"cooldownTicks\":12}", p);
                var skill = Def<SkillData>(); skill.ManaCost = 10; skill.mechanismData = new MechanismData { mechanism = p, mode = TargetMode.TowardsSelf };
                Bindings[SkillSlot.Attack] = new SkillBinding { skillData = skill };
                Attacker.EnqueueCast(SkillSlot.Attack); Attacker.Tick(Clock.TickCount, 0);
            }
            public void Dispose()
            {
                Attacker.Dispose(); Collector.CeaseCommand();
                for (var i = Owned.Count - 1; i >= 0; i--) if (Owned[i]) Object.DestroyImmediate(Owned[i]);
            }
        }
        public static void Run()
        {
            Guard(); Results.Clear(); Errors.Clear();
            var ticker = GameTime.Ticker; var scheduler = GameTime.DelayScheduler;
            var ti = Ticker.Instance; var si = DelayScheduler.Instance; var collector = CommandCollector.Instance;
            Application.logMessageReceived += Observe;
            try
            {
                Transitions(); Ownership(); Failures(); Reentrancy(); Pending(); TriggerDirectContract();
                Check(Errors.Count == 0, "no unexpected Unity errors: " + string.Join(";", Errors));
                Debug.Log("StackTransitionChecks PASS " + Results.Count);
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                Application.logMessageReceived -= Observe; _expectedError = null;
                typeof(GameTime).GetProperty("Ticker").SetValue(null, ticker);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, scheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, ti); DelayScheduler.Instance = si;
                typeof(CommandCollector).GetProperty("Instance").SetValue(null, collector);
                File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../../stack-s1-results.txt")), Results);
            }
        }
        private static void Transitions()
        {
            foreach (var type in new[] { EffectType.Rooted, EffectType.Suppressed, EffectType.Stunned, EffectType.Tumbled })
            {
                using var f = new Fixture(); var def = f.CC(type); def.maxStacks = 1; var key = new StackKey(def);
                f.Manager.ApplyStack(key, 0, 0);
                Check(!f.States.ContainsKey(key) && f.Act.CanMove && f.Act.CanAttack, type + " zero creates no CC");
                for (var i = 0; i < 256; i++) f.Manager.ApplyStack(key, 1, i);
                Check(f.States[key].Amount == 1 && f.Scheduler.GetAllEntries().Count == 1, type + " 256 refreshes keep one reservation and capped quantity");
                Check(f.MoveCount == (type == EffectType.Suppressed ? 0 : 1) && f.AttackCount == (type == EffectType.Rooted ? 0 : 1), type + " one owned restriction");
                f.Manager.Tick(257); Check(f.States.ContainsKey(key), type + " refreshed before expiry");
                f.Manager.Tick(258); f.Manager.Tick(259);
                Check(f.Act.CanMove && f.Act.CanAttack && f.Scheduler.GetAllEntries().Count == 0, type + " expires exactly once");
                def.maxStacks = int.MaxValue;
                f.Manager.ApplyStack(key, int.MaxValue, 300); f.Manager.ApplyStack(key, int.MaxValue, 301);
                Check(f.States[key].Amount == int.MaxValue, type + " long intermediate prevents int overflow");
                f.Manager.ApplyStack(key, -2, 302);
                Check(f.States[key].Amount == int.MaxValue - 2 && f.Contribution(key) != null, type + " partial decrease retains contribution");
                f.Manager.ApplyStack(key, int.MinValue, 303);
                Check(!f.States.ContainsKey(key) && f.Act.CanAttack && f.Act.CanMove, type + " negative to zero releases contribution");
                f.Manager.EnqueueStack(key, 1); f.Manager.Tick(400); f.Manager.EnqueueStack(key, 1); f.Manager.Tick(403);
                Check(f.States.ContainsKey(key), type + " same deadline applies before expiration");
                f.Manager.Tick(406); Check(f.Act.CanAttack && f.Act.CanMove, type + " refresh deadline expires");
            }
        }
        private static void Ownership()
        {
            using var f = new Fixture(); var rooted = f.CC(EffectType.Rooted); var stunned = f.CC(EffectType.Stunned, 5);
            var a = new StackKey(rooted, "A"); var b = new StackKey(rooted, "B"); var c = new StackKey(stunned);
            f.Manager.ApplyStack(a, 2, 0); f.Manager.ApplyStack(b, 1, 1); f.Manager.ApplyStack(c, 1, 0);
            var old = f.Contribution(a);
            typeof(ActBridge).GetMethod("ReleaseCC", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(new ActBridge(null, null), new[] { old });
            Check(f.MoveCount == 3 && f.AttackCount == 1, "different keys and CC types independent");
            f.Manager.Tick(3); Check(f.MoveCount == 2 && f.AttackCount == 1, "one expiry preserves other keys");
            f.Manager.ApplyStack(a, 1, 3); f.Release(old);
            Check(f.MoveCount == 3, "old released handle cannot remove new cycle");
            // 현재 정의가 아니라 실제 적용 당시의 제한을 해제합니다.
            rooted.Type = EffectType.Suppressed;
            f.Manager.Tick(4); Check(f.MoveCount == 2 && f.AttackCount == 1, "definition mutation does not change recorded release type");
            f.Manager.Tick(5); Check(f.MoveCount == 1 && f.AttackCount == 0, "mixed type expiration preserves remaining movement CC");
            f.Act.RemoveCC(new CCData(EffectType.Rooted, 0));
            Check(f.MoveCount == 1, "legacy removal cannot steal owned CC");
            f.Manager.Tick(6); Check(f.Act.CanMove && f.Act.CanAttack, "all keys ended");
        }
        private static void Failures()
        {
            using var f = new Fixture(); var def = f.CC(EffectType.Stunned); var key = new StackKey(def);
            f.Manager.ApplyStack(key, 1, 0); var previous = f.States[key]; var owner = f.Contribution(key);
            f.Scheduler.ModifyDelta(previous.DelayId, -2);
            Throws<OverflowException>(() => f.Manager.ApplyStack(key, 1, long.MaxValue), "refresh reservation failure observable");
            Check(f.States[key].Amount == 1 && f.States[key].DelayId.Equals(previous.DelayId) && ReferenceEquals(owner, f.Contribution(key)) && f.Scheduler.GetRemaining(previous.DelayId, 0) == 5, "failed refresh preserves amount, handle, old reservation and delta");
            f.Manager.Tick(5); Check(f.Act.CanAttack && f.Act.CanMove, "preserved old reservation still expires");
            Throws<OverflowException>(() => f.Manager.ApplyStack(key, 1, long.MaxValue), "initial reservation failure observable");
            Check(!f.States.ContainsKey(key) && f.Scheduler.GetAllEntries().Count == 0, "initial failure leaves no state");
            def.Type = EffectType.DamageBoost;
            Throws<ArgumentException>(() => f.Manager.ApplyStack(key, 1, 0), "effect acquisition failure observable");
            Check(!f.States.ContainsKey(key) && f.Scheduler.GetAllEntries().Count == 0 && f.Act.CanMove && f.Act.CanAttack, "acquisition failure rolls back new reservation and contribution");
            def.Type = EffectType.Stunned; def.maxStacks = -1;
            Throws<ArgumentOutOfRangeException>(() => f.Manager.ApplyStack(key, 1, 0), "invalid max rejected before mutation");
            def.maxStacks = 100;
            Throws<ArgumentOutOfRangeException>(() => f.Manager.ApplyStack(key, 1, -1), "invalid time rejected before mutation");
            f.Manager.EnqueueStack(default(StackKey), 1); f.Manager.EnqueueStack(key, 1);
            ExpectLog("Stack definition", () => f.Manager.Tick(0));
            Check(f.States[key].Amount == 1, "bad queue item does not stop next item");
            f.Manager.Tick(1); f.Manager.Tick(2);
            Check(f.States[key].Amount == 1, "processed items never replay after buffer swaps");
            f.Manager.Tick(3); Check(f.Act.CanAttack, "expiry continues after queue exception");
            var variable = f.Def<VariableDefinition>(); variable.isPeriodic = true; variable.periodTick = 3; variable.maxStacks = 5;
            var vk = new StackKey(variable); f.Manager.ApplyStack(vk, 2, 0); var vs = f.States[vk];
            Throws<OverflowException>(() => f.Manager.ApplyStack(vk, 1, long.MaxValue), "Variable reservation failure before visible mutation");
            Check(f.States[vk].Amount == 2 && f.States[vk].DelayId.Equals(vs.DelayId) && f.Manager.Storage.GetVariable(variable).Amount == 2, "Variable amount, visibility and recharge preserved");
            var buff = f.Def<BuffDefinition>(); buff.Type = EffectType.DamageBoost; buff.Value = 10; buff.duration = 3;
            var bk = new StackKey(buff); f.Manager.ApplyStack(bk, 1, 0); var bs = f.States[bk];
            Throws<OverflowException>(() => f.Manager.ApplyStack(bk, 1, long.MaxValue), "Buff reservation failure before effect mutation");
            Check(f.States[bk].Amount == 1 && f.States[bk].DelayId.Equals(bs.DelayId) && f.Stats.Stats.AttackDamage == 110, "Buff reservation failure preserves prior state and effect");
            f.Manager.ApplyStack(vk, 3, 0);
            Throws<OverflowException>(() => f.Manager.ConsumeVariable(vk, 1, long.MaxValue), "direct Variable consume reservation failure before consumption");
            Check(f.States[vk].Amount == 5 && f.Manager.Storage.GetVariable(variable).Amount == 5, "failed recharge start retains full Variable amount");
            f.Manager.ConsumeVariable(vk, 1, 0); f.Manager.ConsumeVariable(vk, 1, 0); vs = f.States[vk];
            ExpectLog("OverflowException", () => f.Manager.Tick(long.MaxValue));
            Check(f.States[vk].Amount == 3 && f.States[vk].DelayId.Equals(vs.DelayId) && f.Manager.Storage.GetVariable(variable).Amount == 3 && f.Scheduler.GetAllEntries().ContainsKey(vs.DelayId), "periodic renewal failure retains amount, visibility and existing handle");
        }
        private static void Reentrancy()
        {
            using var f = new Fixture(); var cc = f.CC(EffectType.Suppressed); var key = new StackKey(cc);
            // CancelPending의 외부 해제 콜백에 실패를 주입합니다. 실제 CastCommit 경계를 사용합니다.
            var pending = (Dictionary<SkillSlot, CastCommit>)Get(f.Attacker, "_pending");
            Action release = () =>
            {
                Check(f.States.ContainsKey(key) && f.Contribution(key) != null && f.AttackCount == 1, "notification observes fully committed CC");
                pending.Clear();
                f.Manager.ApplyStack(key, 1, 100, durationOverride: 7); f.Manager.Tick(100);
                Check(f.States[key].Amount == 1, "direct apply and Tick reentry are deferred");
                throw new InvalidOperationException("S1ExpectedNotification");
            };
            var commit = (CastCommit)Activator.CreateInstance(typeof(CastCommit), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 0L, (byte)5, release }, null);
            pending.Add(SkillSlot.Attack, commit);
            ExpectLog("S1ExpectedNotification", () => f.Manager.ApplyStack(key, 1, 0));
            Check(f.States[key].Amount == 1 && f.AttackCount == 1 && commit.State == CastCommitState.Cancelled, "notification exception does not roll back committed restriction or revive Cast");
            f.Manager.Tick(1);
            Check(f.States[key].Amount == 2 && f.AttackCount == 1 && f.Scheduler.GetRemaining(f.States[key].DelayId, 1) == 7, "deferred request retains duration override and applies once");
            f.Manager.Tick(8); Check(f.Act.CanAttack, "notification failure still allows eventual release");
            var probe = new Probe(); var trigger = f.Def<TriggerableDefinition>(); trigger.threshold = 1; trigger.duration = 3;
            trigger.effect = new MechanismData { mechanism = probe }; var tk = new StackKey(trigger, "A", f.Entity.transform);
            probe.Action = () => { f.Manager.ApplyStack(key, 1, 10); f.Manager.Tick(10); throw new Exception("S1ExpectedExecution"); };
            f.Manager.EnqueueStack(tk, 1); f.Manager.EnqueueStack(key, 1);
            ExpectLog("S1ExpectedExecution", () => f.Manager.Tick(10));
            Check(probe.Calls == 1 && f.States[key].Amount == 1, "throwing callback does not stop next queue item");
            f.Manager.Tick(11); f.Manager.Tick(12);
            Check(probe.Calls == 1 && f.States[key].Amount == 2 && f.AttackCount == 1, "execution reentry next buffer only; old trigger never replays");
            f.Manager.Tick(14); Check(f.Act.CanAttack, "expiry survives execution failure");
        }
        private static void TriggerDirectContract()
        {
            // S3: 동일 Mechanism의 루트 Cast가 대기 중이어도 Stack은 직접 실행합니다.
            using var f = new Fixture(); var probe = new Probe(); f.Cast(probe);
            Check(f.Attacker.IsPending(SkillSlot.Attack) && probe.Calls == 0, "S3 root Cast is pending before direct trigger");
            var d = f.Def<TriggerableDefinition>(); d.threshold = 1; d.duration = 3;
            d.effect = new MechanismData { mechanism = probe }; var key = new StackKey(d, "A", f.Entity.transform);
            f.Manager.ApplyStack(key, 1, 0);
            Check(probe.Calls == 1 && f.Attacker.IsPending(SkillSlot.Attack), "S3 direct trigger ignores root delay and retains existing Pending");
            Check(f.Stats.Stats.Mana == 90 && f.Commands.Count == 1, "S3 no extra root mana or collected command");
            for (var i = 0; i < 6; i++) f.Clock.Step();
            Check(probe.Calls == 2 && !f.Attacker.IsPending(SkillSlot.Attack), "S3 original Pending completes exactly once independently");
            f.Attacker.EnqueueCast(SkillSlot.Attack); f.Attacker.Tick(f.Clock.TickCount, 0);
            Check(f.Commands.Count == 1 && f.Stats.Stats.Mana == 90, "S3 original root cooldown remains unchanged");
            f.Manager.ApplyStack(new StackKey(f.CC(EffectType.Suppressed)), 1, f.Clock.TickCount);
            f.Manager.ApplyStack(key, 1, f.Clock.TickCount);
            Check(!f.Act.CanAttack && probe.Calls == 3, "S3 direct trigger does not acquire root CanAttack policy");
        }

        private static void Pending()
        {
            foreach (var type in new[] { EffectType.Rooted, EffectType.Suppressed, EffectType.Stunned, EffectType.Tumbled })
            {
                using var f = new Fixture(); var p = new Probe(); f.Cast(p);
                Check(f.Attacker.IsPending(SkillSlot.Attack) && f.Stats.Stats.Mana == 90, type + " root committed before CC");
                var key = new StackKey(f.CC(type)); f.Manager.ApplyStack(key, 1, 0);
                Check(f.Attacker.IsPending(SkillSlot.Attack) == (type == EffectType.Rooted), type + " Stack path pending policy");
                f.Manager.ApplyStack(key, 1, 0); f.Manager.Tick(3);
                for (var i = 0; i < 6; i++) f.Clock.Step();
                Check(p.Calls == (type == EffectType.Rooted ? 1 : 0) && f.Stats.Stats.Mana == 90, type + " no refund or cancelled execution revival");
                f.Attacker.EnqueueCast(SkillSlot.Attack); f.Attacker.Tick(f.Clock.TickCount, 0);
                Check(f.Commands.Count == 1, type + " cooldown remains after CC ends");
            }
        }
    }
}
