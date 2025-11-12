using System;
using System.Collections.Generic;

public class PlayerStackManager
{
    private ushort _lastTick;
    private readonly PlayerContext _context;
    public PlayerStackManager(PlayerContext ctx)
    {
        _context = ctx;
    }

    private readonly Dictionary<StackKey, StackStatus> _stackStorage = new();
    private Dictionary<ushort, List<StackKey>> _expirable = new();

    public void Tick(ushort tick)
    {
        if (_lastTick < tick)
        {
            for (ushort t = (ushort)(_lastTick + 1); t <= tick; t++)
                CacheStack(t);
        }
        else // overflow 발생
        {
            for (ushort t = (ushort)(_lastTick + 1); t != 0; t++)
                CacheStack(t);
            for (ushort t = 0; t <= tick; t++)
                CacheStack(t);
        }
        _lastTick = tick;
    }
    
    #region ===== Apply =====
    public void ApplyStack(StackKey stackKey, int amount, ushort tick, ushort duration = 0)
    {
        var endAt = EndTick(tick, duration == 0 ? stackKey.def.duration : duration);
        var total = TotalStack(stackKey.def.maxStacks, amount);
        AddExpiration(stackKey, endAt);
        if (_stackStorage.ContainsKey(stackKey))
        {
            //total = Math.Min(total + CurrentStacks[stackKey].amount, stackKey.def.maxStacks);
            total = TotalStack(stackKey.def.maxStacks, total, _stackStorage[stackKey].Amount);
            _stackStorage[stackKey] = new StackStatus(total, tick, endAt);
        }
        else
        {
            _stackStorage.Add(stackKey, new StackStatus(total, tick, endAt));
        }
        ResolveApply(stackKey, amount);
    }
    
    private void AddExpiration(StackKey key, ushort expireTick)
    {
        if (expireTick == 65535) return;
        // 1️⃣ 기존 만료 tick이 존재하는가?
        if (_stackStorage.TryGetValue(key, out var status))
        {
            ushort oldExpireTick = status.ExpireAt;

            if (_expirable.TryGetValue(oldExpireTick, out var oldList))
            {
                oldList.Remove(key);
                if (oldList.Count == 0)
                    _expirable.Remove(oldExpireTick);
            }
        }

        // 2️⃣ 새 tick으로 재등록
        if (!_expirable.TryGetValue(expireTick, out var list))
            _expirable[expireTick] = list = new List<StackKey>();

        list.Add(key);
    }
    #endregion
    
    #region ===== Cache =====

    private void CacheStack(ushort tick)
    {
        if (!_expirable.TryGetValue(tick, out var list)) 
            return;

        foreach (var key in list)
        {
            ResolveCache(key);
            _stackStorage[key] = new StackStatus(0, tick, 0);
        }
        list.Clear();
        _expirable.Remove(tick);
    }
    #endregion
    
    #region ===== Stack Resolve =====

    private void ResolveApply(StackKey stack, int amp = 1)
    {
        switch (stack.def)
        {
            case VariableDefinition:
                break;
            case BuffStackDefinition buff:
                _context.Stats.TryApply(new BuffData(buff.Type, buff.Value * amp, buff.displayName));
                break;
            case CCStackDefinition cc:
                _context.Act.ApplyCC(new CCData(cc.Type, cc.Value));
                break;
        }
    }

    private void ResolveCache(StackKey stack)
    {
        switch (stack.def)
        {
            case VariableDefinition va:
                //TODO: Add Variable period here
                break;
            case BuffStackDefinition buff:
                _context.Stats.TryRemove(new BuffData(buff.Type, buff.Value * _stackStorage[stack].Amount, buff.displayName));
                break;
            case CCStackDefinition cc:
                _context.Act.RemoveCC(new CCData(cc.Type, cc.Value));
                break;
        }
    }
    
    #endregion
    
    #region ===== Utils =====
    

    private ushort EndTick(ushort tick, ushort duration)
    {
        if (duration == 65535 || tick + duration < tick) //tick + duration < tick => overflow
        {
            return 65535;
        }

        return (ushort)(tick + duration);
    }

    private int TotalStack(int max, params int[] applies)
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