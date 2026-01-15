using System.Collections.Generic;
using System.Linq;
using Moves;
using Systems.Stacks;
using Systems.Stacks.Definition;
using UnityEngine;

namespace PlayerScripts.Stack
{
    public class VariableStorage
    {
        private readonly Dictionary<StackKey, VariableState> _raw = new();
        private readonly Dictionary<ushort, StackKey> _exclusiveWinners = new();
        public readonly Dictionary<StackKey, VariableState> Public = new();

        public void AddStorage(StackKey key, VariableState state)
        {
            if (key.def is not VariableDefinition) return;
            Debug.Log($"{key.def.displayName} 이(가) {state.Amount} 추가되었습니다.");
            _raw[key] = state;
            UpdateExclusiveGroups(key);
            RebuildPublic();
        }

        public void RemoveStorage(StackKey key, int amount = 0)
        {
            if (key.def is not VariableDefinition) return;
            Debug.Log($"{key.def.displayName} 이(가) 삭제되었습니다.");
            _raw.Remove(key);
            UpdateExclusiveGroups(key);
            RebuildPublic();
        }

        public void Tell(bool ignoreExclusive = false)
        {
            if (ignoreExclusive)
            {
                foreach (var key in _raw.Keys)
                {
                    Debug.Log($"{key.def.displayName}이 존재합니다.");
                }
            }
            else
            {
                foreach (var key in Public.Keys)
                {
                    Debug.Log($"{key.def.displayName}이 공개되었습니다.");
                }
            }
        }

        public SwitchVariable GetVariable(VariableDefinition def)
        {
            var key = new StackKey(def);
            return Public.TryGetValue(key, out var value) ? new SwitchVariable(def, value.Amount) : new SwitchVariable(def, 0);
        }

        public bool Has(StackDefinition def) //Don't check applier since VariableDefinition itself doesn't have its applier
        {
            var found = false;
            foreach (var key in Public.Keys)
            {
                found = key.def == def;
                if (found) break;
            }
            return found;
        }

        public bool Has(StackKey stack) //It checks applier
        {
            var found = false;
            foreach (var key in Public.Keys)
            {
                found = key.Equals(stack);
                if (found) break;
            }
            return found;
        }
        private void UpdateExclusiveGroups(StackKey key)
        {
            if (key.def is not VariableDefinition va) 
                return;

            // 이 Variable이 속한 모든 exclusive 그룹을 갱신해야 한다
            foreach (var group in va.exclusiveGroup)
            {
                var gid = group.groupId;

                StackKey? winner = null;
                VariableState? winnerState = null;

                // Raw에 있는 모든 VariableKey 중 이 그룹에 속한 것들만 검사
                foreach (var (stackKey, variableState) in _raw)   // Raw: Dictionary<StackKey, VariableState>
                {
                    // 없는 Variable이면 무시
                    if (variableState.Amount <= 0)
                        continue;

                    // 이 Variable이 같은 그룹인지 검사
                    if (stackKey.def is not VariableDefinition vd || vd.exclusiveGroup == null)
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
                        winner = stackKey;
                        winnerState = variableState;
                    }
                    else
                    {
                        var ws = winnerState.Value;
                        // ① lastTick이 큰 쪽 승자
                        if (variableState.LastTick > ws.LastTick)
                        {
                            winner = stackKey;
                            winnerState = variableState;
                        }
                        // ② lastTick 동일하면 priority 비교
                        else if (variableState.LastTick == ws.LastTick)
                        {
                            int oldPri = ((VariableDefinition)winner.Value.def).exclusivePriority;
                            int newPri = vd.exclusivePriority;
                            if (newPri > oldPri)
                            {
                                winner = stackKey;
                                winnerState = variableState;
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

                if (key.def is VariableDefinition { exclusiveGroup: { Length: > 0 } }) continue; // exclusive는 이 단계에서 보류

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
        public readonly ushort LastTick;

        public VariableState(int amount, ushort lastTick)
        {
            Amount = amount;
            LastTick = lastTick;
        }
    }
}