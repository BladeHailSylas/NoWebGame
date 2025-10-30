using System;
using System.Collections.Generic;
using StatsInterfaces;
using EffectInterfaces;

public sealed class PlayerStatsContainer
{
    // ===== Base and Current Stats =====
    public int BaseHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public int Health { get; private set; }
    public int Shield { get; private set; }
    public int SpecialShield { get; private set; }
    public int BaseArmor { get; private set; }
    public int Armor { get; private set; }
    public int BaseHealthRegen { get; private set; }
    public int HealthRegen { get; private set; }
    public int BaseAttackDamage { get; private set; }
    public int AttackDamage { get; private set; }
    public int BaseMana { get; private set; }
    public int MaxMana { get; private set; }
    public int Mana { get; private set; }
    public int BaseManaRegen { get; private set; }
    public int ManaRegen { get; private set; }
    public int BaseSpeed { get; private set; }
    public int Speed { get; private set; }
    public bool IsDead { get; private set; }

    public List<byte> DamageReduction { get; private set; } = new();
    public List<byte> ArmorPenetration { get; private set; } = new();
    public List<short> DamageAmplitudes { get; private set; } = new();
    
    public Dictionary<EffectType, Action<PlayerStatsContainer, BuffData>> applier;
    public Dictionary<EffectType, Action<PlayerStatsContainer, BuffData>> remover;

    // ===== Constructor =====
    public PlayerStatsContainer(BaseStatsContainer baseCon)
    {
        BaseHealth = baseCon.BaseHp;
        BaseHealthRegen = baseCon.BaseHpGen;
        BaseMana = baseCon.BaseMana;
        BaseManaRegen = baseCon.BaseManaGen;
        BaseAttackDamage = baseCon.BaseAttack;
        BaseArmor = baseCon.BaseDefense;
        BaseSpeed = baseCon.BaseSpeed;
        CreateBuffers();
        ResetToBase();
    }

    public void ResetToBase()
    {
        MaxHealth = BaseHealth;
        Health = MaxHealth;
        MaxMana = BaseMana;
        Mana = MaxMana;
        Armor = BaseArmor;
        AttackDamage = BaseAttackDamage;
        Speed = BaseSpeed;
        Shield = 0;
        SpecialShield = 0;
        IsDead = false;
    }
    // ===== Damage / Stat Manipulation =====
    public void ReduceStat(ReduceType stat, int amount, int apRatio = 0, DamageType type = DamageType.Normal)
    {
        if (stat == ReduceType.Mana)
        {
            Mana = Math.Max(0, Mana - amount);
            return;
        }

        ApplyDamage(amount, apRatio, type);
    }

    private void ApplyDamage(int damage, int apRatio = 0, DamageType type = DamageType.Normal)
    {
        if (IsDead || damage <= 0) return;

        // -- Percent damage types --
        switch (type)
        {
            case DamageType.CurrentPercent: damage = Health * damage / 100; break;
            case DamageType.LostPercent: damage = (MaxHealth - Health) * damage / 100; break;
            case DamageType.MaxPercent: damage = MaxHealth * damage / 100; break;
        }

        // -- Armor & reduction --
        if (type != DamageType.Fixed)
            damage = (int)(damage * DamageReductionCalc(Armor, apRatio, TotalDamageReduction()));

        // -- Apply shield layers --
        int remaining = damage;

        if (SpecialShield > 0)
        {
            int used = Math.Min(SpecialShield, remaining);
            SpecialShield -= used;
            remaining -= used;
        }

        if (remaining > 0 && Shield > 0)
        {
            int used = Math.Min(Shield, remaining);
            Shield -= used;
            remaining -= used;
        }

        if (remaining > 0)
        {
            Health = Math.Max(0, Health - remaining);
            if (Health <= 0)
                IsDead = true;
        }
    }

    // ===== Math Helpers =====
    private double DamageReductionCalc(int armor, int apRatio = 0, double damageRatio = 1)
    {
        return (80.0 / (80.0 + armor * (1 - apRatio / 100.0))) * damageRatio;
    }

    public double TotalArmorPenetration()
    {
        double total = 1;
        foreach (var ap in ArmorPenetration)
            total *= (1 - ap / 100.0);
        return 1 - total;
    }

