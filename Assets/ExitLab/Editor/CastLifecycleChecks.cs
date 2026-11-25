using global::NoWebGame.Characters;
using global::NoWebGame.Combat.Definitions;
using global::NoWebGame.Combat.Effects.Runtime;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Entities.Enemy;
using global::NoWebGame.Entities.Player;
using global::NoWebGame.Entities.Summon;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Actions;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Presentation;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Combat.Stats;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Movement;
using global::NoWebGame.Movement.Travel;
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
    /// <summary>Phase 4: 실제 큐/시간/비용을 사용해 취소와 실패의 격리 경계를 검사합니다.</summary>
    [InitializeOnLoad]
    public static class CastLifecycleChecks
    {
        private static readonly List<string> Results = new(), Errors = new();
        private static bool _expectFailure;
        private sealed class Probe : DummyMechanism
        {
            public int Count;
            public Action<CastContext> Action;
            protected override void ExecuteCore(CastContext ctx) { Count++; Action?.Invoke(ctx); }
        }
        private sealed class Broken : INewMechanism
        {
            public bool Input;
            public short CooldownTicks => Input ? throw new Exception("ExpectedLifecycleInput") : (short)0;
            public byte DelayTicks => throw new Exception("ExpectedLifecycleSchedule");
            public float MinRange => 0;
            public float MaxRange => 0;
            public LayerMask Mask => 0;
            public void Execute(CastContext ctx) { }
        }
        private sealed class BrokenDisplay : IEffectDisplayer
        {
            public int Count;
            public void Display(SkillCommand cmd) { Count++; throw new Exception("ExpectedLifecycleDisplay"); }
        }
        private sealed class Actor
        {
            public readonly TemporaryAITarget Owner;
            public readonly Dictionary<SkillSlot, SkillBinding> Bindings = new();
            public readonly Attacker Attack;
            public readonly StatsBridge Stats;
            public readonly ActBridge Act;
            private readonly Fixture _fixture;
            public Actor(Fixture fixture)
            {
                _fixture = fixture;
                Owner = fixture.Go("Lifecycle actor").AddComponent<TemporaryAITarget>();
                var context = new Context(Owner, Owner.gameObject, Owner.transform, fixture.Resolver, fixture.Collector, null, new global::NoWebGame.Combat.Runtime.Logger("Lifecycle"));
                context.RegisterScheduler(fixture.Scheduler);
                Stats = new StatsBridge(context, new BaseStatsContainer(100, 0, 100, 0, 1, 0, 0));
                context.RegisterStats(Stats);
                Attack = new Attacker(context, Owner.transform, Bindings, fixture.Collector);
                Act = new ActBridge(null, Attack); context.RegisterAct(Act);
            }
            public SkillData Bind(INewMechanism mech, int cost = 10, SkillSlot slot = SkillSlot.Attack)
            {
                var skill = ScriptableObject.CreateInstance<SkillData>(); _fixture.Owned.Add(skill);
                skill.ManaCost = cost; skill.mechanismData = new MechanismData { mechanism = mech, mode = TargetMode.TowardsSelf };
                Bindings[slot] = new SkillBinding { skillData = skill }; return skill;
            }
            public void Cast(params SkillSlot[] slots)
            {
                foreach (var slot in slots) Attack.EnqueueCast(slot);
                Attack.Tick(_fixture.Clock.TickCount, 0);
            }
            public void Kill() => Stats.TakeDamage(new DamageData(DamageType.Fixed, 10000, 100));
            public SkillCommand Child(INewMechanism mech) => new(Owner.transform, TargetMode.TowardsSelf, default, mech, default);
        }
        private sealed class Fixture : IDisposable
        {
            public readonly List<Object> Owned = new();
            public readonly List<Actor> Actors = new();
            public readonly List<SkillCommand> Commands = new();
            public readonly Ticker Clock = new();
            public readonly DelayScheduler Scheduler = new();
            public readonly CommandCollector Collector;
            public readonly TargetResolver Resolver;
            public Fixture()
            {
                typeof(GameTime).GetProperty("Ticker").SetValue(null, Clock);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, Scheduler);
                var go = Go("Lifecycle services"); go.SetActive(false);
                Resolver = go.AddComponent<TargetResolver>(); Collector = go.AddComponent<CommandCollector>(); go.SetActive(true);
                if (!Application.isPlaying) Call(Collector, "OnEnable");
                Collector.CommandEnqueued += Commands.Add;
            }
            public Actor Actor() { var actor = new Actor(this); Actors.Add(actor); return actor; }
            public GameObject Go(string name) { var go = new GameObject(name); Owned.Add(go); return go; }
            public void Step(int count = 1) { for (var i = 0; i < count; i++) Clock.Step(); }
            public void Disable()
            {
                Collector.enabled = false;
                if (!Application.isPlaying) Call(Collector, "OnDisable");
            }
            public void Enable()
            {
                Collector.enabled = true;
                if (!Application.isPlaying) Call(Collector, "OnEnable");
            }
            public void Dispose()
            {
                foreach (var actor in Actors) actor.Attack.Dispose();
                if (Collector) { Collector.CeaseCommand(); if (!Application.isPlaying) Call(Collector, "OnDisable"); }
                for (var i = Owned.Count - 1; i >= 0; i--) if (Owned[i]) Object.DestroyImmediate(Owned[i]);
            }
        }
        static CastLifecycleChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CastLifecycle.Run", false)) return;
                SessionState.SetBool("CastLifecycle.Run", false);
                try { Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunWithRegression() { Run(); CastCommitChecks.RunWithRegression(); }
        public static void RunPlayMode()
        {
            Guard(); SessionState.SetBool("CastLifecycle.Run", true);
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
            var ticker = GameTime.Ticker; var scheduler = GameTime.DelayScheduler;
            var tickerInstance = Ticker.Instance; var schedulerInstance = DelayScheduler.Instance; var collector = CommandCollector.Instance;
            Application.logMessageReceived += Observe;
            try
            {
                CrowdControl(); DeathAndSuspend(); CollectorLifetime(); Exceptions(); Reentrancy();
                if (Application.isPlaying) { PlayerLifecycle(); EnemyAndSummonLifecycle(); ActiveAreaLifetime(); ActiveProjectileLifetime(); }
                Check(Errors.Count == 0, "no unexpected runtime errors: " + string.Join(";", Errors));
                Debug.Log($"CastLifecycleChecks PASS {Results.Count}");
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                Application.logMessageReceived -= Observe; _expectFailure = false;
                typeof(GameTime).GetProperty("Ticker").SetValue(null, ticker);
                typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, scheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, tickerInstance); DelayScheduler.Instance = schedulerInstance;
                typeof(CommandCollector).GetProperty("Instance").SetValue(null, collector);
                File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../../cast-lifecycle-" + (Application.isPlaying ? "play" : "edit") + ".txt")), Results);
            }
        }
        private static Probe ProbeFor(int delay = 0, int cd = 0)
        {
            var probe = new Probe(); JsonUtility.FromJsonOverwrite($"{{\"delayTicks\":{delay},\"cooldownTicks\":{cd}}}", probe); return probe;
        }
        private static void CrowdControl()
        {
            foreach (var type in new[] { EffectType.Suppressed, EffectType.Stunned, EffectType.Tumbled })
            foreach (var scheduled in new[] { false, true })
            {
                using var f = new Fixture(); var a = f.Actor(); var b = f.Actor();
                var p = ProbeFor(5, 12); var q = ProbeFor(5);
                a.Bind(p); b.Bind(q); a.Cast(SkillSlot.Attack); b.Cast(SkillSlot.Attack);
                if (scheduled) f.Step();
                a.Attack.EnqueueCast(SkillSlot.Skill1);
                var cc = new CCData(type, 1); a.Act.ApplyCC(cc);
                Check(f.Commands[0].Commit.State == CastCommitState.Cancelled && !a.Attack.IsPending(SkillSlot.Attack), $"{type}/{scheduled} cancels immediately");
                Check(a.Stats.Stats.Mana == 90, "CC does not refund Mana");
                a.Act.RemoveCC(cc); f.Step(6); a.Cast(SkillSlot.Attack);
                Check(p.Count == 0 && q.Count == 1 && f.Commands.Count == 2, "owner isolation, no revival and cooldown preserved");
                f.Step(12); a.Cast(SkillSlot.Attack); f.Step(5);
                Check(p.Count == 1, "fresh cast works after cooldown");
            }
            using (var f = new Fixture())
            {
                var a = f.Actor(); var p = ProbeFor(2); a.Bind(p); a.Cast(SkillSlot.Attack);
                a.Act.ApplyCC(new CCData(EffectType.Rooted, 1)); f.Step(2);
                Check(p.Count == 1 && a.Act.CanAttack, "Rooted permits attacking");
                var cc = new CCData(EffectType.Suppressed, 1);
                for (var i = 0; i < 256; i++) a.Act.ApplyCC(cc);
                Check(!a.Act.CanAttack, "256 overlapping CC instances cannot wrap to allowed");
                for (var i = 0; i < 255; i++) a.Act.RemoveCC(cc);
                Check(!a.Act.CanAttack, "last overlapping CC still blocks");
                a.Act.RemoveCC(cc); a.Act.RemoveCC(cc); Check(a.Act.CanAttack, "balanced and redundant CC removal is safe");
            }
        }
        private static void DeathAndSuspend()
        {
            using (var targetFixture = new Fixture())
            {
                var owner = targetFixture.Actor(); var lost = ProbeFor(2, 8); owner.Bind(lost, 20); owner.Cast(SkillSlot.Attack);
                owner.Owner.targetable = false; targetFixture.Step(2);
                var aborted = targetFixture.Commands[0].Commit;
                Check(aborted.State == CastCommitState.DispatchAborted && aborted.EndReason == "PreparedTargetLost", "target loss records dispatch rejection, not successful execution");
                Check(lost.Count == 0 && owner.Stats.Stats.Mana == 80 && !owner.Attack.IsPending(SkillSlot.Attack), "target loss keeps cost and releases Pending");
            }
            using var f = new Fixture(); var a = f.Actor(); var p = ProbeFor(4, 10); a.Bind(p, 30);
            a.Cast(SkillSlot.Attack); a.Kill();
            Check(f.Commands[0].Commit.State == CastCommitState.Cancelled && a.Owner.isActiveAndEnabled, "death cancels before owner deactivation");
            Check(a.Stats.Stats.Mana == 70 && !a.Attack.IsPending(SkillSlot.Attack), "death releases Pending without refund");
            a.Stats.ResetStats(); f.Step(5); a.Cast(SkillSlot.Attack);
            Check(p.Count == 0 && f.Commands.Count == 1, "stat reset cannot revive old cast or reset CD");
            f.Step(5); a.Cast(SkillSlot.Attack); a.Attack.Suspend("TestDisabled"); a.Attack.Resume(); f.Step(4);
            Check(p.Count == 0 && a.Stats.Stats.Mana == 70, "suspend/resume preserves spent Mana and cancels pending");
            a.Attack.EnqueueCast(SkillSlot.Attack); a.Attack.Suspend("TestDisabled"); a.Attack.Resume(); f.Step(10); a.Attack.Tick(f.Clock.TickCount, 0);
            Check(f.Commands.Count == 2, "suspend clears buffered input");
            a.Cast(SkillSlot.Attack); a.Attack.Dispose(); a.Attack.Dispose(); a.Attack.Resume(); a.Cast(SkillSlot.Attack);
            Check(f.Scheduler.GetAllEntries().Count == 0 && f.Commands.Count == 3, "dispose removes owned timers once and cannot resume");
        }
        private static void CollectorLifetime()
        {
            using var f = new Fixture(); var a = f.Actor(); var p = ProbeFor(5); a.Bind(p, 20);
            a.Cast(SkillSlot.Attack); f.Step(); a.Attack.EnqueueCast(SkillSlot.Attack);
            f.Disable();
            Check(CommandCollector.Instance == null && !a.Attack.IsPending(SkillSlot.Attack), "collector disable releases pending and global port");
            Check(f.Scheduler.GetAllEntries().Count == 0 && a.Stats.Stats.Mana == 80, "collector disable removes delay without refund");
            Check(!CommandBridge.TryEnqueue(a.Child(p)), "disabled service rejects continuation explicitly");
            f.Enable(); a.Attack.Tick(f.Clock.TickCount, 0); f.Step(6);
            Check(f.Commands.Count == 1 && p.Count == 0, "old service generation rejects buffered input and pending cast");
            a.Cast(SkillSlot.Attack); f.Step(5); Check(p.Count == 1, "new service generation accepts new input");
        }
        private static void Exceptions()
        {
            _expectFailure = true;
            try
            {
                using var f = new Fixture(); var a = f.Actor(); var good = ProbeFor();
                a.Bind(new Broken { Input = true }); a.Bind(good, 10, SkillSlot.Skill1);
                a.Cast(SkillSlot.Attack, SkillSlot.Skill1); f.Step();
                Check(good.Count == 1 && a.Stats.Stats.Mana == 90, "one input exception does not skip next input or spend failed cost");
                f.Collector.EnqueueCommand(a.Child(new Broken()));
                f.Collector.EnqueueCommand(a.Child(good)); f.Step();
                Check(good.Count == 2 && f.Scheduler.GetAllEntries().Count == 0, "scheduling exception isolated and timer cleaned");
                var bad = ProbeFor(); bad.Action = _ => throw new Exception("ExpectedLifecycleExecute");
                a.Bind(bad); a.Bind(good, 10, SkillSlot.Skill1);
                a.Cast(SkillSlot.Attack, SkillSlot.Skill1); f.Step();
                var failed = f.Commands[f.Commands.Count - 2].Commit;
                Check(failed.State == CastCommitState.ExecutionException && good.Count == 3 && bad.Count == 1, "execution exception allows next root");
                Check(!a.Attack.IsPending(SkillSlot.Attack) && a.Stats.Stats.Mana == 70, "execution failure releases Pending without refund");
                f.Step(3); Check(bad.Count == 1, "failed command never replays");
                var display = new BrokenDisplay();
                Set(f.Collector, "_runner", new SkillRunner(f.Resolver, display));
                a.Bind(good); a.Cast(SkillSlot.Attack); f.Step();
                Check(f.Commands[f.Commands.Count - 1].Commit.State == CastCommitState.ExecutionReturned && display.Count == 1 && good.Count == 4, "display failure preserves successful execution");
                f.Step(); Check(good.Count == 4, "display failure does not replay effect");
            }
            finally { _expectFailure = false; }
        }
        private static void Reentrancy()
        {
            using var f = new Fixture(); var a = f.Actor(); var b = f.Actor(); var child = ProbeFor();
            var p = ProbeFor(); var q = ProbeFor(5); a.Bind(p); a.Bind(q, 10, SkillSlot.Skill1); b.Bind(child);
            p.Action = _ =>
            {
                a.Act.ApplyCC(new CCData(EffectType.Suppressed, 1));
                f.Collector.EnqueueCommand(a.Child(child));
                f.Clock.Step(); // 재진입은 같은 Collector의 목록을 다시 순회하지 않아야 합니다.
            };
            a.Cast(SkillSlot.Attack, SkillSlot.Skill1); b.Cast(SkillSlot.Attack); f.Step();
            Check(f.Commands[0].Commit.State == CastCommitState.ExecutionReturned && f.Commands[1].Commit.State == CastCommitState.Cancelled, "executing root survives reentrant CC, pending sibling cancels");
            Check(p.Count == 1 && child.Count == 1, "nested tick does not execute new continuation immediately");
            f.Step(7); Check(child.Count == 2 && q.Count == 0, "continuation survives owner CC and executes once");
            using var g = new Fixture(); var c = g.Actor(); c.Bind(ProbeFor(3));
            g.Collector.CommandEnqueued += _ => { g.Disable(); g.Enable(); };
            c.Cast(SkillSlot.Attack); g.Step(4);
            Check(g.Commands[0].Commit.State == CastCommitState.Cancelled && c.Stats.Stats.Mana == 90 && !c.Attack.IsPending(SkillSlot.Attack), "observer service restart cancels once without post-commit refund");
        }
        private static void PlayerLifecycle()
        {
            using var f = new Fixture(); var seed = f.Actor(); var p = ProbeFor(4, 8); var skill = seed.Bind(p, 20);
            var spec = ScriptableObject.CreateInstance<CharacterSpec>(); f.Owned.Add(spec);
            spec.baseHp = 100; spec.baseMana = 100; spec.CharacterVariables = new();
            spec.attack = new SkillBinding { skillData = skill };
            var go = f.Go("Real lifecycle player"); go.SetActive(false);
            go.AddComponent<Rigidbody2D>(); go.AddComponent<BoxCollider2D>();
            var player = go.AddComponent<PlayerEntity>();
            Set(player, "spec", spec); Set(player, "targetResolver", f.Resolver); Set(player, "commandCollector", f.Collector);
            go.SetActive(true);
            var attack = (Attacker)Get(player, "_attacker");
            attack.EnqueueCast(SkillSlot.Attack); attack.Tick(f.Clock.TickCount, 0); go.SetActive(false);
            Check(f.Commands[0].Commit.State == CastCommitState.Cancelled && !attack.IsPending(SkillSlot.Attack), "real Player OnDisable cancels immediately");
            go.SetActive(true); f.Step(10);
            Check(p.Count == 0, "real Player re-enable does not resurrect cast");
            attack.EnqueueCast(SkillSlot.Attack); f.Step(5);
            Check(p.Count == 1, "real Player re-enable restores ticker and accepts fresh cast");
            var child = ProbeFor(); var pending = ProbeFor(5);
            var dash = new DashMechanism { durationTicks = 2, speed = 0, preventActivation = true,
                onExpire = new List<MechanismData> { new() { mechanism = child, mode = TargetMode.TowardsSelf } } };
            var bindings = (Dictionary<SkillSlot, SkillBinding>)Get(attack, "_skills");
            bindings[SkillSlot.Skill1] = new SkillBinding { skillData = seed.Bind(pending, 10, SkillSlot.Skill1) };
            bindings[SkillSlot.Skill2] = new SkillBinding { skillData = seed.Bind(dash, 10, SkillSlot.Skill2) };
            attack.EnqueueCast(SkillSlot.Skill1); attack.EnqueueCast(SkillSlot.Skill2); attack.Tick(f.Clock.TickCount, 0);
            var sibling = f.Commands[f.Commands.Count - 2].Commit; var dashRoot = f.Commands[f.Commands.Count - 1].Commit;
            f.Step();
            Check(sibling.State == CastCommitState.Cancelled && dashRoot.State == CastCommitState.ExecutionReturned, "prevent-activation Dash cancels pending sibling, not its own execution");
            var mover = (Mover)Get(player, "_mover");
            mover.AfterDashPhysics();
            for (var i = 0; i < 8; i++) { f.Step(); mover.AfterDashPhysics(); }
            Check(child.Count == 1 && pending.Count == 0, "executed Dash retains normal expiration chain");
        }
        private static void EnemyAndSummonLifecycle()
        {
            using var f = new Fixture(); var seed = f.Actor(); var p = ProbeFor(4); var skill = seed.Bind(p);
            var spec = ScriptableObject.CreateInstance<CharacterSpec>(); f.Owned.Add(spec);
            spec.baseHp = 100; spec.baseMana = 100; spec.CharacterVariables = new(); spec.attack = new SkillBinding { skillData = skill };
            var enemyGo = f.Go("Real lifecycle enemy"); enemyGo.SetActive(false);
            enemyGo.AddComponent<Rigidbody2D>(); enemyGo.AddComponent<BoxCollider2D>();
            var enemy = enemyGo.AddComponent<EnemyEntity>(); enemy.InstallSpec(spec);
            Set(enemy, "targetResolver", f.Resolver); Set(enemy, "commandCollector", f.Collector);
            enemyGo.SetActive(true);
            // 동기 검사에서는 Start만 명시 호출합니다. 아래 disable/enable 콜백은 Unity가 호출합니다.
            Call(enemy, "Start");
            VerifyReenable(enemyGo, (Attacker)Get(enemy, "_attacker"), f, p, "Enemy");
            var summonGo = f.Go("Real lifecycle summon"); summonGo.SetActive(false);
            summonGo.AddComponent<Rigidbody2D>(); summonGo.AddComponent<BoxCollider2D>();
            var summon = summonGo.AddComponent<SummonEntity>(); summon.baseHp = 100; summon.baseMana = 100;
            typeof(SummonEntityBase).GetField("targetResolver", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(summon, f.Resolver);
            typeof(SummonEntityBase).GetField("commandCollector", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(summon, f.Collector);
            summonGo.SetActive(true);
            summon.Init(new CastContext(new SummonMechanism(), seed.Owner.transform, SkillTarget.At(Vector2.zero), default));
            var attack = (Attacker)typeof(SummonEntityBase).GetField("_attacker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(summon);
            ((Dictionary<SkillSlot, SkillBinding>)Get(attack, "_skills"))[SkillSlot.Attack] = new SkillBinding { skillData = skill };
            VerifyReenable(summonGo, attack, f, p, "Summon");
        }
        private static void VerifyReenable(GameObject owner, Attacker attack, Fixture f, Probe p, string name)
        {
            var before = p.Count;
            attack.EnqueueCast(SkillSlot.Attack); attack.Tick(f.Clock.TickCount, 0);
            var root = f.Commands[f.Commands.Count - 1].Commit;
            owner.SetActive(false); owner.SetActive(true); f.Step(5);
            Check(root.State == CastCommitState.Cancelled && p.Count == before, name + " real disable/enable cancels old pending cast");
            attack.EnqueueCast(SkillSlot.Attack); f.Step(6);
            Check(p.Count == before + 1, name + " real re-enable resumes ticker and fresh cast");
        }
        private static void ActiveAreaLifetime()
        {
            using var f = new Fixture(); var a = f.Actor(); var child = ProbeFor();
            AreaEntity area = null;
            var p = ProbeFor();
            p.Action = ctx =>
            {
                area = f.Go("Executed area").AddComponent<AreaEntity>();
                var mech = new AreaMechanism { lifeTick = 20, onExpire = new List<MechanismData> { new() { mechanism = child, mode = TargetMode.TowardsSelf } } };
                area.Init(new CastContext(mech, ctx.Caster, ctx.Target, ctx.Damage, originCastPosition: ctx.OriginCastPosition));
            };
            a.Bind(p); a.Cast(SkillSlot.Attack); f.Step();
            a.Act.ApplyCC(new CCData(EffectType.Stunned, 1)); a.Kill(); a.Owner.gameObject.SetActive(false);
            f.Step(5);
            Check(area && child.Count == 0 && f.Commands[0].Commit.State == CastCommitState.ExecutionReturned, "executed Area survives owner CC, death and disable until own lifetime");
            f.Step(20); Check(child.Count == 1, "executed Area publishes expiration continuation once even with inactive owner");
            f.Step(10); Check(child.Count == 1, "expired Area never publishes twice");
        }
        private static void ActiveProjectileLifetime()
        {
            using var f = new Fixture(); var a = f.Actor(); var child = ProbeFor(); ProjectileEntity projectile = null;
            var root = ProbeFor();
            root.Action = ctx =>
            {
                var go = f.Go("Executed projectile"); go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                go.AddComponent<CircleCollider2D>().isTrigger = true; projectile = go.AddComponent<ProjectileEntity>();
                var mech = new ProjectileMechanism { lifeTick = 15, speed = 1,
                    onExpire = new List<MechanismData> { new() { mechanism = child, mode = TargetMode.TowardsCoordinate } } };
                projectile.Init(new CastContext(mech, ctx.Caster, SkillTarget.At(new Vector2(100, 100)), default, originCastPosition: ctx.OriginCastPosition));
            };
            a.Bind(root); a.Cast(SkillSlot.Attack); f.Step();
            a.Act.ApplyCC(new CCData(EffectType.Suppressed, 1)); a.Kill(); a.Owner.gameObject.SetActive(false);
            for (var i = 0; i < 5; i++) { f.Step(); projectile.AfterPhysics(); }
            Check(!projectile.Result.HasValue && child.Count == 0, "executed Projectile survives owner CC, death and disable");
            for (var i = 0; i < 15; i++) { f.Step(); projectile.AfterPhysics(); }
            Check(projectile.Result?.Cause == ExitCause.TimeExpired && child.Count == 1, "Projectile keeps own timeout and continuation");
            projectile.Cancel(); f.Step(3);
            Check(child.Count == 1, "late cancellation cannot republish finished Projectile");
        }
        private static object Get(object obj, string field) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        private static void Call(object obj, string method) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null);
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Results.Add("PASS " + message); }
        private static void Observe(string message, string stack, LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            if (_expectFailure && message.Contains("ExpectedLifecycle")) return;
            Errors.Add(message);
        }
    }
}
