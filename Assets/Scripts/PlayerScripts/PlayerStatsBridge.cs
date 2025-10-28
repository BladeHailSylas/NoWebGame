using UnityEngine;
using StatsInterfaces;

public class PlayerStatsBridge : MonoBehaviour
{
    public PlayerStatsContainer Stats { get; private set; }

    private void Awake()
    {
        Stats = new PlayerStatsContainer(GetComponent<InputBinder>().BaseStats);
        Ticker.Instance.OnTick += OnTick;
    }

    private void OnDestroy()
    {
        if (Ticker.Instance != null)
            Ticker.Instance.OnTick -= OnTick;
    }

    private void OnTick(ushort deltaMs)
    {
        Stats.TickRegen(deltaMs);
    }

    // === Unity/Command interface ===
    public void ApplyDamage(int amount, int apRatio = 0, DamageType type = DamageType.Normal)
    {
        Stats.ReduceStat(ReduceType.Health, amount, apRatio, type);
        Debug.Log($"{gameObject.name} took {amount} damage, HP: {Stats.Health}/{Stats.MaxHealth}");
    }

    public void ApplyManaCost(int amount)
    {
        Stats.ReduceStat(ReduceType.Mana, amount);
    }

    public void Heal(int amount)
    {
        Debug.Log("Not heal");
        //Stats.Heal(amount);
    }

    public void ResetStats()
    {
        Stats.ResetToBase();
    }
    public void ReduceStat(ReduceType stat, int amount, int apRatio = 0, DamageType type = DamageType.Normal)
    {
        Stats.ReduceStat(stat, amount, apRatio, type);
    }
}