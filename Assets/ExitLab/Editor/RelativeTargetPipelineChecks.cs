using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Moves;
using Moves.Mechanisms;
using Moves.ObjectEntity;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using Systems.SubSystems;
using Systems.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using GameTime = Systems.Time.Time;

namespace ExitLab
{
    /// <summary>에셋을 저장하지 않는 상대 Mask 통합 검사. 실제 Collider/물리/Collector 경계를 통과합니다.</summary>
    public static class RelativeTargetPipelineChecks
    {
        private static readonly List<string> Results = new();
        private static int _fixture;
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static void SetTime(Ticker ticker, DelayScheduler scheduler)
        {
            typeof(GameTime).GetProperty("Ticker").SetValue(null, ticker);
            typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, scheduler);
        }
        private static T Relative<T>(TargetRelationMask mask) where T : NewMechanism, new()
        {
            var mech = new T();
            JsonUtility.FromJsonOverwrite("{\"maxRange\":10000,\"mask\":{\"m_Bits\":128}}", mech);
            mech.ConfigureRelativeTargets(mask);
            return mech;
        }
        private static List<MechanismData> Follow() => new() { new MechanismData { mechanism = new DummyMechanism(), mode = TargetMode.TowardsEntity } };

        [MenuItem("Tools/ExitLab/Check Relative Target Pipeline")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
            if (!Application.isBatchMode)
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        throw new InvalidOperationException("미저장 Scene은 먼저 보존하세요. 검사는 원본 Scene을 저장하지 않습니다.");
            Results.Clear(); _fixture = 0;
            var oldScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            var simulation = Physics2D.simulationMode;
            var triggers = Physics2D.queriesHitTriggers;
            var starts = Physics2D.queriesStartInColliders;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                Physics2D.queriesHitTriggers = true;
                Physics2D.queriesStartInColliders = true;
                SelectionAndEffects();
                QueryChecks();
                TravelChecks();
                PolicyChecks();
                SerializationChecks();
                Debug.Log($"RelativeTargetPipelineChecks PASS {Results.Count}");
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                Physics2D.simulationMode = simulation; Physics2D.queriesHitTriggers = triggers; Physics2D.queriesStartInColliders = starts;
                File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../target-stage2-tests.txt"), Results);
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                if (oldScene.IsValid() && oldScene.isLoaded) SceneManager.SetActiveScene(oldScene);
            }
        }

        private static void SelectionAndEffects()
        {
            using var f = new Fixture();
            var caster = f.Actor("Foe", 0);
            var friend = f.Actor("Foe", 2);
            var enemy = f.Actor("You", 2, true);
            var resolver = f.Services.GetComponent<TargetResolver>();
            var attack = Relative<DamageMechanism>(TargetRelationMask.Enemy);
            var req = new TargetRequest(caster.transform, 0, 10, TargetMode.TowardsEntity, 0, targeting: attack.Targeting);
            Physics2D.SyncTransforms();
            var found = resolver.ResolveRelativeAt(req, enemy.transform.position);
            Check(found.Target.Matches(enemy), "overlapping ally does not hide enemy with Default child collider");
            enemy.targetable = false;
            Check(!resolver.ResolveRelativeAt(req, enemy.transform.position).Found, "untargetable target rejected");
            enemy.targetable = true;
            caster.targetable = false;
            Check(resolver.ResolveRelativeAt(req, enemy.transform.position).Found, "untargetable caster still has relationship information");
            caster.targetable = true;
            var selfReq = new TargetRequest(caster.transform, 0, 10, TargetMode.TowardsSelf, 0, targeting: attack.Targeting);
            Check(!resolver.ResolveTarget(selfReq).Found, "TowardsSelf requires Self");
            attack.ConfigureRelativeTargets(TargetRelationMask.Self);
            selfReq = new TargetRequest(caster.transform, 0, 10, TargetMode.TowardsSelf, 0, targeting: attack.Targeting);
            Check(resolver.ResolveTarget(selfReq).Target.Matches(caster), "TowardsSelf selects exact Entity");
            attack.damageValue = 100;
            var runner = new SkillRunner(resolver);
            runner.Activate(new SkillCommand(caster.transform, TargetMode.TowardsEntity, default, attack, default, SkillTarget.ForEntity(friend)));
            Check(friend.HitCount == 0, "inherited Ally cannot receive Self-only damage");
            runner.Activate(new SkillCommand(caster.transform, TargetMode.TowardsEntity, default, attack, default, SkillTarget.ForEntity(caster)));
            Check(caster.HitCount == 1, "Self-only direct effect delivered");
            foreach (var mask in new[] { TargetRelationMask.Enemy, TargetRelationMask.None, TargetRelationMask.Ally })
            {
                attack.ConfigureRelativeTargets(mask);
                var before = enemy.HitCount;
                runner.Activate(new SkillCommand(caster.transform, TargetMode.TowardsEntity, default, attack, default, SkillTarget.ForEntity(enemy)));
                Check(enemy.HitCount - before == (mask == TargetRelationMask.Enemy ? 1 : 0), "direct child effect mask " + mask);
            }
            var stackEffect = Relative<StackMechanism>(TargetRelationMask.Self);
            stackEffect.onHit = new(); stackEffect.onExpire = new();
            stackEffect.Execute(new CastContext(stackEffect, caster.transform, SkillTarget.ForEntity(friend), default));
            Check(friend.StackCount == 0, "Self-only stack rejected for Ally");
            stackEffect.Execute(new CastContext(stackEffect, caster.transform, SkillTarget.ForEntity(caster), default));
            Check(caster.StackCount == 1, "Self-only stack reaches caster");
            var parent = Relative<DummyMechanism>(TargetRelationMask.Enemy);
            var child = Relative<DamageMechanism>(TargetRelationMask.Ally);
            var ctx = new CastContext(parent, caster.transform, SkillTarget.ForEntity(enemy), default, originCastPosition: new Vector2(5, 7));
            SkillUtils.ActivateChain(new[] { new MechanismData { mechanism = child, mode = TargetMode.TowardsEntity, requireRetarget = true } }, ctx);
            var cmd = f.Commands.Last();
            Check(!cmd.Target.IsResolved && cmd.Targeting.Relations == TargetRelationMask.Ally, "retarget child owns its mask and discards old target");
            var childResult = resolver.ResolveRelativeAt(new TargetRequest(cmd.Caster, 0, 10, cmd.TargetMode, cmd.Mask, targeting: cmd.Targeting), enemy.transform.position);
            Check(childResult.Target.Matches(friend), "retarget Ally finds friend rather than parent enemy");
            friend.gameObject.SetActive(false); Physics2D.SyncTransforms();
            Check(!resolver.ResolveRelativeAt(new TargetRequest(cmd.Caster, 0, 10, cmd.TargetMode, cmd.Mask, targeting: cmd.Targeting), enemy.transform.position).Found && !cmd.Target.IsResolved,
                "failed retarget does not reuse parent target");
            friend.gameObject.SetActive(true);
            Check(cmd.OriginCastPosition == new Vector2(5, 7) && cmd.Caster == caster.transform, "chain preserves origin and Caster");
            var fail = Relative<DetectMechanism>(TargetRelationMask.None);
            fail.requiredMode = TargetMode.TowardsEntity; fail.onNotFound = Follow().ToArray(); fail.onDetected = Array.Empty<MechanismData>();
            f.Commands.Clear();
            fail.Execute(new CastContext(fail, caster.transform, SkillTarget.ForEntity(enemy), default));
            Check(f.Commands.Count == 1, "Detect inherited rejected relation takes onNotFound");
            var control = Relative<DummyMechanism>(TargetRelationMask.None);
            control.onHit = Follow(); control.onExpire = new(); f.Commands.Clear();
            control.Execute(new CastContext(control, caster.transform, SkillTarget.ForEntity(enemy), default));
            Check(f.Commands.Count == 1, "None does not block pure control chain forwarding");
            var unrelated = f.Actor("Default", 3); unrelated.RelationProfile = EntityRelationProfile.ExplicitUnrelated;
            var neutral = f.Actor("Default", 4); neutral.RelationProfile = EntityRelationProfile.ExplicitNeutral;
            var unknown = f.Actor("Default", 5);
            foreach (var item in new[] { (unrelated, TargetRelationMask.Unrelated), (neutral, TargetRelationMask.Neutral) })
            {
                attack.ConfigureRelativeTargets(item.Item2);
                Check(attack.Targeting.Allows(caster.transform, item.Item1), "explicit profile " + item.Item2);
                Check(!attack.Targeting.Allows(caster.transform, unknown), "Unknown excluded for " + item.Item2);
            }
            attack.ConfigureRelativeTargets(TargetRelationMask.Enemy);
            enemy.gameObject.SetActive(false);
            Check(!attack.Targeting.Allows(caster.transform, enemy), "inactive target excluded");
            Object.DestroyImmediate(enemy.gameObject);
            Check(!attack.Targeting.Allows(caster.transform, enemy), "destroyed target excluded");
        }

        private static void QueryChecks()
        {
            foreach (var kind in new[] { "Melee", "Hitscan", "Ray", "Circle", "Box", "Laser" })
            using (var f = new Fixture())
            {
                var caster = f.Actor("Foe", 0);
                var friend = f.Actor("Foe", 1);
                var enemy = f.Actor("You", 2, true);
                f.Collider(enemy.transform); // 동일 Entity의 두 번째 Collider
                NewMechanism mech;
                var onHit = Follow();
                if (kind == "Melee") { var m = Relative<MeleeMechanism>(TargetRelationMask.Enemy); m.angleDeg = 360; m.onHit = onHit; m.onExpire = new(); mech = m; }
                else if (kind == "Hitscan") { var m = Relative<HitscanMechanism>(TargetRelationMask.Enemy); m.onHit = onHit; m.onExpire = new(); mech = m; }
                else if (kind == "Ray") { var m = Relative<RayMechanism>(TargetRelationMask.Enemy); m.onHit = onHit; mech = m; }
                else { var m = Relative<AreaMechanism>(TargetRelationMask.Enemy); m.onEnter = onHit; m.onExpire = new(); m.lifeTick = 120; mech = m; }
                Physics2D.SyncTransforms();
                var ctx = new CastContext(mech, caster.transform, SkillTarget.At(f.At(6)), default);
                AreaEntity area = null;
                if (mech is AreaMechanism am)
                {
                    area = f.Go("area", 0).AddComponent<AreaEntity>();
                    area.areaShape = kind == "Circle" ? new CircleArea { Radius = 10000 } :
                        kind == "Box" ? new BoxArea { Width = 20000, Height = 20000 } : new LaserArea { Width = 2000 };
                    area.Init(ctx);
                }
                else ((INewMechanism)mech).Execute(ctx);
                Check(f.Commands.Count == 1 && f.Commands[0].Target.Matches(enemy), kind + " ignores Ally, hits child Collider Entity once");
                if (area)
                {
                    Invoke(area, "ActivateInterval");
                    Check(f.Commands.Count == 2, kind + " next interval may hit same Entity again");
                    // 검사에서 직접 구독을 정리하여 다른 Fixture의 Tick으로 영역을 갱신하지 않습니다.
                    var handler = (Action<long>)Delegate.CreateDelegate(typeof(Action<long>), area, "TickHandler");
                    GameTime.Ticker.OnTick -= handler;
                }
                else if (kind != "Melee")
                {
                    f.Commands.Clear();
                    var wall = f.Go("wall", 1.5f); wall.layer = LayerMask.NameToLayer("Walls&Obstacles"); wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 2);
                    Physics2D.SyncTransforms(); ((INewMechanism)mech).Execute(ctx);
                    Check(f.Commands.Count == 0, kind + " walls stop hit search");
                }
                mech.ConfigureRelativeTargets(TargetRelationMask.None);
                f.Commands.Clear();
                if (area) Invoke(area, "ActivateInterval"); else ((INewMechanism)mech).Execute(ctx);
                Check(f.Commands.Count == 0, kind + " None does not produce Entity hits");
            }
        }

        private static void TravelChecks()
        {
            using (var f = new Fixture())
            {
                var caster = f.Actor("Foe", 0); var target = f.Actor("You", 2);
                var wall = f.Go("wall", 2); wall.layer = LayerMask.NameToLayer("Walls&Obstacles");
                wall.AddComponent<CircleCollider2D>().radius = .2f;
                var body = f.Go("projectile", 0); var rb = body.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                var col = body.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
                var mech = Relative<ProjectileMechanism>(TargetRelationMask.Enemy); var hits = new List<Entity>();
                var travel = new TravelMotion(rb, col, new CastContext(mech, caster.transform, SkillTarget.ForEntity(target), default), 120, false, TargetPolicy.WallLayers, 128, hits.Add);
                Physics2D.SyncTransforms(); travel.Advance(); Physics2D.Simulate(.02f);
                Check(travel.AfterPhysics()?.Cause == ExitCause.WallBlocked && hits.Count == 0, "Travel wall wins equal-distance Entity hit");
            }
            using (var f = new Fixture())
            {
                var caster = f.Actor("Foe", 0); var target = f.Actor("You", 1);
                var body = f.Go("projectile", 0); var rb = body.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                var col = body.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
                var mech = Relative<ProjectileMechanism>(TargetRelationMask.None); var hits = new List<Entity>();
                var travel = new TravelMotion(rb, col, new CastContext(mech, caster.transform, SkillTarget.At(f.At(2)), default), 120, false, TargetPolicy.WallLayers, 128, hits.Add);
                Physics2D.SyncTransforms(); travel.Advance(); Physics2D.Simulate(.02f);
                Check(travel.AfterPhysics()?.Cause == ExitCause.PositionReached && hits.Count == 0, "None permits coordinate travel without Entity hit");
            }
            foreach (var dash in new[] { false, true })
            foreach (var pierce in new[] { false, true })
            using (var f = new Fixture())
            {
                var caster = f.Actor("Foe", 0, true);
                var friend = f.Actor("Foe", 1);
                var enemy = f.Actor("You", 3, true); f.Collider(enemy.transform);
                NewMechanism mech = dash ? Relative<DashMechanism>(TargetRelationMask.Enemy) : Relative<ProjectileMechanism>(TargetRelationMask.Enemy);
                var body = dash ? caster.gameObject : f.Go("projectile", 0);
                var rb = body.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                var col = body.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
                var hits = new List<Entity>();
                var travel = new TravelMotion(rb, col, new CastContext(mech, caster.transform, SkillTarget.ForEntity(enemy), default), 60, pierce,
                    TargetPolicy.WallLayers, LayerMask.GetMask("Foe"), hits.Add);
                Physics2D.SyncTransforms();
                TravelResult? result = null;
                for (var i = 0; i < 8 && !result.HasValue; i++)
                { result = travel.Advance(); Physics2D.Simulate(.02f); result ??= travel.AfterPhysics(); }
                Check(result?.Kind == ExitKind.Completed && result?.Cause == ExitCause.TargetHit && result?.HitEntity == enemy,
                    $"{(dash ? "Dash" : "Projectile")} pierce={pierce} Foe reaches You as TargetHit");
                Check(hits.Count == 1 && hits[0] == enemy, "Travel ignores Ally and deduplicates child colliders");
            }
            foreach (var mask in new[] { TargetRelationMask.None, TargetRelationMask.Self, TargetRelationMask.Enemy })
            using (var f = new Fixture())
            {
                var caster = f.Actor("Foe", 0);
                var body = f.Go("projectile", 0); var rb = body.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                var col = body.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
                var mech = Relative<ProjectileMechanism>(mask); var hits = new List<Entity>();
                var travel = new TravelMotion(rb, col, new CastContext(mech, caster.transform, SkillTarget.At(f.At(3)), default), 60, false, TargetPolicy.WallLayers, 128, hits.Add);
                Physics2D.SyncTransforms(); travel.Advance(); Physics2D.Simulate(.02f); var result = travel.AfterPhysics();
                Check(hits.Count == (mask == TargetRelationMask.Self ? 1 : 0), "Projectile Caster Self permission " + mask);
                if (mask != TargetRelationMask.Self) Check(!result.HasValue, "rejected caster does not stop projectile " + mask);
            }
            using (var f = new Fixture())
            {
                var caster = f.Actor("Foe", 0);
                var target = f.Actor("You", 2, true);
                var body = f.Go("projectile", 0); var rb = body.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                var col = body.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
                var mech = Relative<ProjectileMechanism>(TargetRelationMask.Enemy);
                var hits = new List<Entity>();
                var travel = new TravelMotion(rb, col, new CastContext(mech, caster.transform, SkillTarget.ForEntity(target), default), 120, false, TargetPolicy.WallLayers, 128, hits.Add);
                // 이동 검사 뒤, 도착 검사 전에 Collider가 나타나는 경우로 Overlap 경로를 독립 검증합니다.
                target.gameObject.SetActive(false); Physics2D.SyncTransforms();
                // Entity는 활성 상태로 유지하고 Collider만 제외합니다.
                target.gameObject.SetActive(true); foreach (var c in target.GetComponentsInChildren<Collider2D>()) c.enabled = false;
                Physics2D.SyncTransforms(); travel.Advance(); Physics2D.Simulate(.02f);
                foreach (var c in target.GetComponentsInChildren<Collider2D>()) c.enabled = true;
                Physics2D.SyncTransforms(); var result = travel.AfterPhysics();
                Check(result?.Cause == ExitCause.TargetHit && hits.Count == 1, "arrival Overlap accepts You child Collider");
            }
            using (var f = new Fixture())
            {
                var caster = f.Actor("Foe", 0); var target = f.Actor("You", 2);
                var body = f.Go("projectile", 0); var rb = body.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
                var col = body.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
                var mech = Relative<ProjectileMechanism>(TargetRelationMask.None);
                var hits = new List<Entity>();
                var travel = new TravelMotion(rb, col, new CastContext(mech, caster.transform, SkillTarget.ForEntity(target), default), 120, false, TargetPolicy.WallLayers, 128, hits.Add);
                Physics2D.SyncTransforms(); travel.Advance(); Physics2D.Simulate(.02f);
                Check(!travel.AfterPhysics().HasValue && hits.Count == 0, "rejected designated Entity does not cause arrival termination");
            }
        }

        private static void PolicyChecks()
        {
            using var f = new Fixture();
            var victim = f.Actor("You", 0); var caster = f.Actor("Foe", 2);
            var context = new Context(victim, victim.gameObject, victim.transform, null, null, null, null);
            var stats = new StatsBridge(context, new BaseStatsContainer(10000, 0, 0, 0, 100, 0, 0));
            var storage = new VariableStorage(); var stack = new StackManager(context, storage);
            var filter = new InteractionFilter(victim, stats, stack);
            void Damage(Transform source) => filter.FilterDamage(new DamageData(DamageType.Fixed, 100, 100, attacker: source, useRelationPolicy: true));
            Damage(caster.transform); Check(stats.Stats.Health == 10000, "Enemy receiver permission required");
            foreach (var name in new[] { "AllowEnemyDamage", "AllowNeutralDamage", "AllowOwnerDamage" })
            {
                var def = ScriptableObject.CreateInstance<VariableDefinition>(); f.Owned.Add(def); def.name = name; def.displayName = name; def.exclusiveGroup = Array.Empty<Systems.Stacks.ExclusiveGroups.ExclusiveGroup>();
                storage.AddStorage(new StackKey(def), new VariableState(1, 0));
            }
            Damage(caster.transform); Check(stats.Stats.Health == 9900, "Enemy permission allows new damage");
            Damage(null); Check(stats.Stats.Health == 9800, "environment Unknown uses explicit compatibility branch");
            caster.gameObject.layer = 0;
            Damage(caster.transform); Check(stats.Stats.Health == 9800, "Entity with unknown affiliation is not environmental damage");
            caster.RelationProfile = EntityRelationProfile.ExplicitUnrelated;
            Damage(caster.transform); Check(stats.Stats.Health == 9800, "Unrelated damage denied");
            caster.RelationProfile = EntityRelationProfile.ExplicitNeutral;
            Damage(caster.transform); Check(stats.Stats.Health == 9700, "explicit Neutral damage uses permission");
            caster.RelationProfile = EntityRelationProfile.LegacyLayer; caster.gameObject.layer = LayerMask.NameToLayer("You");
            Damage(caster.transform); Check(stats.Stats.Health == 9700, "Ally damage remains denied");
            victim.Owner = caster.transform;
            Damage(caster.transform); Check(stats.Stats.Health == 9600, "Owner is independent receiver permission");
            Damage(victim.transform); Check(stats.Stats.Health == 9500, "Self precedence over Owner");
            var summon = f.Actor("Default", 4);
            caster.gameObject.layer = LayerMask.NameToLayer("Foe");
            TargetPolicy.CopyAffiliation(caster.transform, summon);
            var enemyMask = new TargetPolicy(1, TargetRelationMask.Enemy);
            Check(summon.gameObject.layer == caster.gameObject.layer && enemyMask.Allows(summon.transform, victim), "summon copies Foe and independent caster attacks You");
            Check(EntityRelationResolver.Resolve(caster, summon) == EntityRelation.Ally, "summon is not Self of owner");
            caster.gameObject.layer = LayerMask.NameToLayer("You");
            Check(summon.gameObject.layer == LayerMask.NameToLayer("Foe"), "summon affiliation is creation snapshot");
            caster.RelationProfile = EntityRelationProfile.ExplicitNeutral; TargetPolicy.CopyAffiliation(caster.transform, summon);
            Check(summon.RelationProfile == EntityRelationProfile.ExplicitNeutral, "summon explicit profile copied");
            foreach (var relative in new[] { false, true })
            {
                var go = f.Go("real summon", 6); go.AddComponent<Rigidbody2D>(); go.AddComponent<CircleCollider2D>();
                var actual = go.AddComponent<SummonEntity>();
                var mechanism = new SummonMechanism();
                if (relative) mechanism.ConfigureRelativeTargets(TargetRelationMask.None);
                actual.Init(new CastContext(mechanism, caster.transform, SkillTarget.At(f.At(6)), default));
                Check(actual.Owner == caster.transform && actual.RelationProfile == (relative ? EntityRelationProfile.ExplicitNeutral : EntityRelationProfile.LegacyLayer),
                    "real Summon.Init respects transition mode " + relative);
            }
        }

        private static void SerializationChecks()
        {
            var legacy = new DamageMechanism();
            JsonUtility.FromJsonOverwrite("{\"mask\":{\"serializedVersion\":2,\"m_Bits\":128}}", legacy);
            Check(!legacy.Targeting.IsRelative && legacy.Mask.value == 128, "missing schema preserves legacy LayerMask");
            var relative = Relative<DamageMechanism>(TargetRelationMask.None);
            var copy = JsonUtility.FromJson<DamageMechanism>(JsonUtility.ToJson(relative));
            Check(copy.Targeting.IsRelative && copy.Targeting.Relations == TargetRelationMask.None && copy.Mask.value == 128, "None is explicit new setting not unmigrated marker");
            Check(!new TargetPolicy(99, TargetRelationMask.Enemy).IsValid, "unknown version fails closed");
            Check(!new TargetPolicy(1, (TargetRelationMask)128).IsValid, "legacy bits never become relation bits");
        }

        private sealed class Fixture : IDisposable
        {
            public readonly List<Object> Owned = new();
            public readonly List<SkillCommand> Commands = new();
            public readonly GameObject Services;
            public readonly CommandCollector Collector;
            private readonly Vector2 _origin = new(1000 + 30 * _fixture++, 1000);
            private readonly Ticker _oldTicker = GameTime.Ticker, _oldInstance = Ticker.Instance;
            private readonly DelayScheduler _oldScheduler = GameTime.DelayScheduler, _oldSchedulerInstance = DelayScheduler.Instance;
            private readonly CommandCollector _oldCollector = CommandCollector.Instance;
            public Fixture()
            {
                SetTime(new Ticker(), new DelayScheduler());
                typeof(Ticker).GetProperty("Instance").SetValue(null, GameTime.Ticker); DelayScheduler.Instance = GameTime.DelayScheduler;
                Services = Go("services", -10); Services.SetActive(false); Services.AddComponent<TargetResolver>();
                Collector = Services.AddComponent<CommandCollector>(); Invoke(Collector, "OnEnable");
                Collector.CommandEnqueued += Commands.Add;
            }
            public Vector2 At(float x) => _origin + new Vector2(x, 0);
            public GameObject Go(string name, float x)
            { var go = new GameObject(name); go.transform.position = At(x); Owned.Add(go); return go; }
            public RelativeTargetTestEntity Actor(string layer, float x, bool child = false)
            {
                var go = Go(layer, x); go.layer = LayerMask.NameToLayer(layer);
                var entity = go.AddComponent<RelativeTargetTestEntity>();
                var owner = go.transform;
                if (child) { var c = Go("Default child collider", x); c.transform.SetParent(go.transform, true); owner = c.transform; }
                Collider(owner); return entity;
            }
            public void Collider(Transform owner) { var col = owner.gameObject.AddComponent<CircleCollider2D>(); col.radius = .2f; col.isTrigger = true; }
            public void Dispose()
            {
                Invoke(Collector, "OnDisable");
                for (var i = Owned.Count - 1; i >= 0; i--) if (Owned[i]) Object.DestroyImmediate(Owned[i]);
                SetTime(_oldTicker, _oldScheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, _oldInstance); DelayScheduler.Instance = _oldSchedulerInstance;
                typeof(CommandCollector).GetProperty("Instance").SetValue(null, _oldCollector);
            }
        }
    }
}
