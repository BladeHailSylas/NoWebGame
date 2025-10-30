using StatsInterfaces;

/// <summary>
/// Maintains the player's runtime statistics and exposes helper methods for
/// other modules. This bridge keeps the heavy lifting out of MonoBehaviours,
/// improving testability.
/// </summary>
public sealed class PlayerStatsBridge
{
    public PlayerStatsContainer Stats { get; }
    private readonly PlayerContext _context;

    public PlayerStatsBridge(PlayerContext context, BaseStatsContainer baseStats)
    {
        _context = context;
        Stats = new PlayerStatsContainer(baseStats);
    }

    public void Tick(ushort deltaMs)
    {
        Stats.TickRegen(deltaMs);
    }

    public void ApplyDamage(int amount, int apRatio = 0, DamageType type = DamageType.Normal)
    {
        Stats.ReduceStat(ReduceType.Health, amount, apRatio, type);
        _context.Logger.Info($"Damage applied: {amount}, HP {Stats.Health}/{Stats.MaxHealth}.");
    }

    public double AP() => Stats.TotalArmorPenetration();
    public double DR() => Stats.TotalDamageReduction();
    public double DA() => Stats.TotalDamageAmplitude();
    public DamageData DamageData() => new(DamageType.Normal, Stats.AttackDamage, 1, AP(), DA());
    public void ApplyManaCost(int amount)
    {
        Stats.ReduceStat(ReduceType.Mana, amount);
    }

    public void ResetStats()
    {
        Stats.ResetToBase();
    }

    public void ReduceStat(ReduceType stat, int amount, int apRatio = 0, DamageType type = DamageType.Normal)
    {
        Stats.ReduceStat(stat, amount, apRatio, type);
    }

    public void TryApply(BuffData data)
    {
        Stats.TryApply(data);
    }
    public void TryRemove(BuffData data)
    {
        Stats.TryRemove(data);
    }
}
