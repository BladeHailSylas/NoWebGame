using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Characters;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Data;
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
    public static class TemporaryEnemyChecks
    {
        private static readonly List<string> Checks = new();
        private static readonly List<Object> Owned = new();
        private static Scene _scene;
        private static readonly Vector2 Origin = new(1000, 1000);

        public static void RunWithRegression()
        {
            EntityRelationChecks.Run();
            Run();
            OriginCastPositionChecks.Run();
        }

        [MenuItem("Tools/ExitLab/Check Temporary Enemy AI")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
            Checks.Clear(); Owned.Clear();
            var oldScene = SceneManager.GetActiveScene();
            var oldTicker = GameTime.Ticker;
            var oldScheduler = GameTime.DelayScheduler;
            var oldTickerInstance = Ticker.Instance;
            var oldSchedulerInstance = DelayScheduler.Instance;
            var oldCollector = CommandCollector.Instance;
            // Preview Scene의 Collider는 기본 Physics2D 검색에서 제외됩니다.
            // batch 사본은 빈 Scene을 사용하고, 대화형 검증은 이름 있는 Scene 옆에만 추가합니다.
            if (!Application.isBatchMode)
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        throw new InvalidOperationException("현재 미저장 Scene을 먼저 저장한 뒤 자동 검증을 실행하세요. 원본 Scene은 자동 저장하지 않습니다.");
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            var collector = default(CommandCollector);
            try
            {
                SetTime(new Ticker(), new DelayScheduler());
                var services = Go("services"); services.SetActive(false);
                collector = services.AddComponent<CommandCollector>();
                // Edit Mode의 MonoBehaviour 생명주기는 검증에서 명시적으로 호출합니다.
                Invoke(collector, "OnEnable");
                var commands = new List<SkillCommand>();
                collector.CommandEnqueued += commands.Add;

                var foeLayer = LayerMask.NameToLayer("Foe");
                var youLayer = LayerMask.NameToLayer("You");
                var allyLayer = LayerMask.NameToLayer("Ally");
                Check(foeLayer >= 0 && youLayer >= 0 && allyLayer >= 0, "required layers");
                var near = Target("You nearest", youLayer, new Vector2(1, 0));
                var far = Target("Ally farther", allyLayer, new Vector2(2, 0));
                var friend = Target("Foe friend", foeLayer, new Vector2(.6f, 0));
                var neutral = Target("Neutral", 0, new Vector2(.7f, 0));
                neutral.RelationProfile = EntityRelationProfile.ExplicitNeutral;
                var unrelated = Target("Unrelated", 0, new Vector2(.8f, 0));
                unrelated.RelationProfile = EntityRelationProfile.ExplicitUnrelated;
                var unknown = Target("Unknown", 0, new Vector2(.9f, 0));
                near.gameObject.AddComponent<CircleCollider2D>().radius = .1f; // 같은 Entity의 중복 후보
                var skill = Skill();
                var spec = Spec(skill);
                var casterObject = Go("Enemy under test"); casterObject.SetActive(false); casterObject.layer = foeLayer;
                casterObject.AddComponent<Rigidbody2D>().gravityScale = 0;
                casterObject.AddComponent<CircleCollider2D>().radius = .1f;
                var enemy = casterObject.AddComponent<EnemyEntity>(); enemy.InstallSpec(spec);
                // 검증용 Collector를 Enemy의 초기화 전에 활성화합니다.
                services.SetActive(true);
                enemy.ConfigureTemporaryAI(collector, seed: 777);
                casterObject.SetActive(true);
                Invoke(enemy, "Start");
                Physics2D.SyncTransforms();
                Step(59);
                Check(commands.Count == 0 && enemy.AIDecisionCount == 0, "no decision before 60 received ticks");
                Step(1);
                Check(enemy.AIDecisionCount == 1 && commands.Count == 1,
                    $"one Attack request at tick 60 (decisions={enemy.AIDecisionCount}, requests={commands.Count})");
                Check(commands[0].Caster == enemy.transform && commands[0].Target.Matches(near), "AI chooses nearest enemy without cursor");
                Check(enemy.AIMovement == Vector2.zero, "attack clears movement");
                Check(near.HitCount == 0, "request does not bypass delay");
                Step(3);
                Check(near.HitCount == 1 && near.LastAttacker == enemy.transform, "real Collector Runner DamageMechanism path hits once");
                Check(friend.HitCount == 0 && neutral.HitCount == 0 && unrelated.HitCount == 0 && unknown.HitCount == 0,
                    "same side and non-enemies not selected");
                Step(57);
                Check(commands.Count == 2 && enemy.AIDecisionCount == 2, "second decision at tick 120");

                // 이미 큐에 들어간 요청도 AI를 끄면 무효화되어야 합니다.
                enemy.TemporaryAIEnabled = false;
                Step(3);
                Check(near.HitCount == 1, "AI disable cancels queued root Attack");
                enemy.TemporaryAIEnabled = true;
                Step(59);
                Check(commands.Count == 2, "reenable waits a fresh 60 ticks");
                Step(1); Step(3);
                Check(commands.Count == 3 && near.HitCount == 2, "reenable resumes without duplicate subscriptions");

                // 같은 Attack을 다른 진영의 Caster로 실행합니다.
                enemy.gameObject.layer = youLayer;
                Step(57); Step(3);
                Check(commands[3].Target.Matches(friend) && friend.HitCount == 1, "same Attack with You caster selects Foe");

                // 대상이 없는 기간에는 이동 입력을 유지합니다.
                foreach (var target in new[] { near, far, friend, neutral, unrelated, unknown }) target.targetable = false;
                Step(57);
                var direction = enemy.AIMovement;
                Check(commands.Count == 4 && direction.sqrMagnitude > .99f && direction.sqrMagnitude < 1.01f, "no target chooses normalized direction");
                Step(10);
                Check(enemy.AIMovement == direction, "movement persists between decisions");
                enemy.TemporaryAIEnabled = false;
                Check(enemy.AIMovement == Vector2.zero, "disable clears movement");
                Invoke(enemy, "OnDisable");
                var before = commands.Count;
                Step(60);
                Check(commands.Count == before, "disabled lifecycle stops AI input");
                enemy.ConfigureTemporaryAI(collector, seed: 777);
                Invoke(enemy, "OnEnable");
                Step(60);
                Check(enemy.AIMovement == direction, "same seed reproduces first walking direction");
                enemy.TemporaryAIEnabled = false;
                Invoke(enemy, "OnDisable");

                // 명시적 루트 요청의 경계 조건을 검증합니다.
                var source = Target("validation caster", foeLayer, new Vector2(0, -10));
                var victim = Target("validation target", youLayer, new Vector2(1, -10));
                var mechanism = skill.mechanismData.mechanism;
                var active = true;
                var request = new ExplicitAttackTarget(source, victim, TargetRelationMask.Enemy, () => active);
                var runner = new SkillRunner(null); // 잘못 커서 탐색으로 돌아가면 null Resolver로 인해 실패합니다.
                SkillCommand Command() => new(source.transform, TargetMode.TowardsEntity, default, mechanism, default,
                    SkillTarget.ForEntity(victim), explicitTarget: request);
                runner.Activate(Command());
                Check(victim.HitCount == 1, "valid explicit request needs no Resolver or Camera");
                victim.transform.position = source.transform.position + Vector3.right * 4;
                runner.Activate(Command()); Check(victim.HitCount == 1, "out of range rejected at execution");
                victim.transform.position = source.transform.position + Vector3.right * .1f;
                runner.Activate(Command()); Check(victim.HitCount == 1, "inside minimum range rejected");
                victim.transform.position = source.transform.position + Vector3.right * 3;
                Check(request.IsValid(source.transform, mechanism), "maximum boundary included");
                victim.transform.position = source.transform.position + Vector3.right * .25f;
                Check(request.IsValid(source.transform, mechanism), "minimum boundary included");
                victim.gameObject.layer = foeLayer;
                runner.Activate(Command()); Check(victim.HitCount == 1, "changed faction rejected");
                victim.gameObject.layer = youLayer;
                victim.targetable = false;
                runner.Activate(Command()); Check(victim.HitCount == 1, "untargetable rejected");
                victim.targetable = true; victim.gameObject.SetActive(false);
                runner.Activate(Command()); Check(victim.HitCount == 1, "inactive target rejected");
                victim.gameObject.SetActive(true); source.enabled = false;
                runner.Activate(Command()); Check(victim.HitCount == 1, "disabled caster rejected");
                source.enabled = true; active = false;
                runner.Activate(Command()); Check(victim.HitCount == 1, "expired request lifetime rejected");
                active = true;
                var lost = Command(); Object.DestroyImmediate(victim.gameObject);
                runner.Activate(lost); Check(true, "destroyed explicit target never falls back to cursor");

                Check(!EntityAttackSelection.IsAllowed(source, far, TargetRelationMask.None, 0, 100), "None rejects selection");
                Check(EntityAttackSelection.FindNearest(source, (TargetRelationMask)128, 0, 100) == null, "legacy layer bit not treated as relation");
                Check(!EntityAttackSelection.ValidRange(float.NaN, 3) && !EntityAttackSelection.ValidRange(0, float.PositiveInfinity), "invalid ranges rejected");
                ValidateSupport(spec);
                InputIsolation(collector);
                Debug.Log($"TemporaryEnemyChecks: PASS ({Checks.Count} assertions)");
                Checks.Add($"PASS: total {Checks.Count} assertions");
            }
            catch (Exception error) { Checks.Add("FAIL: " + error); throw; }
            finally
            {
                if (collector) Invoke(collector, "OnDisable");
                if (!Application.isBatchMode) EditorSceneManager.CloseScene(_scene, true);
                else foreach (var root in _scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                foreach (var item in Owned) if (item) Object.DestroyImmediate(item);
                SetTime(oldTicker, oldScheduler);
                typeof(Ticker).GetProperty("Instance").SetValue(null, oldTickerInstance);
                DelayScheduler.Instance = oldSchedulerInstance;
                typeof(CommandCollector).GetProperty("Instance").SetValue(null, oldCollector);
                if (oldScene.IsValid()) SceneManager.SetActiveScene(oldScene);
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../audit-results/temporary-ai-tests.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllLines(output, Checks);
            }
        }

        private static void InputIsolation(CommandCollector collector)
        {
            var caster = Target("input caster", LayerMask.NameToLayer("Foe"), new Vector2(20, 0));
            var one = Target("input target 1", LayerMask.NameToLayer("You"), new Vector2(21, 0));
            var two = Target("input target 2", LayerMask.NameToLayer("You"), new Vector2(22, 0));
            var skill = Skill(); var spec = Spec(skill);
            var context = new Context(caster, caster.gameObject, caster.transform, null, collector, spec, new PlayerScripts.Core.Logger("test"));
            context.RegisterScheduler(GameTime.DelayScheduler);
            var stats = new StatsBridge(context, new BaseStatsContainer(100, 0, 0, 0, 100, 0, 0));
            context.RegisterStats(stats); context.RegisterVariableStorage(new VariableStorage());
            var attacker = new Attacker(context, caster.transform, new Dictionary<SkillSlot, SkillBinding> { { SkillSlot.Attack, spec.attack } }, collector);
            var captured = new List<SkillCommand>();
            collector.CommandEnqueued += captured.Add;
            var first = new ExplicitAttackTarget(caster, one, TargetRelationMask.Enemy);
            var second = new ExplicitAttackTarget(caster, two, TargetRelationMask.Enemy);
            attacker.EnqueueCast(SkillSlot.Attack, first); attacker.EnqueueCast(SkillSlot.Attack, second);
            attacker.Tick(GameTime.Ticker.TickCount, 0);
            Check(captured.Count == 2 && captured[0].Target.Matches(one) && captured[1].Target.Matches(two), "request targets isolated in shared queue");
            attacker.EnqueueCast(SkillSlot.Attack, first); attacker.Tick(GameTime.Ticker.TickCount, 1);
            Check(captured.Count == 2, "innoxious input blocked by existing Attacker");
            attacker.EnqueueCast(SkillSlot.Attack); attacker.Tick(GameTime.Ticker.TickCount, 0);
            Check(captured.Count == 3 && captured[2].ExplicitTarget == null && !captured[2].Target.IsResolved, "legacy player input stays unresolved");
            // 미지정 플레이어 입력은 실행하지 않고 요청 내용만 관찰합니다.
            collector.CeaseCommand(); collector.CommandEnqueued -= captured.Add;
        }

        private static void ValidateSupport(CharacterSpec spec)
        {
            Check(TemporaryAttackSupport.Validate(spec.attack, out _), "direct Damage Attack supported");
            var damage = (DamageMechanism)spec.attack.skillData.mechanismData.mechanism;
            damage.onHit = new List<MechanismData> { new() { mechanism = new DamageMechanism(), requireRetarget = true } };
            Check(!TemporaryAttackSupport.Validate(spec.attack, out _), "retarget rejected before mouse fallback");
            damage.onHit[0].requireRetarget = false;
            damage.onHit[0].mechanism = damage;
            Check(!TemporaryAttackSupport.Validate(spec.attack, out _), "cyclic Chain rejected");
            damage.onHit = null;
            var binding = spec.attack; binding.mode = TargetMode.TowardsCursor;
            Check(!TemporaryAttackSupport.Validate(binding, out _), "cursor Attack rejected");
        }

        private static SkillData Skill()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>(); Owned.Add(skill);
            skill.mechanismData = new MechanismData { mode = TargetMode.TowardsEntity,
                mechanism = JsonUtility.FromJson<DamageMechanism>("{\"minRange\":250,\"maxRange\":3000,\"delayTicks\":2,\"damageValue\":100}") };
            return skill;
        }
        private static CharacterSpec Spec(SkillData skill)
        {
            var spec = ScriptableObject.CreateInstance<CharacterSpec>(); Owned.Add(spec);
            spec.baseHp = 100; spec.baseAttack = 100; spec.baseSpeed = 0;
            spec.CharacterVariables = new List<VariableDefinition>();
            spec.attack = new SkillBinding { slot = SkillSlot.Attack, mode = TargetMode.TowardsEntity, skillData = skill };
            return spec;
        }
        private static GameObject Go(string name)
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, _scene); go.transform.position = Origin; return go;
        }
        private static TemporaryAITarget Target(string name, int layer, Vector2 offset)
        {
            var go = Go(name); go.layer = layer; go.transform.position = Origin + offset;
            var entity = go.AddComponent<TemporaryAITarget>();
            var child = Go("child Collider"); child.transform.SetParent(go.transform, false); child.transform.localPosition = Vector3.zero;
            child.AddComponent<CircleCollider2D>().radius = .1f;
            return entity;
        }
        private static void Step(int count) { for (var i = 0; i < count; i++) GameTime.Ticker.Step(); }
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void SetTime(Ticker ticker, DelayScheduler scheduler)
        {
            typeof(GameTime).GetProperty("Ticker").SetValue(null, ticker);
            typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, scheduler);
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Checks.Add("PASS: " + message);
        }
    }
}
