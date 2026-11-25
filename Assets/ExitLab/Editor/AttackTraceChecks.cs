using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Interaction;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Combat.Stats;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using global::NoWebGame.Entities.Relations;
using global::NoWebGame.Movement.Travel;
using System;
using System.Collections.Generic;
using System.IO;





using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExitLab
{
    public static class AttackTraceChecks
    {
        [MenuItem("Tools/ExitLab/Check Attack Trace")]
        public static void Run()
        {
            var lines = new List<string>();
            var checks = new List<string>();
            var enabled = AttackLog.Enabled; var detail = AttackLog.Detail; var filter = AttackLog.CasterFilter;
            var go = new GameObject("TraceCaster");
            void Capture(string text, string stack, LogType type) { if (text.StartsWith("[AttackTrace]")) lines.Add(text); }
            void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks.Add("PASS " + name); }
            Application.logMessageReceived += Capture;
            try
            {
                AttackLog.Enabled = true; AttackLog.Detail = false; AttackLog.CasterFilter = 0;
                var root = AttackTrace.Root(go.transform);
                var first = root.Command(); var effect = first.Effect(); var child = effect.Command();
                Check(first.AttackId == child.AttackId && child.ParentCommandId == first.CommandId && child.CommandId != first.CommandId, "chain IDs");
                Check(AttackTrace.Root(go.transform).AttackId != root.AttackId, "independent attacks");
                Check(effect.Damage().DamageId != effect.Damage().DamageId, "independent damage attempts");
                var mech = new DamageMechanism();
                var a = new SkillCommand(go.transform, TargetMode.TowardsSelf, default, mech, default, trace: first);
                var b = new SkillCommand(go.transform, TargetMode.TowardsSelf, default, mech, default, trace: child);
                Check(a.Equals(b) && a.GetHashCode() == b.GetHashCode(), "trace does not change command equality");
                var ctx = new CastContext(mech, go.transform, SkillTarget.At(Vector2.one), default, trace: effect);
                Check(ctx.WithExit(new TravelResult(ExitKind.Completed, ExitCause.PositionReached, Vector2.one)).Trace.EffectId == effect.EffectId, "exit keeps trace");
                AttackLog.Write(first, "Probe", "visible");
                Check(lines.Count == 1 && lines[0].Contains("caster=TraceCaster#"), "identity and output");
                AttackLog.CasterFilter = go.transform.GetInstanceID() + 1;
                AttackLog.Write(first, "Probe", "hidden"); Check(lines.Count == 1, "caster filter");
                AttackLog.CasterFilter = 0; AttackLog.Write(first, "Probe", "hidden", true);
                Check(lines.Count == 1, "detail off");
                AttackLog.Enabled = false; AttackLog.Write(first, "Probe", "hidden"); Check(lines.Count == 1, "disabled");
                AttackLog.Enabled = true;
                var stats = new StatsContainer(new BaseStatsContainer(1000, 0, 0, 0, 100, 0, 0));
                stats.ReduceStat(ReduceType.Health, new DamageData(DamageType.Fixed, 100, 100, attacker: go.transform, trace: effect.Damage()));
                Check(stats.Health == 900 && lines.Exists(x => x.Contains("stage=DamageApplied") && x.Contains("health=1000->900")), "actual HP delta");
                stats.ReduceStat(ReduceType.Health, new DamageData(DamageType.Fixed, 100, 0, trace: effect.Damage()));
                Check(stats.Health == 900 && lines.Exists(x => x.Contains("reason=NonPositiveValue")), "zero damage reason");
                typeof(StatsContainer).GetProperty("Shield").SetValue(stats, 150);
                stats.ReduceStat(ReduceType.Health, new DamageData(DamageType.Fixed, 100, 100, trace: effect.Damage()));
                Check(stats.Health == 900 && stats.Shield == 50 && lines.Exists(x => x.Contains("shield=150->50") && x.Contains("healthDelta=0")), "shield absorbs without HP loss");
                var victim = go.AddComponent<TemporaryAITarget>();
                var context = new global::NoWebGame.Combat.Runtime.Context(victim, go, go.transform, null, null, null, null);
                var bridge = new StatsBridge(context, new BaseStatsContainer(1000, 0, 0, 0, 100, 0, 0));
                var storage = new global::NoWebGame.Combat.Stacks.Runtime.VariableStorage();
                var stack = new global::NoWebGame.Combat.Stacks.Runtime.StackManager(context, storage);
                var policy = new global::NoWebGame.Combat.Interaction.InteractionFilter(victim, bridge, stack);
                policy.FilterDamage(new DamageData(DamageType.Fixed, 100, 100, trace: effect.Damage()));
                Check(bridge.Stats.Health == 1000 && lines.Exists(x => x.Contains("reason=DamagePolicyDenied")), "missing neutral permission denies damage");
                policy.FilterDamage(new DamageData(DamageType.Fixed, 100, 100, attacker: go.transform, trace: effect.Damage()));
                Check(bridge.Stats.Health == 900 && lines.Exists(x => x.Contains("damagePolicyRelation=Self") && x.Contains("allowed=True")), "self policy allows once");
                var request = new ExplicitAttackTarget(null, null, TargetRelationMask.Enemy, () => false);
                Check(request.Validate(go.transform, mech) == "RequestLifetimeEnded", "lifetime reason priority");
                var missing = new SkillCommand(null, TargetMode.TowardsEntity, default, mech, default, trace: first);
                new SkillRunner(null).Activate(missing);
                Check(lines.Exists(x => x.Contains("stage=ExecutionRejected") && x.Contains("MissingCasterOrMechanism")), "runner rejection");
                // 기존 실제 명중/AI/Chain 회귀 검사를 로그 활성 상태로 수행합니다.
                TemporaryEnemyChecks.RunWithRegression();
                Check(lines.Exists(x => x.Contains("stage=Enqueued")), "collector trace observed");
                Check(lines.Exists(x => x.Contains("stage=ExecuteReturned")), "execution trace observed");
                Check(lines.Exists(x => x.Contains("stage=DamageDelivered")), "damage delivery observed");
                File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../attack-trace-tests.txt"), checks);
                File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../attack-trace-sample.log"), lines);
                Debug.Log($"AttackTraceChecks PASS {checks.Count}");
            }
            finally
            {
                Application.logMessageReceived -= Capture;
                AttackLog.Enabled = enabled; AttackLog.Detail = detail; AttackLog.CasterFilter = filter;
                Object.DestroyImmediate(go);
            }
        }
    }
}
