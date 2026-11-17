#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>실제 Runner→Projectile/Dash→종료 Chain을 Play Mode에서 검증합니다.</summary>
    public sealed class RelativeTargetPlayModeRunner : MonoBehaviour
    {
        private readonly List<string> _results = new();
        private readonly List<string> _errors = new();
        private sealed class Record : NewMechanism, INewMechanism
        {
            public int Count;
            public CastContext Last;
            protected override void ExecuteCore(CastContext context) { Count++; Last = context; }
        }
        private void Check(bool value, string name)
        { if (!value) throw new Exception(name); _results.Add("PASS " + name); }
        private void Capture(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(message); }
        private IEnumerator Start()
        {
            Application.logMessageReceived += Capture;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.queriesHitTriggers = true;
            Physics2D.queriesStartInColliders = true;
            var success = false;
            try
            {
                foreach (var relative in new[] { false, true })
                foreach (var dash in new[] { false, true }) RunCase(relative, dash);
                Check(_errors.Count == 0, "no runtime errors: " + string.Join(" | ", _errors));
                success = true;
            }
            catch (Exception error) { _results.Add("FAIL " + error); }
            File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../target-stage2-playmode-tests.txt"), _results);
            Debug.Log($"RelativeTargetPlayMode {(success ? "PASS" : "FAIL")} {_results.Count}");
            Application.logMessageReceived -= Capture;
            yield return null;
            EditorApplication.Exit(success ? 0 : 1);
        }
        private void RunCase(bool relative, bool dash)
        {
            var label = $"relative={relative} dash={dash}";
            var owned = new List<GameObject>();
            GameObject Go(string name, Vector2 position)
            { var go = new GameObject(name); go.transform.position = position; owned.Add(go); return go; }
            GameTime.Ticker = new Ticker(); GameTime.DelayScheduler = new DelayScheduler();
            var services = Go("services", new Vector2(20, 20)); services.AddComponent<TargetResolver>();
            var collector = services.AddComponent<CommandCollector>();
            var casterObject = Go("Enemy caster", Vector2.zero); casterObject.layer = LayerMask.NameToLayer("Foe");
            var caster = casterObject.AddComponent<RelativeTargetTestEntity>();
            var rb = casterObject.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
            var col = casterObject.AddComponent<CircleCollider2D>(); col.radius = .1f; col.isTrigger = true;
            var targetObject = Go("Player target", new Vector2(3, 0)); targetObject.layer = LayerMask.NameToLayer("You");
            var target = targetObject.AddComponent<RelativeTargetTestEntity>();
            var child = Go("Default child collider", new Vector2(3, 0)); child.transform.SetParent(target.transform);
            child.AddComponent<CircleCollider2D>().radius = .2f; child.GetComponent<Collider2D>().isTrigger = true;
            var context = new Context(caster, casterObject, caster.transform, services.GetComponent<TargetResolver>(), collector, null, new PlayerScripts.Core.Logger("RelativeTest"));
            context.RegisterScheduler(GameTime.DelayScheduler);
            var stats = new StatsBridge(context, new BaseStatsContainer(1000, 0, 0, 0, 100, 0, 1000)); context.RegisterStats(stats);
            var mover = new Mover(context, stats, rb, col);
            var attacker = new Attacker(context, caster.transform, new Dictionary<SkillSlot, SkillBinding>(), collector);
            caster.Act = new ActBridge(mover, attacker);
            var completed = new Record(); var interrupted = new Record();
            var damage = new DamageMechanism { damageValue = 100 }; damage.ConfigureRelativeTargets(TargetRelationMask.Enemy);
            List<MechanismData> Chain(INewMechanism m) => new() { new MechanismData { mechanism = m, mode = TargetMode.TowardsEntity } };
            NewMechanism root;
            if (dash)
                root = new DashMechanism { speed = 60, durationTicks = 50, callbackMode = ExitCallbackMode.Classified,
                    onHit = Chain(damage), onCompleted = Chain(completed), onInterrupted = Chain(interrupted), onExpire = new() };
            else
            {
                var template = Go("Projectile template", new Vector2(30, 30));
                template.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                template.AddComponent<CircleCollider2D>().radius = .1f; template.GetComponent<Collider2D>().isTrigger = true;
                root = new ProjectileMechanism { speed = 60, lifeTick = 50, projectilePrefab = template.AddComponent<ProjectileEntity>(),
                    callbackMode = ExitCallbackMode.Classified, onHit = Chain(damage), onCompleted = Chain(completed), onInterrupted = Chain(interrupted), onExpire = new() };
            }
            if (relative) root.ConfigureRelativeTargets(TargetRelationMask.Enemy);
            collector.EnqueueCommand(new SkillCommand(caster.transform, TargetMode.TowardsEntity, default, root,
                new DamageData(DamageType.Normal, 100), SkillTarget.ForEntity(target), masker: 128));
            try
            {
                Physics2D.SyncTransforms();
                for (var tick = 0; tick < 12; tick++)
                {
                    GameTime.Ticker.Step(); caster.Act.Tick(GameTime.Ticker.TickCount);
                    Physics2D.Simulate(.02f); mover.AfterDashPhysics();
                    foreach (var projectile in FindObjectsByType<ProjectileEntity>(FindObjectsSortMode.None)) projectile.AfterPhysics();
                }
                Check(target.HitCount == (relative ? 1 : 0), label + " actual onHit DamageMechanism count");
                Check(completed.Count == (relative ? 1 : 0) && interrupted.Count == (relative ? 0 : 1), label + " classified callback exactly once");
                var exit = relative ? completed.Last.Exit : interrupted.Last.Exit;
                Check(exit?.Cause == (relative ? ExitCause.TargetHit : ExitCause.TargetNotHit), label + " exact exit cause");
                if (relative) Check(target.LastDamage.Attacker == caster.transform && target.LastDamage.UseRelationPolicy, label + " original Caster and new damage policy");
            }
            finally
            {
                foreach (var projectile in FindObjectsByType<ProjectileEntity>(FindObjectsSortMode.None)) DestroyImmediate(projectile.gameObject);
                for (var i = owned.Count - 1; i >= 0; i--) if (owned[i]) DestroyImmediate(owned[i]);
            }
        }
    }
}
#endif