    public double TotalDamageReduction()
    {
        double total = 1.0;
        foreach (var dr in DamageReduction)
            total *= (1 - dr / 100.0);

        // Lower bound to prevent healing from damage
        return (int)Math.Max(0.15, total);
    }

    // ===== Regeneration =====
    public void TickRegen(ushort deltaMs)
    {
        // regen every second-like rate
        Health = Math.Min(MaxHealth, Health + HealthRegen);
        Mana = Math.Min(MaxMana, Mana + ManaRegen);
    }

    public double TotalDamageAmplitude()
    {
        double total = 1;
        foreach (var da in DamageAmplitudes)
        {
            total *= (1 + da);
        }

        return total;
    }

    public bool TryApply(BuffData buff)
    {
        if (applier.TryGetValue(buff.Type, out var modify))
        {
            modify(this, buff);
            return true;
        }

        return false;
    }
    public bool TryRemove(BuffData buff)
    {
        if (remover.TryGetValue(buff.Type, out var modify))
        {
            modify(this, buff);
            return true;
        }

        return false;
    }
    public void CreateBuffers()
    {
        applier = new Dictionary<EffectType, Action<PlayerStatsContainer, BuffData>>
        {
            // 공격력 상승
            [EffectType.DamageBoost] = (self, buff) => {
                int delta = (self.BaseAttackDamage * buff.Value + 50) / 100;
                self.AttackDamage += delta;
            },

            // 방어력 상승
            [EffectType.ArmorBoost] = (self, buff) => {
                int delta = (self.BaseArmor * buff.Value + 50) / 100;
                self.Armor += delta;
            },

            // 방어 관통 (리스트 추가)
            [EffectType.APBoost] = (self, buff) => {
                byte value = (byte)Math.Clamp((int)buff.Value, 0, 100);
                self.ArmorPenetration.Add(value);
            },

            // 피해 감소 (리스트 추가)
            [EffectType.DRBoost] = (self, buff) => {
                byte value = (byte)Math.Clamp((int)buff.Value, 0, 100);
                self.DamageReduction.Add(value);
            },

            // 가속 (Haste)
            [EffectType.Haste] = (self, buff) => {
                int delta = (self.BaseSpeed * buff.Value + 50) / 100;
                self.Speed += delta;
            },

            // 감속 (Slow)
            [EffectType.Slow] = (self, buff) => {
                int delta = (self.BaseSpeed * buff.Value + 50) / 100;
                self.Speed -= delta; // 감속은 감소 방향
            },
        };
        remover = new Dictionary<EffectType, Action<PlayerStatsContainer, BuffData>>
        {
            // 공격력 상승
            [EffectType.DamageBoost] = (self, buff) => {
                int delta = (self.BaseAttackDamage * buff.Value + 50) / 100;
                self.AttackDamage -= delta;
            },

            // 방어력 상승
            [EffectType.ArmorBoost] = (self, buff) => {
                int delta = (self.BaseArmor * buff.Value + 50) / 100;
                self.Armor -= delta;
            },

            // 방어 관통 (리스트 추가)
            [EffectType.APBoost] = (self, buff) => {
                byte value = (byte)Math.Clamp((int)buff.Value, 0, 100);
                self.ArmorPenetration.Remove(value);
            },

            // 피해 감소 (리스트 추가)
            [EffectType.DRBoost] = (self, buff) => {
                byte value = (byte)Math.Clamp((int)buff.Value, 0, 100);
                self.DamageReduction.Remove(value);
            },

            // 가속 (Haste)
            [EffectType.Haste] = (self, buff) => {
                int delta = (self.BaseSpeed * buff.Value + 50) / 100;
                self.Speed -= delta;
            },

            // 감속 (Slow)
            [EffectType.Slow] = (self, buff) => {
                int delta = (self.BaseSpeed * buff.Value + 50) / 100;
                self.Speed += delta; // 감속은 감소 방향
            },
        };
    }
}

public readonly struct BuffData
{
    public readonly string Name;
    public readonly EffectType Type;
    public readonly byte Value;

    public BuffData(string name, EffectType type, byte value)
    {
        Name = name;
        Type = type;
        Value = value;
    }
}