using System;
using System.Collections.Generic;
using UnityEngine;

public class StackManager
{
    private ushort _lastTick;
    private readonly Context _context;
    public StackManager(Context ctx)
    {
        _context = ctx;
    }

    private readonly Dictionary<StackKey, StackStatus> _stackStorage = new();
    private readonly Dictionary<ushort, List<StackKey>> _expirable = new();
    private readonly List<ReapplySchedule> _reapplicable = new();

    public void Tick(ushort tick)
    {
        if (_lastTick < tick)
        {
            // lastTick부터 현재 틱까지 모든 틱을 순회; 실질적으로 1틱 순회로 끝날 확률이 큼
            for (var t = (ushort)(_lastTick + 1); t <= tick; t++)
            {
                CacheStack(t);
                HandlePeriodic(t);
            }
        }
        else // overflow 발생 시
        {
            for (var t = (ushort)(_lastTick + 1); t != 0; t++)
            {
                CacheStack(t);
                HandlePeriodic(t);
            }

            for (ushort t = 0; t <= tick; t++)
            {
                CacheStack(t);
                HandlePeriodic(t);
            }
        }
        _lastTick = tick;
    }

    public void DetachVariable(StackKey key, ushort tick, int amount = 0)
    {
        // Variable이 아니면 관심 없음
        if (key.def is not VariableDefinition va)
            return;

        if (!_stackStorage.TryGetValue(key, out var status) || status.Amount <= 0)
            return;

        // 현재는 "전부 제거"만 지원한다고 가정 (amount는 확장용)
        _stackStorage[key] = default;  // new StackStatus() 와 동일

        // periodic Variable이면 "다시 차오를 수 있는 상태"가 되었으니 스케줄 등록
        if (va.isPeriodic)
        {
            ReSchedule(key, tick);
        }
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
        ResolveApply(stackKey, tick, amount);
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
            ResolveCache(key, tick);
            _stackStorage[key] = new StackStatus(0, tick, 0);
        }
        list.Clear();
        _expirable.Remove(tick);
    }
    #endregion
    
    #region ===== Resolve =====

    private void ResolveApply(StackKey stack, ushort tick, int amp = 1)
    {
        switch (stack.def)
        {
            case VariableDefinition va:
                if (va.isPeriodic)
                    ApplyPeriodic(stack, va, tick);
                break;
            case BuffStackDefinition buff:
                _context.Stats.TryApply(new BuffData(buff.Type, buff.Value * amp, buff.displayName));
                break;
            case CCStackDefinition cc:
                _context.Act.ApplyCC(new CCData(cc.Type, cc.Value));
                break;
        }
        Debug.Log($"{stack.def.displayName} 이(가) {_stackStorage[stack].Amount} 적용되었습니다.");
    }

    private void ResolveCache(StackKey stack, ushort tick)
    {
        switch (stack.def)
        {
            case VariableDefinition:
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
    
    #region ===== Variable =====
    
    private bool CanReapply(ReapplySchedule schd, ushort tick)
    {
        // Variable이 아니면 재적용 대상이 아님
        if (schd.key.def is not VariableDefinition va || !va.isPeriodic)
            return false;

        // tick + delta 가 관찰자의 "체감 시간"
        int observed = tick + schd.cooldownDelta;
        if (observed < 0) observed = 0;
        if (observed > ushort.MaxValue) observed = ushort.MaxValue;

        return (ushort)observed >= schd.reapplyAt;
    }
    
    private void ApplyPeriodic(StackKey key, VariableDefinition va, ushort tick)
    {
        _stackStorage.TryGetValue(key, out var status);
        int current = status.Amount;

        // 현재 amount가 최대면 더 이상 충전할 필요 없음
        if (current >= va.maxStacks)
            return;

        int next = current + 1;
        if (next > va.maxStacks)
            next = va.maxStacks;

        // 상태 갱신 (duration은 사실상 영구)
        _stackStorage[key] = new StackStatus(next, tick, 65535);

        // 아직 최대치가 아니라면, "다음 한 칸"을 위한 스케줄을 다시 건다
        if (next < va.maxStacks)
        {
            ReSchedule(key, tick);
        }
    }

    private void ReSchedule(StackKey key, ushort tick)
    {
        if (key.def is not VariableDefinition va || !va.isPeriodic)
            return;

        // "제거된 순간" 또는 "방금 충전된 순간" 기준으로 다음 재적용 시각 결정
        ushort reapplyAt = (ushort)(tick + va.periodTick);

        // 같은 Variable에 대한 이전 스케줄은 모두 폐기 (마지막 요청만 유효)
        for (int i = 0; i < _reapplicable.Count; i++)
        {
            if (_reapplicable[i].key.Equals(key))
            {
                _reapplicable.RemoveAt(i);
                break;
            }
        }
        _reapplicable.Add(new ReapplySchedule(key, reapplyAt, 0));
        Debug.Log($"{key.def.displayName} 이(가) {reapplyAt} 에 적용될 것입니다.");
    }

    private void HandlePeriodic(ushort tick)
    {
        for (int i = _reapplicable.Count - 1; i >= 0; i--)
        {
            var schd = _reapplicable[i];

            if (!CanReapply(schd, tick))
                continue;

            // 공통 파이프라인을 타기 위해 ApplyStack을 호출
            // durationOverride는 periodic에선 의미가 없으므로 0 또는 기본값 사용
            ApplyStack(schd.key, 1, tick, 0);

            // 이 스케줄은 소비되었으므로 제거
            _reapplicable.RemoveAt(i);
            // 이후 ApplyPeriodic에서 필요하면 다시 ReSchedule을 호출합니다.
        }
    }

    private readonly struct ReapplySchedule
    {
        public readonly StackKey key;
        public readonly ushort reapplyAt;
        public readonly short cooldownDelta;

        public ReapplySchedule(StackKey key, ushort reapplyAt, short cooldownDelta)
        {
            this.key = key;
            this.reapplyAt = reapplyAt;
            this.cooldownDelta = cooldownDelta;
        }
    }
    #endregion
    
    #region ===== Utils =====
    private void CooldownModifier(StackKey key, short delta)
    {
        for (int i = 0; i < _reapplicable.Count; i++)
        {
            if (_reapplicable[i].key.Equals(key))
            {
                var s = _reapplicable[i];
                short newDelta = (short)(s.cooldownDelta + delta);
                _reapplicable[i] = new ReapplySchedule(s.key, s.reapplyAt, newDelta);
                break;
            }
        }
    }
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