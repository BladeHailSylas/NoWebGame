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
    /// S0 수정 전 기준 검사. KNOWN_DEFECT는 성공 동작이 아니라 확정 계약의 위반입니다.
    /// 원본 에셋을 저장하지 않으며 marker가 있는 별도 batch 사본에서만 실행합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class StackBaselineChecks
    {
        private static readonly List<string> Results = new();
        private static readonly List<string> Errors = new();
        private static int _defects;
        static StackBaselineChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("StackS0.Run", false)) return;
                SessionState.SetBool("StackS0.Run", false);
                try { Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunPlayMode()
        {
            Guard(); SessionState.SetBool("StackS0.Run", true);
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
        private static void Defect(string id, string actual, string expected, bool fingerprint)
        {
            Check(fingerprint, "baseline fingerprint " + id);
            _defects++;
            Results.Add($"KNOWN_DEFECT {id}: actual={actual}; required={expected}");
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
            Results.Clear(); Errors.Clear(); _defects = 0;
            var ticker = GameTime.Ticker; var scheduler = GameTime.DelayScheduler;
            var ti = Ticker.Instance; var si = DelayScheduler.Instance; var collector = CommandCollector.Instance;
            var physics = Physics2D.simulationMode;
            Application.logMessageReceived += Observe;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Buffs(); ProjectileCC(); Characteristics();
                Check(Errors.Count == 0, "no unexpected Unity errors: " + string.Join(";", Errors));
                Results.Add($"SUMMARY observedKnownDefects={_defects}; baseline reproduction only, NOT stabilization passed");
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
                File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../../stack-s0-results.txt")), Results);
            }
        }
        private static void Buffs()
        {
            using var f = new Fixture();
            var buff = f.Def<BuffDefinition>(); buff.Type = EffectType.DamageBoost; buff.Value = 10; buff.duration = 3; buff.maxStacks = 1;
            var key = new StackKey(buff); var manager = f.Manager(100, out var stats);
            manager.ApplyStack(key, 1, 0); manager.ApplyStack(key, 1, 1); manager.Tick(4);
            Check(stats.AttackDamage == 100, "R03-cap fixed: capped Buff fully expires");
            buff.maxStacks = 10;
            var split = f.Manager(5, out var ss); var batch = f.Manager(5, out var bs);
            split.ApplyStack(key, 1, 0); split.ApplyStack(key, 1, 0); batch.ApplyStack(key, 2, 0);
            Check(ss.AttackDamage == 6 && bs.AttackDamage == 6, "R03-batching fixed: split and batch both equal 6");
            split.Tick(3); batch.Tick(3);
            Check(ss.AttackDamage == 5 && bs.AttackDamage == 5, "R03-rounding-release fixed: both return to 5");
            foreach (var type in new[] { EffectType.APBoost, EffectType.DRBoost })
            {
                buff.Type = type; var listManager = f.Manager(100, out var ls);
                listManager.ApplyStack(key, 1, 0); listManager.ApplyStack(key, 1, 0);
                var list = type == EffectType.APBoost ? ls.ArmorPenetration : ls.DamageReduction;
                double Factor() => (double)typeof(StatsContainer).GetMethod("OwnedPercentageFactor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ls, new object[] { type });
                Check(list.Count == 0 && Math.Abs(Factor() - 0.8) < 1e-12, type + " owned aggregate 20 percent; no duplicate legacy entries");
                if (type == EffectType.DRBoost)
                    Defect("DR-separate-truncation", ls.TotalDamageReduction().ToString(), "0.8 before unrelated rounding policy", ls.TotalDamageReduction() == 0);
                listManager.Tick(3);
                Check(list.Count == 0 && Factor() == 1, "R03-" + type + "-release fixed: no remaining contribution");
            }
        }
        private static Dictionary<StackKey, StackStatus> States(StackManager manager) => (Dictionary<StackKey, StackStatus>)Get(manager, "_stackStorage");
        private static void ProjectileCC()
        {
            using var f = new Fixture();
            var caster = f.Enemy("S0 enemy caster", Vector2.zero);
            var target = f.Enemy("S0 target", new Vector2(2, 0));
            var stacks = (StackManager)Get(target, "_stackManager"); var act = (ActBridge)Get(target, "_actBridge");
            var cc = f.Def<CCDefinition>(); cc.Type = EffectType.Suppressed; cc.duration = 120; cc.maxStacks = 1000000;
            var stack = new StackMechanism { stack = cc, amount = 1 };
            var all = TargetRelationMask.Self | TargetRelationMask.Ally | TargetRelationMask.Enemy | TargetRelationMask.Neutral | TargetRelationMask.Unrelated;
            stack.ConfigureRelativeTargets(all);
            var key = new StackKey(cc, caster.name, caster.transform);
            for (var hit = 1; hit <= 2; hit++)
            {
                var go = f.Go("S0 projectile " + hit); go.transform.position = Vector2.zero;
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                go.AddComponent<CircleCollider2D>().radius = 0.1f;
                var projectile = go.AddComponent<ProjectileEntity>();
                var mech = new ProjectileMechanism { speed = 60, lifeTick = 30, callbackMode = ExitCallbackMode.Classified,
                    onHit = new List<MechanismData> { new() { mechanism = stack, mode = TargetMode.TowardsEntity } } };
                mech.ConfigureRelativeTargets(all & ~TargetRelationMask.Self);
                projectile.Init(new CastContext(mech, caster.transform, SkillTarget.ForEntity(target), default));
                for (var step = 0; step < 12 && projectile.UniqueHitCount == 0; step++)
                {
                    Physics2D.SyncTransforms(); f.Step(1); Physics2D.Simulate(1f / 60); projectile.AfterPhysics();
                }
                Check(projectile.UniqueHitCount == 1, "actual Projectile collider hit " + hit);
                f.Step(3);
                Check(f.StackCommands == hit && States(stacks)[key].Amount == hit, "Chain Collector -> StackMechanism -> Enemy.Enqueue -> StackManager " + hit);
                Results.Add($"OBSERVE hit={hit};tick={f.Clock.TickCount};amount={States(stacks)[key].Amount};counter={Get(act, "_innoxiousCount")}");
                // Destroy is deferred in PlayMode; remove the finished fixture before firing again.
                if (go) Object.DestroyImmediate(go);
            }
            Check(!act.CanAttack && act.CanMove, "Suppressed blocks attack only before expiry");
            f.Step(121);
            Check(!States(stacks).ContainsKey(key), "CC stack actually expired");
            var remaining = (int)Get(act, "_innoxiousCount");
            // S1부터 정상 계약으로 전환합니다. 수정 전 결과는 S0 산출물에 보존했습니다.
            Check(remaining == 0 && act.CanAttack, "STACK-CC-01 fixed: actual Projectile Chain expires with counter=0 and CanAttack=True");
        }
        private static void Characteristics()
        {
            using var f = new Fixture(); var manager = f.Manager(100, out _);
            var variable = f.Def<VariableDefinition>(); variable.duration = ushort.MaxValue;
            var buff = f.Def<BuffDefinition>(); buff.Type = EffectType.DamageBoost; buff.Value = 10; buff.duration = ushort.MaxValue;
            var vk = new StackKey(variable); var bk = new StackKey(buff);
            manager.ApplyStack(vk, 1, 0); manager.ApplyStack(bk, 1, 0); manager.Tick(65535);
            Check(States(manager).ContainsKey(vk) && !States(manager).ContainsKey(bk), "preserve existing 65535 sentinel difference");
            var a = f.Go("same"); var b = f.Go("same");
            Check(new StackKey(buff, a.name, a.transform).Equals(new StackKey(buff, b.name, b.transform)), "preserve name-based key equality");
            var enemy = f.Enemy("disabled target", new Vector2(20, 0));
            var sm = (StackManager)Get(enemy, "_stackManager");
            enemy.gameObject.SetActive(false); enemy.ApplyStack(bk, f.Clock.TickCount); f.Step(5);
            Check(!States(sm).ContainsKey(bk), "disabled owner queues without ticking");
            enemy.gameObject.SetActive(true); f.Step(1);
            Check(States(sm).ContainsKey(bk), "reenabled owner applies queued request");
            var trigger = f.Def<TriggerableDefinition>(); trigger.threshold = 3; trigger.duration = 2;
            var tk = new StackKey(trigger); manager.ApplyStack(tk, 1, 0, metadata: new StackMetadata(7)); manager.Tick(2);
            var payloads = (Dictionary<StackKey, StackMetadata>)Get(manager, "_metadata");
            Defect("Trigger-expired-payload", payloads[tk].Metadata.ToString(), "key absent", !States(manager).ContainsKey(tk) && payloads[tk].Metadata == 7);
        }
    }
}
