using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Diagnostics.Runtime;



using UnityEngine;

namespace NoWebGame.Combat.Stats
{
    /// <summary>
    /// Maintains the player's runtime statistics and exposes helper methods for
    /// other modules. This bridge keeps the heavy lifting out of MonoBehaviours,
    /// improving testability.
    /// </summary>
    public sealed class StatsBridge
    {
        public StatsContainer Stats { get; }
        private readonly Context _context;

        public StatsBridge(Context context, BaseStatsContainer baseStats)
        {
            _context = context;
            Stats = new StatsContainer(baseStats);
            Stats.Died += () => _context.Attacker?.CancelPending("CasterDead");
        }

        public void Tick(long tick)
        {
            Stats.TickRegen(tick);
        }
        public double AP => Stats.TotalArmorPenetration();
        public double DR => Stats.TotalDamageReduction();
        public double DA => Stats.TotalDamageAmplitude();
        public bool IsDead => Stats.IsDead;
        public bool CanSpendMana(int amount) => Stats.CanSpendMana(amount);
        public bool TrySpendMana(int amount) => Stats.TrySpendMana(amount);
        public DamageData DamageData() => new(DamageType.Normal, Stats.AttackDamage, 1, AP, DA);
        public void ApplyManaCost(int amount)
        {
            Stats.ReduceStat(ReduceType.Mana, amount);
        }
        public void TakeDamage(DamageData data)
        {
            AttackLog.Write(data.Trace, "StatsReceived", $"health={Stats.Health} shield={Stats.Shield} specialShield={Stats.SpecialShield}");
            _context.SpriteManager?.Flash();
            Stats.ReduceStat(ReduceType.Health, data);
        }
        public void ResetStats()
        {
            Stats.ResetToBase();
        }

        public float GetSpeed()
        {
            return Stats.Speed / 1000f;
        }
        private void ReduceStat(ReduceType stat, int amount, int apRatio = 0, DamageType type = DamageType.Normal)
        {
            Stats.ReduceStat(stat, amount, apRatio, type);
        }

        public void TryApply(BuffData data)
        {
            //Debug.Log($"I am applying {data.Name}({data.Type}, {data.Value}%)");
            Stats.TryApply(data);
        }

        // Stack 전용 경로는 성공/복구 가능한 기여 기록을 그대로 전달합니다.
        internal global::NoWebGame.Combat.Stats.StatsContainer.BuffChange PrepareBuffChange(global::NoWebGame.Combat.Stats.StatsContainer.BuffContribution previous,
            EffectType type, byte value, int amount) => Stats.PrepareBuffChange(previous, type, value, amount);

        public void TryRemove(BuffData data)
        {
            //Debug.Log($"I am removing {data.Name}({data.Type}, {data.Value}%)");
            Stats.TryRemove(data);
        }
    }

    public readonly struct BaseStatsContainer
    {
        public readonly int BaseHp;
        public readonly int BaseHpGen;
        public readonly int BaseMana;
        public readonly int BaseManaGen;
        public readonly int BaseAttack;
        public readonly int BaseDefense;
        public readonly int BaseSpeed;

        public BaseStatsContainer(int bhp, int hpg, int bmp, int mpg, int bad, int bar, int bsp)
        {
            BaseHp = bhp;
            BaseHpGen = hpg;
            BaseMana = bmp;
            BaseManaGen = mpg;
            BaseAttack = bad;
            BaseDefense = bar;
            BaseSpeed = bsp;
        }
    }
}
