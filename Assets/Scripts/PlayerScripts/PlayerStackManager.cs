using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class PlayerStackManager
{
    public readonly Dictionary<StackKey, StackStatus> StackStorage = new();
    public readonly Dictionary<ushort, List<StackKey>> ExpirableStacks = new();
    public void ApplyStack(StackKey stackKey, int amount, ushort tick, ushort duration = 0)
    {
        var endAt = endTick(tick, duration == 0 ? stackKey.def.defaultDuration : duration);
        var total = totalStack(stackKey.def.maxStacks, amount);
        AddExpiration(stackKey, endAt);
        if (StackStorage.ContainsKey(stackKey))
        {
            //total = Math.Min(total + CurrentStacks[stackKey].amount, stackKey.def.maxStacks);
            total = totalStack(stackKey.def.maxStacks, total, StackStorage[stackKey].Amount);
            StackStorage[stackKey] = new StackStatus(total, tick, endAt);
        }
        else
        {
            StackStorage.Add(stackKey, new StackStatus(total, tick, endAt));
        }
    }

    public void CacheStack(ushort tick)
    {
        if (!ExpirableStacks.TryGetValue(tick, out var list)) return;
        foreach (var key in list)
        {
            ExpirableStacks[tick].Remove(key);
            StackStorage[key] = new StackStatus(0, tick, 0);
        }
    }

    public void AddExpiration(StackKey key, ushort expireTick)
    {
        if (expireTick == 65535) return;
        // 1️⃣ 기존 만료 tick이 존재하는가?
        if (StackStorage.TryGetValue(key, out var status))
        {
            ushort oldExpireTick = status.ExpireAt;

            if (ExpirableStacks.TryGetValue(oldExpireTick, out var oldList))
            {
                oldList.Remove(key);
                if (oldList.Count == 0)
                    ExpirableStacks.Remove(oldExpireTick);
            }
        }

        // 2️⃣ 새 tick으로 재등록
        if (!ExpirableStacks.TryGetValue(expireTick, out var list))
            ExpirableStacks[expireTick] = list = new List<StackKey>();

        list.Add(key);
    }

    #region ===== Utils =====
    public ushort endTick(ushort tick, ushort duration)
    {
        if (duration == 65535 || tick + duration < tick) //tick + duration < tick => overflow
        {
            return 65535;
        }

        return (ushort)(tick + duration);
    }

    public int totalStack(int max, params int[] applies)
    {
        int total = 0;
        foreach (var n in applies)
        {
            total += n;
        }
        return Math.Min(total, max);
    }
    #endregion
}