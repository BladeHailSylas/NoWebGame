using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Characters;
using Moves;
using Moves.Mechanisms;
using Moves.ObjectEntity;
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
    /// <summary>
    /// S4 파괴·비활성화·재진입 종료 검사입니다.
    /// 원본 에셋을 저장하지 않으며 marker가 있는 별도 batch 사본에서만 실행합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class StackLifecycleChecks
    {
        private static readonly List<string> Results = new();
        private static readonly List<string> Errors = new();
        static StackLifecycleChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("StackS4.Run", false)) return;
                SessionState.SetBool("StackS4.Run", false);
                try { Run(); StackTriggerChecks.Run(); StackBuffChecks.Run(); StackTransitionChecks.Run(); StackBaselineChecks.Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunPlayMode()
        {
            Guard(); SessionState.SetBool("StackS4.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void Guard()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Isolated validation copy only");
        }
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Results.Add("PASS " + name);
        }
        private static object Get(object obj, string field) => obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        private static void Call(object obj, string method) => obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, null);
        private static void Observe(string message, string trace, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) Errors.Add(message); }

        private sealed class Fixture : IDisposable
        {
            public readonly List<Object> Owned = new();
            public readonly Ticker Clock = new();
            public readonly DelayScheduler Scheduler = new();
            public readonly CommandCollector Collector;
            public readonly TargetResolver Resolver;
            public int StackCommands;
            public Fixture()
            {
                typeof(GameTime).GetProperty("Ticker").SetValue(null, Clock);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, Scheduler);
                var services = Go("S0 services"); services.SetActive(false);
                Resolver = services.AddComponent<TargetResolver>();
                Collector = services.AddComponent<CommandCollector>(); services.SetActive(true);
                Collector.CommandEnqueued += cmd => { if (cmd.Mech is StackMechanism) StackCommands++; };
            }
            public GameObject Go(string name) { var go = new GameObject(name); Owned.Add(go); return go; }
            public T Def<T>() where T : ScriptableObject { var def = ScriptableObject.CreateInstance<T>(); Owned.Add(def); return def; }
            public EnemyEntity Enemy(string name, Vector2 position)
            {
                var spec = Def<CharacterSpec>(); spec.baseHp = 100; spec.baseMana = 100; spec.baseAttack = 100;
                spec.CharacterVariables = new();
                var go = Go(name); go.SetActive(false); go.transform.position = position;
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                go.AddComponent<BoxCollider2D>(); var enemy = go.AddComponent<EnemyEntity>(); enemy.InstallSpec(spec);
                // Default Layer의 Unknown 관계로 명중이 거부되지 않도록 fixture 관계를 명시합니다.
                enemy.RelationProfile = EntityRelationProfile.ExplicitNeutral;
                Set(enemy, "targetResolver", Resolver); Set(enemy, "commandCollector", Collector);
                go.SetActive(true);
                // 동기 PlayMode 검사: 실제 Start 초기화만 명시 호출합니다.
                Call(enemy, "Start");
                return enemy;
            }
            public StackManager Manager(int attack, out StatsContainer stats)
            {
                var entity = Go("S0 stats owner").AddComponent<TemporaryAITarget>();
                var ctx = new Context(entity, entity.gameObject, entity.transform, Resolver, Collector, null, null);
                ctx.RegisterScheduler(Scheduler);
                var bridge = new StatsBridge(ctx, new BaseStatsContainer(100, 0, 100, 0, attack, 100, 100));
                ctx.RegisterStats(bridge); ctx.RegisterAct(new ActBridge(null, null)); stats = bridge.Stats;
                return new StackManager(ctx, new VariableStorage());
            }
            public void Step(int count) { for (var i = 0; i < count; i++) Clock.Step(); }
            public void Dispose()
            {
                Collector.CeaseCommand();
                for (var i = Owned.Count - 1; i >= 0; i--) if (Owned[i]) Object.DestroyImmediate(Owned[i]);
            }
        }
        public static void Run()
        {
            Guard(); if (!Application.isPlaying) throw new Exception("PlayMode required");
            Results.Clear(); Errors.Clear();
            var ticker = GameTime.Ticker; var scheduler = GameTime.DelayScheduler;
            var ti = Ticker.Instance; var si = DelayScheduler.Instance; var collector = CommandCollector.Instance;
            var physics = Physics2D.simulationMode;
            Application.logMessageReceived += Observe;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Lifetime(); DisablePolicy(); ReentrantDisposal(); OppositeContributions();
                Check(Errors.Count == 0, "no unexpected Unity errors: " + string.Join(";", Errors));
                Debug.Log("StackLifecycleChecks PASS " + Results.Count);
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                Physics2D.simulationMode = physics;
                Application.logMessageReceived -= Observe;
                typeof(GameTime).GetProperty("Ticker").SetValue(null, ticker);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, scheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, ti); DelayScheduler.Instance = si;
                typeof(CommandCollector).GetProperty("Instance").SetValue(null, collector);
                File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../../stack-s4-results.txt")), Results);
            }
        }
        private static Dictionary<StackKey, StackStatus> States(StackManager manager) => (Dictionary<StackKey, StackStatus>)Get(manager, "_stackStorage");
        private static int Count(object obj, string field) => (int)Get(obj, field).GetType().GetProperty("Count").GetValue(Get(obj, field));
        private sealed class Probe : DummyMechanism
        {
            public Action Action;
            public int Calls;
            protected override void ExecuteCore(CastContext ctx) { Calls++; Action?.Invoke(); }
        }
        private static void Lifetime()
        {
            foreach (var player in new[] { false, true })
            {
                using var f = new Fixture();
                GameObject owner;
                object entity;
                if (!player) { var e = f.Enemy("S4 enemy", Vector2.zero); owner = e.gameObject; entity = e; }
                else
                {
                    var spec = f.Def<CharacterSpec>(); spec.baseHp = 100; spec.baseMana = 100; spec.baseAttack = 100; spec.CharacterVariables = new();
                    owner = f.Go("S4 player"); owner.SetActive(false); owner.AddComponent<Rigidbody2D>(); owner.AddComponent<BoxCollider2D>();
                    var e = owner.AddComponent<PlayerEntity>(); entity = e;
                    Set(e, "spec", spec); Set(e, "targetResolver", f.Resolver); Set(e, "commandCollector", f.Collector); owner.SetActive(true);
                }
                var manager = (StackManager)Get(entity, "_stackManager"); var stats = ((StatsBridge)Get(entity, "_statsBridge")).Stats;
                var act = (ActBridge)Get(entity, "_actBridge");
                var buff = f.Def<BuffDefinition>(); buff.Type = EffectType.DamageBoost; buff.Value = 10; buff.duration = 20;
                var ap = f.Def<BuffDefinition>(); ap.Type = EffectType.APBoost; ap.Value = 10; ap.duration = 20;
                var cc = f.Def<CCDefinition>(); cc.Type = EffectType.Stunned; cc.duration = 20;
                var variable = f.Def<VariableDefinition>(); variable.isPeriodic = true; variable.maxStacks = 3; variable.periodTick = 10;
                var trigger = f.Def<TriggerableDefinition>(); trigger.threshold = 3; trigger.duration = 20; var probe = new Probe(); trigger.effect = new MechanismData { mechanism = probe };
                var vk = new StackKey(variable); var tk = new StackKey(trigger);
                manager.ApplyStack(new StackKey(buff), 2, 0); manager.ApplyStack(new StackKey(ap), 2, 0);
                manager.ApplyStack(new StackKey(cc), 2, 0); manager.ApplyStack(vk, 3, 0); manager.ApplyStack(tk, 1, 0, metadata: new StackMetadata(9));
                stats.TryApply(new BuffData(EffectType.DamageBoost, 10)); stats.TryApply(new BuffData(EffectType.APBoost, 10));
                act.ApplyCC(new CCData(EffectType.Rooted, 0));
                var unrelated = f.Scheduler.Start(0, 100);
                var receipt = manager.CaptureVariable(variable);
                var change = typeof(VariableConsumption).GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(receipt.Consumption, new object[] { 0L });
                manager.EnqueueStack(tk, 3, new StackMetadata(100));
                Check(stats.AttackDamage == 130 && !act.CanAttack && Count(manager, "_consumptionChanges") == 1, player + " active owned effects and open consumption before destruction");
                Object.DestroyImmediate(owner); manager.Dispose();
                Call(change, "Rollback"); Call(change, "Complete");
                Check(States(manager).Count == 0 && Count(manager, "_metadata") == 0 && Count(manager, "_collecting") == 0 && Count(manager, "_resolving") == 0, player + " OnDestroy clears state payload and both queues idempotently");
                Check(stats.AttackDamage == 110 && Math.Abs(stats.TotalArmorPenetration() - 0.1) < 1e-12 && Count(stats, "_buffContributions") == 0, player + " disposal removes owned buffs only and leaves legacy effects");
                Check(act.CanAttack && !act.CanMove && Count(manager, "_ccContributions") == 0, player + " disposal releases owned CC only without duplicate notification");
                Check(!manager.Storage.Has(variable) && Count(manager, "_consumptionChanges") == 0 && f.Scheduler.GetAllEntries().Count == 1 && f.Scheduler.GetRemaining(unrelated, 0) == 100, player + " disposal clears variable reservations including open receipt but preserves unrelated timer");
                Check(!receipt.Consumption.Consumed, player + " disposal is not a successful Switch payment");
                manager.ApplyStack(tk, 3, 1); manager.EnqueueStack(tk, 3); manager.EnqueueStack(tk, 3, new StackMetadata(1)); manager.EnqueueStack(new StackDelta(tk, 3)); manager.Tick(100);
                manager.ConsumeVariable(vk, 1, 100); manager.TryConsume(receipt);
                Check(States(manager).Count == 0 && Count(manager, "_collecting") == 0 && probe.Calls == 0, player + " stale references cannot resurrect disposed stacks");
                f.Scheduler.Remove(unrelated);
            }
        }
        private static void DisablePolicy()
        {
            using var f = new Fixture(); var entity = f.Enemy("S4 disabled", Vector2.zero); var manager = (StackManager)Get(entity, "_stackManager");
            var stats = ((StatsBridge)Get(entity, "_statsBridge")).Stats;
            var buff = f.Def<BuffDefinition>(); buff.Type = EffectType.DamageBoost; buff.Value = 10; buff.duration = 3;
            var key = new StackKey(buff); manager.ApplyStack(key, 1, 0); entity.gameObject.SetActive(false); f.Step(5);
            Check(stats.AttackDamage == 110 && States(manager).ContainsKey(key), "disable stops Tick without disposing active effect");
            entity.ApplyStack(key, f.Clock.TickCount); entity.gameObject.SetActive(true); f.Step(1);
            Check(stats.AttackDamage == 120 && States(manager)[key].Amount == 2, "reenable processes queued refresh before expiry with existing policy");
            f.Step(3); Check(stats.AttackDamage == 100 && !States(manager).ContainsKey(key), "refreshed Buff eventually expires without residue");
            manager.ApplyStack(key, 1, f.Clock.TickCount); entity.gameObject.SetActive(false); f.Step(5); entity.gameObject.SetActive(true); f.Step(1);
            Check(stats.AttackDamage == 100 && !States(manager).ContainsKey(key), "reenable without refresh expires elapsed lifetime");
        }
        private static void ReentrantDisposal()
        {
            using var f = new Fixture(); var manager = f.Manager(100, out var stats); var probe = new Probe();
            var trigger = f.Def<TriggerableDefinition>(); trigger.threshold = 1; trigger.effect = new MechanismData { mechanism = probe };
            var key = new StackKey(trigger);
            var buff = f.Def<BuffDefinition>(); buff.Type = EffectType.DamageBoost; buff.Value = 10;
            manager.ApplyStack(new StackKey(buff), 2, 0);
            probe.Action = () => { manager.EnqueueStack(key, 1); manager.Dispose(); manager.ApplyStack(key, 1, 0); };
            manager.EnqueueStack(key, 1); manager.EnqueueStack(key, 1); manager.Tick(0); manager.Tick(1);
            Check(probe.Calls == 1 && stats.AttackDamage == 100 && States(manager).Count == 0 && Count(manager, "_collecting") == 0 && Count(manager, "_resolving") == 0, "disposal during Execute clears current remainder and reentry without replay");
            Check(f.Scheduler.GetAllEntries().Count == 0, "reentrant disposal removes every owned timer");
        }
        private static void OppositeContributions()
        {
            using var f = new Fixture(); var manager = f.Manager(100, out var stats);
            var slow = f.Def<BuffDefinition>(); slow.Type = EffectType.Slow; slow.Value = 1; slow.maxStacks = int.MaxValue;
            var haste = f.Def<BuffDefinition>(); haste.Type = EffectType.Haste; haste.Value = 1; haste.maxStacks = int.MaxValue;
            manager.ApplyStack(new StackKey(slow), int.MaxValue, 0);
            manager.ApplyStack(new StackKey(haste), int.MaxValue, 0);
            Check(stats.Speed == 100, "large opposite owned contributions produce representable final speed");
            manager.Dispose();
            Check(stats.Speed == 100 && Count(stats, "_buffContributions") == 0 && f.Scheduler.GetAllEntries().Count == 0, "batch disposal avoids removal-order intermediate overflow");
        }
    }
}

