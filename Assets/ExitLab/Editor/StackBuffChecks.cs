using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PlayerScripts.Core;
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

namespace ExitLab
{
    /// <summary>S2 총량·소유권·실패 보상 검사. 반올림 경계와 비율형 효과를 별도로 검증합니다.</summary>
    [InitializeOnLoad]
    public static class StackBuffChecks
    {
        private static readonly List<string> Results = new();
        private static readonly List<string> Errors = new();
        static StackBuffChecks()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("StackS2.Run", false)) return;
                SessionState.SetBool("StackS2.Run", false);
                try { Run(); StackTransitionChecks.Run(); StackBaselineChecks.Run(); EditorApplication.Exit(0); }
                catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            };
        }
        public static void RunPlayMode()
        {
            Guard(); SessionState.SetBool("StackS2.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.EnterPlaymode();
        }
        private static void Guard()
        { if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker")) throw new Exception("Validation copy only"); }
        private static void Check(bool value, string name)
        { if (!value) throw new Exception(name); Results.Add("PASS " + name); }
        private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-12;
        private static void Throws<T>(Action action, string name) where T : Exception
        { try { action(); } catch (T) { Check(true, name); return; } throw new Exception("Missing exception: " + name); }
        private static object Get(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static object Call(object obj, string name, params object[] args)
        {
            try { return obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) Errors.Add(message); }
        private sealed class Fixture : IDisposable
        {
            private readonly List<Object> _owned = new();
            public readonly DelayScheduler Scheduler = new();
            public readonly StatsBridge Bridge;
            public StatsContainer Stats => Bridge.Stats;
            public readonly StackManager Manager;
            public Fixture(int basis = 5)
            {
                var go = new GameObject("S2 actor"); _owned.Add(go); var owner = go.AddComponent<TemporaryAITarget>();
                var ctx = new Context(owner, go, go.transform, null, null, null, null); ctx.RegisterScheduler(Scheduler);
                Bridge = new StatsBridge(ctx, new BaseStatsContainer(100, 0, 100, 0, basis, basis, basis)); ctx.RegisterStats(Bridge);
                Manager = new StackManager(ctx, new VariableStorage());
            }
            public BuffDefinition Buff(EffectType type, byte value = 10)
            {
                var def = ScriptableObject.CreateInstance<BuffDefinition>(); _owned.Add(def);
                def.Type = type; def.Value = value; def.maxStacks = 10; def.duration = 5; return def;
            }
            public Dictionary<StackKey, StackStatus> States => (Dictionary<StackKey, StackStatus>)Get(Manager, "_stackStorage");
            public object Handle(StackKey key) => ((System.Collections.IDictionary)Get(Manager, "_buffContributions"))[key];
            public int Count
            {
                get { var entries = Get(Stats, "_buffContributions"); return (int)entries.GetType().GetProperty("Count").GetValue(entries); }
            }
            public double Factor(EffectType type) => (double)Call(Stats, "OwnedPercentageFactor", type);
            public void Dispose() { foreach (var obj in _owned) if (obj) Object.DestroyImmediate(obj); }
        }
        public static void Run()
        {
            Guard(); Results.Clear(); Errors.Clear(); var old = DelayScheduler.Instance;
            Application.logMessageReceived += Observe;
            try
            {
                Scalars(); Percentages(); OwnershipAndReset(); FailuresAndRollback();
                Check(Errors.Count == 0, "no unexpected Unity errors: " + string.Join(";", Errors));
                Debug.Log("StackBuffChecks PASS " + Results.Count);
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                DelayScheduler.Instance = old; Application.logMessageReceived -= Observe;
                File.WriteAllLines(Path.GetFullPath(Path.Combine(Application.dataPath, "../../stack-s2-results.txt")), Results);
            }
        }
        private static int Measure(StatsContainer stats, EffectType type) => type switch
        { EffectType.DamageBoost => stats.AttackDamage, EffectType.ArmorBoost => stats.Armor, _ => stats.Speed };
        private static void Scalars()
        {
            foreach (var type in new[] { EffectType.DamageBoost, EffectType.ArmorBoost, EffectType.Haste, EffectType.Slow })
            {
                using var split = new Fixture(); using var batch = new Fixture(); var def = split.Buff(type); var key = new StackKey(def);
                var sign = type == EffectType.Slow ? -1 : 1;
                split.Manager.ApplyStack(key, 1, 0); var handle = split.Handle(key);
                split.Manager.ApplyStack(key, 1, 1); batch.Manager.ApplyStack(key, 2, 1);
                Check(Measure(split.Stats, type) == 5 + sign && Measure(batch.Stats, type) == 5 + sign, type + " split +1,+1 equals batch +2 at rounding boundary");
                Check(ReferenceEquals(handle, split.Handle(key)) && split.Count == 1, type + " update retains one owned contribution");
                split.Manager.ApplyStack(key, 100, 2); var capped = Measure(split.Stats, type);
                split.Manager.ApplyStack(key, 100, 3);
                Check(capped == 5 + sign * 5 && Measure(split.Stats, type) == capped && split.States[key].Amount == 10, type + " cap refresh adds no effect");
                split.Manager.ApplyStack(key, -7, 4);
                Check(Measure(split.Stats, type) == 5 + sign * 2 && split.States[key].Amount == 3, type + " decrease recalculates total contribution once");
                split.Manager.ApplyStack(key, 0, 5); split.Manager.Tick(9);
                Check(split.States.ContainsKey(key), type + " zero delta refreshes duration without extra contribution");
                split.Manager.Tick(10); split.Manager.Tick(11);
                Check(Measure(split.Stats, type) == 5 && split.Count == 0 && !split.States.ContainsKey(key), type + " expiry returns exactly to base once");
                split.Manager.ApplyStack(key, 0, 12);
                Check(split.Count == 0 && !split.States.ContainsKey(key), type + " zero amount on absent key has no effect");
                split.Manager.ApplyStack(key, 3, 12); split.Manager.ApplyStack(key, int.MinValue, 13);
                Check(Measure(split.Stats, type) == 5 && split.Count == 0, type + " negative-to-zero releases recorded contribution");
                batch.Manager.Tick(6); Check(Measure(batch.Stats, type) == 5, type + " batch also expires without residual");
            }
        }
        private static void Percentages()
        {
            foreach (var type in new[] { EffectType.APBoost, EffectType.DRBoost })
            {
                using var f = new Fixture(); using var batch = new Fixture(); var def = f.Buff(type); var a = new StackKey(def, "A"); var b = new StackKey(def, "B");
                f.Manager.ApplyStack(a, 1, 0); f.Manager.ApplyStack(a, 1, 0); batch.Manager.ApplyStack(a, 2, 0);
                Check(Near(f.Factor(type), 0.8) && Near(batch.Factor(type), 0.8), type + " same Key has aggregate 20 percent independent of batch");
                f.Manager.ApplyStack(b, 1, 1);
                Check(Near(f.Factor(type), 0.72) && f.Count == 2, type + " different Keys combine multiplicatively");
                f.Manager.ApplyStack(a, -1, 2);
                Check(Near(f.Factor(type), 0.81), type + " decrease keeps other equal-valued Key");
                f.Manager.ApplyStack(a, -1, 3);
                Check(Near(f.Factor(type), 0.9) && f.Count == 1, type + " exact owner removal preserves other 10 percent");
                f.Stats.TryApply(new BuffData(type, 10));
                var legacy = type == EffectType.APBoost ? f.Stats.ArmorPenetration : f.Stats.DamageReduction;
                Check(legacy.Count == 1 && Near(f.Factor(type), 0.9), type + " legacy and owned storage are separate");
                if (type == EffectType.APBoost) Check(Near(f.Stats.TotalArmorPenetration(), 0.19), "public AP includes legacy and owned exactly once");
                f.Manager.ApplyStack(a, 10, 4);
                Check(f.Factor(type) == 0, type + " percentage clamps at 100");
                f.Manager.ApplyStack(a, int.MaxValue, 5); f.Manager.ApplyStack(a, -10, 6);
                Check(Near(f.Factor(type), 0.9) && legacy.Count == 1, type + " removing 100 percent uses remaining items, no divide by zero");
                f.Manager.Tick(6);
                Check(f.Factor(type) == 1 && f.Count == 0 && legacy.Count == 1, type + " expiration leaves legacy contribution intact");
                f.Stats.TryRemove(new BuffData(type, 10));
                Check(legacy.Count == 0 && (type == EffectType.APBoost ? f.Stats.TotalArmorPenetration() == 0 : f.Stats.TotalDamageReduction() == 1), type + " legacy removal after owned expiry has no residue");
            }
        }
        private static void OwnershipAndReset()
        {
            using var f = new Fixture(100); var haste = f.Buff(EffectType.Haste); var slow = f.Buff(EffectType.Slow);
            var a = new StackKey(haste); var b = new StackKey(slow);
            f.Manager.ApplyStack(a, 3, 0); f.Manager.ApplyStack(b, 1, 0);
            Check(f.Stats.Speed == 120, "Haste and Slow retain independent signed contributions");
            var attack = f.Buff(EffectType.DamageBoost); var k = new StackKey(attack);
            f.Manager.ApplyStack(k, 2, 0); var oldHandle = f.Handle(k);
            f.Stats.TryApply(new BuffData(EffectType.DamageBoost, 10));
            f.Manager.ApplyStack(k, 1, 1);
            Check(f.Stats.AttackDamage == 140, "owned update preserves unrelated legacy scalar delta");
            f.Stats.TryRemove(new BuffData(EffectType.DamageBoost, 10));
            var ap = f.Buff(EffectType.APBoost); var apKey = new StackKey(ap); f.Manager.ApplyStack(apKey, 2, 0);
            var remaining = f.Scheduler.GetRemaining(f.States[k].DelayId, 1);
            f.Stats.TrySpendMana(20); f.Bridge.ResetStats();
            Check(f.Stats.AttackDamage == 130 && f.Stats.Speed == 120 && Near(f.Stats.TotalArmorPenetration(), 0.2), "ResetStats recomposes active owned scalar and percentage contributions");
            Check(f.Stats.Mana == 100 && f.Scheduler.GetRemaining(f.States[k].DelayId, 1) == remaining, "ResetStats preserves existing resource reset and does not refresh Stack lifetime");
            attack.Type = EffectType.ArmorBoost; attack.Value = 90;
            f.Manager.Tick(6);
            Check(f.Stats.AttackDamage == 100 && f.Stats.Armor == 100 && f.Stats.Speed == 100 && f.Count == 0, "expiry uses actual recorded contribution despite definition mutation");
            f.Manager.ApplyStack(k, 1, 7);
            Check(!ReferenceEquals(oldHandle, f.Handle(k)) && f.Stats.Armor == 190, "new cycle has a fresh contribution");
            Throws<InvalidOperationException>(() => Call(f.Stats, "PrepareBuffChange", oldHandle, default(EffectType), (byte)0, 0), "stale handle cannot remove new cycle");
            using var other = new Fixture();
            Throws<InvalidOperationException>(() => Call(other.Stats, "PrepareBuffChange", f.Handle(k), default(EffectType), (byte)0, 0), "wrong owner cannot remove contribution");
        }
        private static void FailuresAndRollback()
        {
            using var f = new Fixture(100); var def = f.Buff(EffectType.DamageBoost); var key = new StackKey(def);
            f.Manager.ApplyStack(key, 1, 0); var before = f.States[key]; var handle = f.Handle(key); f.Scheduler.ModifyDelta(before.DelayId, -2);
            Throws<OverflowException>(() => f.Manager.ApplyStack(key, 1, long.MaxValue), "reservation overflow rejects Buff refresh");
            Check(f.Stats.AttackDamage == 110 && f.States[key].Amount == 1 && ReferenceEquals(handle, f.Handle(key)) && f.States[key].DelayId.Equals(before.DelayId) && f.Scheduler.GetRemaining(before.DelayId, 0) == 7, "failed scheduling preserves exact contribution, amount, reservation and delta");
            def.maxStacks = int.MaxValue; def.Value = 255;
            Throws<OverflowException>(() => f.Manager.ApplyStack(key, int.MaxValue, 1), "unrepresentable contribution rejected before mutation");
            Check(f.Stats.AttackDamage == 110 && f.Count == 1 && f.Scheduler.GetAllEntries().Count == 1, "calculation failure leaves state unchanged");
            def.Type = EffectType.Stunned;
            Throws<ArgumentException>(() => f.Manager.ApplyStack(key, 1, 1), "unsupported Buff type rejects update");
            f.Manager.Tick(7);
            Check(f.Stats.AttackDamage == 100 && f.Count == 0, "failed updates do not prevent previous expiry");
            using var huge = new Fixture(int.MaxValue); var hd = huge.Buff(EffectType.DamageBoost, 1); var hk = new StackKey(hd);
            Throws<OverflowException>(() => huge.Manager.ApplyStack(hk, 1, 0), "final Stat overflow rejected even when contribution fits int");
            Check(huge.Stats.AttackDamage == int.MaxValue && huge.Count == 0 && huge.States.Count == 0 && huge.Scheduler.GetAllEntries().Count == 0, "failed first apply leaves no owned effect or reservation");
            def.Type = EffectType.DamageBoost; def.Value = 10; f.Manager.ApplyStack(key, 1, 10);
            var change = Call(f.Stats, "PrepareBuffChange", f.Handle(key), EffectType.DamageBoost, (byte)10, 3);
            Call(change, "Commit"); Check(f.Stats.AttackDamage == 130, "prepared update applies exact total");
            Call(change, "Rollback"); Call(change, "Rollback");
            Check(f.Stats.AttackDamage == 110 && f.Count == 1, "rollback restores previous contribution exactly and is idempotent");
            var stale = Call(f.Stats, "PrepareBuffChange", f.Handle(key), EffectType.DamageBoost, (byte)10, 3);
            f.Stats.TryApply(new BuffData(EffectType.DamageBoost, 10));
            Throws<InvalidOperationException>(() => Call(stale, "Commit"), "intervening scalar update rejects stale prepared change");
            Call(stale, "Rollback");
            Check(f.Stats.AttackDamage == 120, "rejected prepared change cannot erase newer external value");
            f.Stats.TryRemove(new BuffData(EffectType.DamageBoost, 10));
            // 같은 Key의 효과 종류가 바뀌어도 기존 기여 회수와 새 기여 반영을 함께 확정합니다.
            def.Type = EffectType.APBoost; f.Manager.ApplyStack(key, 1, 11);
            Check(f.Stats.AttackDamage == 100 && Near(f.Stats.TotalArmorPenetration(), 0.2) && f.Count == 1, "type change replaces old scalar with owned percentage atomically");
            f.Manager.ApplyStack(key, -2, 12); f.Manager.Tick(20);
            Check(f.Stats.AttackDamage == 100 && f.Stats.TotalArmorPenetration() == 0 && f.Count == 0, "explicit zero and later expiry cannot double-release");
        }
    }
}

