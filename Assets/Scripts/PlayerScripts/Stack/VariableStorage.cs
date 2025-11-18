using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VariableStorage
{
    public static VariableStorage Instance;
    private readonly Dictionary<StackKey, VariableState> _raw = new();
    private readonly Dictionary<ushort, StackKey> _exclusiveWinners = new();
    public readonly Dictionary<StackKey, VariableState> Public = new();
    public VariableStorage()
    {
        Instance = this;
    }

    public void AddStorage(StackKey key, VariableState state)
    {
        if (key.def is not VariableDefinition va) return;
        //Debug.Log($"{state.lastTick}에 {key.def.displayName} 이(가) {state.Amount} 추가되었습니다.");
        _raw[key] = state;
        UpdateExclusiveGroups(key);
        RebuildPublic();
    }

    public void RemoveStorage(StackKey key, int amount = 0)
    {
        if (key.def is not VariableDefinition va) return;
        //Debug.Log($"{key.def.displayName} 이(가) 삭제되었습니다.");
        _raw.Remove(key);
        UpdateExclusiveGroups(key);
        RebuildPublic();
    }

    public void Tell(bool ignoreExclusive = false)
    {
        foreach (var key in Public.Keys)
        {
            Debug.Log($"{key.def.displayName}이 공개되었습니다.");
        }
    }

    public SwitchVariable GetVariable(VariableDefinition def)
    {
        var key = new StackKey(def);
        return Public.TryGetValue(key, out var value) ? new SwitchVariable(def, value.Amount) : new SwitchVariable(def, 0);
    }
    private void UpdateExclusiveGroups(StackKey key)
    {
    if (key.def is not VariableDefinition vdef) 
        return;

    // 이 Variable이 속한 모든 exclusive 그룹을 갱신해야 한다
    foreach (var group in vdef.exclusiveGroup)
    {
        var gid = group.groupId;

        StackKey? winner = null;
        VariableState? winnerState = null;

        // Raw에 있는 모든 VariableKey 중 이 그룹에 속한 것들만 검사
        foreach (var kv in _raw)   // Raw: Dictionary<StackKey, VariableState>
        {
            var k = kv.Key;
            var s = kv.Value;

            // 없는 Variable이면 무시
            if (s.Amount <= 0)
                continue;

            // 이 Variable이 같은 그룹인지 검사
            if (k.def is not VariableDefinition vd || vd.exclusiveGroup == null)
                continue;

            // 이 Variable이 groupId와 동일한 그룹을 포함하는가?
            var sameGroup = 
                vd.exclusiveGroup
                    .Any(g => g.groupId == gid);
            
            if (!sameGroup)
                continue;
            // 후보 비교 (AppliedAt → Priority)
            if (winner == null)
            {
                winner = k;
                winnerState = s;
            }
            else
            {
                var ws = winnerState.Value;
                // ① lastTick이 큰 쪽 승자
                if (s.lastTick > ws.lastTick)
                {
                    winner = k;
                    winnerState = s;
                }
                // ② lastTick 동일하면 priority 비교
                else if (s.lastTick == ws.lastTick)
                {
                    int oldPri = ((VariableDefinition)winner.Value.def).exclusivePriority;
                    int newPri = vd.exclusivePriority;
                    if (newPri > oldPri)
                    {
                        winner = k;
                        winnerState = s;
                    }
                }
            }
        }

        // 결과 반영
        if (winner != null)
            _exclusiveWinners[gid] = winner.Value;
        else
            _exclusiveWinners.Remove(gid); // 빈 그룹이면 제거
    }
    }
    private void RebuildPublic()
    {
        Public.Clear();

        // 1) exclusive가 아닌 Variable은 모두 추가
        foreach (var kv in _raw)
        {
            var key = kv.Key;
            var state = kv.Value;

            if (state.Amount <= 0)
                continue;

            if (key.def is VariableDefinition vdef)
            {
                if (vdef.exclusiveGroup is { Length: > 0 })
                    continue; // exclusive는 이 단계에서 보류
            }

            // non-exclusive는 그대로 공개
            Public[key] = state;
        }

        // 2) exclusive winner만 추가
        foreach (var winnerKey in from kv in _exclusiveWinners let gid = kv.Key select kv.Value)
        {
            if (_raw.TryGetValue(winnerKey, out var state) && state.Amount > 0)
            {
                Public[winnerKey] = state;
            }
        }
    }

}

public readonly struct VariableState
{
    public readonly int Amount;
    public readonly ushort lastTick;

    public VariableState(int amount, ushort lastTick)
    {
        Amount = amount;
        this.lastTick = lastTick;
    }
}