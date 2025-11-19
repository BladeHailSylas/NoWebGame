using Moves;
using PlayerScripts.Core;
using Systems.Data;

namespace PlayerScripts.Stats
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
        }

        public void Tick(ushort deltaMs)
        {
            Stats.TickRegen(deltaMs);
        }
        public double AP() => Stats.TotalArmorPenetration();
        public double DR() => Stats.TotalDamageReduction();
        public double DA() => Stats.TotalDamageAmplitude();
        public DamageData DamageData() => new(DamageType.Normal, Stats.AttackDamage, 1, AP(), DA());
        public void ApplyManaCost(int amount)
        {
            Stats.ReduceStat(ReduceType.Mana, amount);
        }
        public void TakeDamage(DamageData data)
        {
            Stats.ReduceStat(ReduceType.Health, data);
        }
        public void ResetStats()
        {
            Stats.ResetToBase();
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