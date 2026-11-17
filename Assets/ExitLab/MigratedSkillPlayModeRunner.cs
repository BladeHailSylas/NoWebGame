#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Characters;
using Moves;
using Moves.Mechanisms;
using Moves.ObjectEntity;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Time;
using UnityEditor;
using UnityEngine;
using GameTime = Systems.Time.Time;

namespace ExitLab
{
    /// <summary>마이그레이션된 실제 저장 스킬을 복제하여 실행하고 원본 에셋은 보존합니다.</summary>
    public sealed class MigratedSkillPlayModeRunner : MonoBehaviour
    {
        private readonly List<string> _results = new();
        private readonly List<string> _errors = new();
        private void Check(bool ok, string name) { if (!ok) throw new Exception(name); _results.Add("PASS " + name); }
        private void Capture(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) _errors.Add(message); }
        private IEnumerator Start()
        {
            Application.logMessageReceived += Capture;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.queriesHitTriggers = true; Physics2D.queriesStartInColliders = true;
            var success = false;
            try
            {
                foreach (var skill in new[] { "SkillData", "SkillData 1", "SkillData 2" })
                foreach (var source in new[] { "You", "Foe" }) RunCase(skill, source);
                Check(_errors.Count == 0, "no runtime errors: " + string.Join(" | ", _errors));
                success = true;
            }
            catch (Exception error) { _results.Add("FAIL " + error); }
            File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../target-stage3-content-tests.txt"), _results);
            Debug.Log($"MigratedSkillPlayMode {(success ? "PASS" : "FAIL")} {_results.Count}");
            Application.logMessageReceived -= Capture;
            yield return null;
            EditorApplication.Exit(success ? 0 : 1);
        }
        private void RunCase(string skill, string sourceLayer)
        {
            var label = skill + " caster=" + sourceLayer;
            var objects = new List<GameObject>();
            GameObject Go(string name, Vector2 position)
            { var go = new GameObject(name); go.transform.position = position; objects.Add(go); return go; }
            GameTime.Ticker = new Ticker(); GameTime.DelayScheduler = new DelayScheduler();
            typeof(Ticker).GetProperty("Instance").SetValue(null, GameTime.Ticker);
            DelayScheduler.Instance = GameTime.DelayScheduler;
            var services = Go("services", new Vector2(20, 20)); services.AddComponent<TargetResolver>();
            var collector = services.AddComponent<CommandCollector>();
            var commands = new List<SkillCommand>(); collector.CommandEnqueued += commands.Add;
            var casterObject = Go("caster", Vector2.zero); casterObject.layer = LayerMask.NameToLayer(sourceLayer);
            var caster = casterObject.AddComponent<RelativeTargetTestEntity>();
            var rb = casterObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
            var col = casterObject.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
            var targetObject = Go("opponent", Vector2.right); targetObject.layer = LayerMask.NameToLayer(sourceLayer == "Foe" ? "You" : "Foe");
            var target = targetObject.AddComponent<RelativeTargetTestEntity>();
            var child = Go("Default child collider", Vector2.right); child.transform.SetParent(target.transform);
            var childCol = child.AddComponent<CircleCollider2D>(); childCol.radius = .15f; childCol.isTrigger = true;
            var context = new Context(caster, casterObject, caster.transform, services.GetComponent<TargetResolver>(), collector, null, new PlayerScripts.Core.Logger("MigrationTest"));
            context.RegisterScheduler(GameTime.DelayScheduler);
            var stats = new StatsBridge(context, new BaseStatsContainer(1000, 0, 0, 0, 100, 0, 1000)); context.RegisterStats(stats);
            var mover = new Mover(context, stats, rb, col);
            caster.Act = new ActBridge(mover, new Attacker(context, caster.transform, new Dictionary<SkillSlot, SkillBinding>(), collector));
            // Instantiate는 관리 참조 그래프를 복제합니다. 런타임 실행에 원본 저장 객체를 쓰지 않습니다.
            var data = Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/ObjectInstances/SkillData/" + skill + ".asset"));
            var root = (NewMechanism)data.mechanismData.mechanism;
            try
            {
                Check(root.Targeting.Version == 1 && root.Targeting.Relations == TargetRelationMask.Enemy, label + " saved root is relative Enemy");
                collector.EnqueueCommand(new SkillCommand(caster.transform, data.mechanismData.mode, default, root,
                    new DamageData(DamageType.Normal, 100), SkillTarget.ForEntity(target)));
                Physics2D.SyncTransforms();
                for (var i = 0; i < 100; i++)
                {
                    GameTime.Ticker.Step(); caster.Act.Tick(GameTime.Ticker.TickCount);
                    Physics2D.Simulate(.02f); mover.AfterDashPhysics();
                    foreach (var projectile in FindObjectsByType<ProjectileEntity>(FindObjectsSortMode.None)) projectile.AfterPhysics();
                }
                Check(target.HitCount > 0 && target.LastDamage.Attacker == caster.transform && target.LastDamage.UseRelationPolicy,
                    label + " actual asset reaches opponent damage with original Caster");
                Check(caster.HitCount == 0, label + " excludes Self");
                if (root is ProjectileMechanism projectileRoot)
                {
                    var completed = projectileRoot.onCompleted[0].mechanism;
                    var interrupted = projectileRoot.onInterrupted[0].mechanism;
                    var completedCommands = commands.Where(c => ReferenceEquals(c.Mech, completed)).ToArray();
                    Check(completedCommands.Length == 1 && completedCommands[0].Exit?.Cause == ExitCause.TargetHit,
                        label + " actual onCompleted Area once with TargetHit");
                    Check(!commands.Any(c => ReferenceEquals(c.Mech, interrupted)), label + " no onInterrupted branch");
                    Check(completedCommands[0].Target.Kind == TargetKind.Position, label + " ExitPosition inheritance retained");
                }
                if (root is DashMechanism)
                    Check(mover.LastDashResult?.Cause == ExitCause.TargetHit, label + " Dash target hit");
            }
            finally
            {
                foreach (var projectile in FindObjectsByType<ProjectileEntity>(FindObjectsSortMode.None)) DestroyImmediate(projectile.gameObject);
                foreach (var area in FindObjectsByType<AreaEntity>(FindObjectsSortMode.None)) DestroyImmediate(area.gameObject);
                for (var i = objects.Count - 1; i >= 0; i--) if (objects[i]) DestroyImmediate(objects[i]);
                DestroyImmediate(data);
            }
        }
    }
}
#endif
