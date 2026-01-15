using System;
using System.Collections.Generic;
using System.Linq;
using Moves;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using Systems.Time;
using UnityEngine;

namespace PlayerScripts.Stack
{
    /// <summary>
    /// StackManager (Expirable + Periodic Variable 지원)
    /// - 시간 판정은 DelayScheduler에 위임
    /// - StackStatus는 (Amount, DelayId)만 가짐
    ///   -> 따라서 Expirable+Periodic을 동시에 갖는 Variable은 이 버전에서 지원하지 않음.
    /// </summary>
    public class StackManager
    {
        private readonly Context _context;
        public VariableStorage Storage;
        private readonly DelayScheduler _scheduler;
        private List<StackDelta> _resolving = new();
        private List<StackDelta> _collecting = new();

        // 현재 Tick 캐시 (모듈이 "마지막으로 실행된 시점"만 기억)
        private ushort _currentTick;

        private readonly Dictionary<StackKey, StackStatus> _stackStorage = new();
        private readonly Dictionary<StackKey, StackMetadata> _metadata = new();

        public StackManager(Context ctx, VariableStorage storage)
        {
            _context = ctx;
            Storage = storage;
            _scheduler = _context.DelayScheduler;
        }

        public void EnqueueStack(StackKey stackKey, int amount)
        {
            _collecting.Add(new StackDelta(stackKey, amount));
        }

        //Overloading EnqueueStack explicitly to prevent confusion.
        public void EnqueueStack(StackKey stackKey, int amount, StackMetadata metadata)
        {
            _collecting.Add(new StackDelta(stackKey, amount, metadata));
        }

        public void EnqueueStack(StackDelta delta)
        {
            _collecting.Add(delta);
        }

        /// <summary>
        /// 매 Tick 호출.
        /// - Expirable(Buff/CC/Expirable Variable): 만료되면 제거
        /// - Periodic Variable: 주기 Delay 완료되면 +1 적용 후 필요하면 다음 주기 재시작
        /// </summary>
        public void Tick(ushort tick)
        {
            _currentTick = tick;
            SwapAndApply(tick);
            // 순회 중 컬렉션 변경을 피하기 위해 처리 대상 수집
            List<StackKey> expireKeys = null;
            List<StackKey> periodicKeys = null;

            foreach (var key in 
                     from kv in _stackStorage 
                     let key = kv.Key 
                     let status = kv.Value 
                     where IsValidDelay(status.DelayId) 
                     where _scheduler.IsCompleted(status.DelayId, _currentTick) 
                     select key)
            {
                // DelayId가 완료되었을 때 "무슨 의미인지"는 Definition이 결정
                if (key.def is VariableDefinition va && va.isPeriodic)
                {
                    periodicKeys ??= new List<StackKey>();
                    periodicKeys.Add(key);
                }
                else
                {
                    // Buff/CC/Expirable Variable은 완료 시 제거로 해석
                    expireKeys ??= new List<StackKey>();
                    expireKeys.Add(key);
                }
            }

            // 1) Expiration 제거 처리
            if (expireKeys != null)
            {
                foreach (var key in expireKeys)
                {
                    RemoveStackCompletely(key);
                }
            }

            // 2) Periodic 완료 처리 (+1 후 필요 시 재시작)
            if (periodicKeys != null)
            {
                foreach (var key in periodicKeys)
                {
                    HandlePeriodicCompleted(key, _currentTick);
                }
            }
        }

        /// <summary>
        /// 공용 Apply 진입점.
        /// - Buff/CC: Expiration Delay(def.duration) 부여
        /// - Variable(Periodic): Apply 트리거( after가 max 미만 )일 때 Periodic Delay(def.periodTick) 시작
        /// - Variable(NonPeriodic): 기본은 Expiration(def.duration)일 수도 있고 영구(duration==65535)일 수도 있음
        /// </summary>
        public void ApplyStack(StackKey stackKey, int amount, ushort tick, ushort durationOverride = 0, StackMetadata metadata = default)
        {
            var def = stackKey.def;
            //Debug.Log($"{def.displayName}을(를) 적용하려 합니다.");
            _stackStorage.TryGetValue(stackKey, out var oldStatus);
            var before = oldStatus.Amount;

            var after = TotalStack(def.maxStacks, before, amount);

            // 0 이하로 떨어지는 Apply(음수 적용)는 ConsumeVariable 쪽으로 보내는 것을 권장하지만,
            // 방어적으로 clamp.
            if (after < 0) after = 0;

            // DelayId는 케이스별로 의미가 달라짐(Periodic 또는 Expiration)
            var nextDelayId = oldStatus.DelayId;

            switch (def)
            {
                case VariableDefinition va:
                {
                    // Variable Storage 갱신(표시/상태용)
                    // (프로젝트의 VariableStorage 설계에 맞춰 "존재 여부"를 유지하려면 Remove 대신 Update가 필요할 수 있음)
                    if (after <= 0)
                    {
                        // Variable이 0이 되면 Storage에서 제거하는 정책을 기본으로 둠.
                        // (만약 0이어도 표시가 필요하면 Remove 대신 0으로 업데이트하는 메서드가 필요)
                        Storage.RemoveStorage(stackKey);
                    }
                    else
                    {
                        Storage.AddStorage(stackKey, new VariableState(after, tick));
                    }

                    if (va.isPeriodic)
                    {
                        //   after < maxStacks 이면 재적용(= periodic delay 시작/유지)
                        var shouldStartPeriodic = ShouldStartPeriodicOnApply(after, va.maxStacks);

                        if (shouldStartPeriodic)
                        {
                            // 기존 Delay가 있었다면 교체
                            if (IsValidDelay(nextDelayId))
                                _scheduler.Remove(nextDelayId);

                            nextDelayId = _scheduler.Start(tick, va.periodTick);
                        }
                        else
                        {
                            // maxStacks에 도달했다면 periodic은 멈춤(Delay 제거)
                            if (IsValidDelay(nextDelayId))
                            {
                                _scheduler.Remove(nextDelayId);
                                nextDelayId = default;
                            }
                        }

                        _stackStorage[stackKey] = new StackStatus(after, nextDelayId);

                        // Variable 자체 효과(Apply/Detach 시 추가 로직이 있다면 ResolveApply에서 처리)
                        ResolveApply(stackKey, tick, amount);
                        return;
                    }

                    // Non-periodic Variable: duration에 따라 Expirable(만료)일 수도, 영구일 수도 있음
                    int durationTick = durationOverride != 0 ? durationOverride : va.duration;
                    if (durationTick != 65535 && after > 0)
                    {
                        // Expiration Delay 설정(변수에도 만료가 있는 경우)
                        if (IsValidDelay(nextDelayId))
                            _scheduler.Remove(nextDelayId);

                        nextDelayId = _scheduler.Start(tick, durationTick);
                    }
                    else
                    {
                        // 영구면 Delay 없음
                        if (IsValidDelay(nextDelayId))
                        {
                            _scheduler.Remove(nextDelayId);
                            nextDelayId = default;
                        }
                    }

                    _stackStorage[stackKey] = new StackStatus(after, nextDelayId);
                    ResolveApply(stackKey, tick, amount);
                    return;
                }

                case BuffDefinition buff:
                {
                    // Buff/CC는 Expirable 모델
                    int durationTick = durationOverride != 0 ? durationOverride : buff.duration;

                    if (IsValidDelay(nextDelayId))
                        _scheduler.Remove(nextDelayId);
                    nextDelayId = _scheduler.Start(tick, durationTick);

                    _stackStorage[stackKey] = new StackStatus(after, nextDelayId);

                    ResolveApply(stackKey, tick, amount);
                    return;
                }

                case CCDefinition cc:
                {
                    int durationTick = durationOverride != 0 ? durationOverride : cc.duration;

                    if (IsValidDelay(nextDelayId))
                        _scheduler.Remove(nextDelayId);

                    nextDelayId = _scheduler.Start(tick, durationTick);

                    _stackStorage[stackKey] = new StackStatus(after, nextDelayId);

                    ResolveApply(stackKey, tick, amount);
                    return;
                }

                case TriggerableDefinition trigger:
                {
                    int durationTick = durationOverride != 0 ? durationOverride : trigger.duration;
                    if(IsValidDelay(nextDelayId))
                        _scheduler.Remove(nextDelayId);
                    nextDelayId = _scheduler.Start(tick, durationTick);
                    _stackStorage[stackKey] = new StackStatus(after, nextDelayId);
                    ResolveTrigger(stackKey, tick, metadata, amount);
                    return;
                }

                default:
                {
                    // 기타 정의는 기본적으로 Expirable로 취급(필요 시 분기 확장)
                    int durationTick = durationOverride != 0 ? durationOverride : def.duration;

                    if (durationTick != 65535)
                    {
                        if (IsValidDelay(nextDelayId))
                            _scheduler.Remove(nextDelayId);

                        nextDelayId = _scheduler.Start(tick, durationTick);
                    }

                    _stackStorage[stackKey] = new StackStatus(after, nextDelayId);
                    ResolveApply(stackKey, tick, amount);
                    return;
                }
            }
        }


        public void TryConsume(SwitchVariable sv)
        {
            StackKey stack = default;
            foreach (var key in _stackStorage.Keys.Where(key => key.def.ID.Equals(sv.Variable.ID)))
            {
                stack = key;
                break;
            }
            ConsumeVariable(stack, sv.Amount, _currentTick);
        }
        /// <summary>
        /// Variable 소비(Amount 감소) 전용 API.
        /// - 합의된 "제거 트리거" 공식을 적용:
        ///   before == maxStacks && after가 maxStacks 미만이면 periodic 시작
        /// - Recharge/Accumulate 구분 없이 maxStacks 기반으로 동작
        /// </summary>
        public void ConsumeVariable(StackKey key, int consumeAmount, ushort tick)
        {
            if (consumeAmount <= 0)
                return;

            if (key.def is not VariableDefinition va)
                return;

            if (!_stackStorage.TryGetValue(key, out var status))
                return;

            var before = status.Amount;
            if (before <= 0)
                return;

            var after = Math.Max(0, before - consumeAmount);

            // Storage 갱신
            if (after <= 0)
                Storage.RemoveStorage(key);
            else
                Storage.AddStorage(key, new VariableState(after, tick));

            // Non-periodic variable이라면 여기서 끝 (소비 후 만료 스케줄링 같은 건 정의 필요)
            if (!va.isPeriodic)
            {
                _stackStorage[key] = new StackStatus(after, status.DelayId);
                return;
            }

            // -----------------------------
            // 합의된 "제거 트리거" 공식:
            //   before == maxStacks && after < maxStacks -> periodic 시작
            // -----------------------------
            var shouldStartPeriodic = ShouldStartPeriodicOnRemove(before, after, va.maxStacks);

            var nextDelayId = status.DelayId;

            if (shouldStartPeriodic)
            {
                if (IsValidDelay(nextDelayId))
                    _scheduler.Remove(nextDelayId);

                nextDelayId = _scheduler.Start(tick, va.periodTick);
            }
            else
            {
                // 제거가 있었지만 시작 조건이 아니라면 아무것도 하지 않음
                // (누적 중이던 상태라면 이미 Delay가 돌고 있어야 함)
            }

            _stackStorage[key] = new StackStatus(after, nextDelayId);
        }

        /// <summary>
        /// Expiration 등에 의해 스택을 완전히 제거한다.
        /// - Delay 제거
        /// - ResolveCache 호출(도메인 효과 해제)
        /// - Storage 제거(Variable)
        /// </summary>
        private void RemoveStackCompletely(StackKey key)
        {
            if (!_stackStorage.TryGetValue(key, out var status))
                return;

            if (IsValidDelay(status.DelayId))
                _scheduler.Remove(status.DelayId);

            ResolveCache(key);

            if (key.def is VariableDefinition)
                Storage.RemoveStorage(key);

            _stackStorage.Remove(key);
        }

        /// <summary>
        /// Periodic Delay 완료 처리:
        /// - Amount가 max보다 작으면 +1 (ApplyStack을 타지 않고 직접 갱신: 무한 루프 방지/의도 명확)
        /// - max에 도달하면 Delay 제거하고 정지
        /// - 아직 max 미만이면 다음 periodTick으로 Delay 재시작
        /// </summary>
        private void HandlePeriodicCompleted(StackKey key, ushort tick)
        {
            if (key.def is not VariableDefinition va || !va.isPeriodic)
            {
                // 안전장치: periodic이 아닌데 periodicKeys에 들어온 경우
                RemoveStackCompletely(key);
                return;
            }

            if (!_stackStorage.TryGetValue(key, out var status))
                return;

            // 현재 완료된 Delay 제거 (재시작/정지 결정 전에 정리)
            if (IsValidDelay(status.DelayId))
                _scheduler.Remove(status.DelayId);

            var before = status.Amount;
            var after = before;

            if (before < va.maxStacks)
            {
                after = Math.Min(before + 1, va.maxStacks);

                // Storage 갱신
                if (after <= 0)
                    Storage.RemoveStorage(key);
                else
                    Storage.AddStorage(key, new VariableState(after, tick));
            }

            // max에 도달했으면 periodic 중지(DelayId 없음)
            if (after >= va.maxStacks)
            {
                _stackStorage[key] = new StackStatus(after, default);
                return;
            }

            // 아직 max 미만이면 다음 주기 시작
            var nextDelayId = _scheduler.Start(tick, va.periodTick);
            _stackStorage[key] = new StackStatus(after, nextDelayId);
        }

        /// <summary>
        /// 추가 트리거 공식:
        /// - 적용 후 총량이 maxStacks 미만이면 재적용(=periodic) 개시
        /// </summary>
        private static bool ShouldStartPeriodicOnApply(int afterAmount, int maxStacks)
            => afterAmount < maxStacks;

        /// <summary>
        /// 제거 트리거 공식:
        /// - 제거 "이전"이 maxStacks이고 제거 "이후"가 maxStacks 미만이면 재적용(=periodic) 개시
        /// </summary>
        private static bool ShouldStartPeriodicOnRemove(int beforeAmount, int afterAmount, int maxStacks)
            => beforeAmount == maxStacks && afterAmount < maxStacks;

        private static bool IsValidDelay(DelayId id)
            => !id.Equals(default);

        private void ResolveApply(StackKey stack, ushort tick, int amp = 1)
        {
            switch (stack.def)
            {
                case VariableDefinition:
                    // Variable은 Storage 갱신이 핵심이며,
                    // 별도 적용 효과(예: UI, 상태 플래그)가 필요하면 여기서 처리.
                    break;

                case BuffDefinition buff:
                    _context.Stats.TryApply(new BuffData(buff.Type, buff.Value * amp, buff.displayName));
                    break;

                case CCDefinition cc:
                    _context.Act.ApplyCC(new CCData(cc.Type, cc.Value));
                    break;
            }
            //Debug.Log($"{stack.def.displayName}이 적용되었습니다.");
        }

        private void ResolveTrigger(StackKey stack, ushort tick, StackMetadata metadata, int amount = 1)
        {
            if (stack.def is not TriggerableDefinition trigger) return;
            int storing;
            if (!_metadata.TryGetValue(stack, out var storedmeta))
            {
                storing = metadata.Metadata;
                _metadata.Add(stack, metadata);
            }
            else
            {
                storing = storedmeta.Metadata + metadata.Metadata;
            }
            _metadata[stack] = new StackMetadata(storing);
            if (!_stackStorage.TryGetValue(stack, out var status) || status.Amount < trigger.threshold) return;
            if (trigger.effect.mechanism is not INewMechanism mech) return;
            DamageData dmg = new(DamageType.Normal, storing);
            CastContext ctx = new(trigger.effect.@params, stack.applier, _context.Transform, dmg);
            RemoveStackCompletely(stack);
            _metadata[stack] = new StackMetadata(0);
            mech.Execute(ctx);
        }

        private void ResolveCache(StackKey stack)
        {
            switch (stack.def)
            {
                case VariableDefinition:
                    // Variable의 "해제 효과"가 필요하다면 여기서 처리 가능.
                    break;

                case BuffDefinition buff:
                    // 제거 시점의 Amount를 기준으로 제거(기존 방식 유지)
                    // 주의: RemoveStackCompletely에서 _stackStorage.Remove 전에 호출되므로 안전.
                    _context.Stats.TryRemove(new BuffData(buff.Type, buff.Value * _stackStorage[stack].Amount, buff.displayName));
                    break;

                case CCDefinition cc:
                    _context.Act.RemoveCC(new CCData(cc.Type, cc.Value));
                    break;
            }
        }

        private int TotalStack(int max, params int[] applies)
        {
            var total = applies.Sum();
            if (total < 0) total = 0;
            return Math.Min(total, max);
        }

        private void SwapAndApply(ushort tick)
        {
            (_resolving, _collecting) = (_collecting, _resolving);
            if (_resolving.Count <= 0) return;
            foreach (var delta in _resolving)
            {
                ApplyStack(delta.Key, delta.Amount, tick, 0, delta.Metadata);
            }
            _resolving.Clear();
        }
    }

    public readonly struct StackDelta
    {
        public readonly StackKey Key;
        public readonly int Amount;
        public readonly StackMetadata Metadata;

        public StackDelta(StackKey key, int amount, StackMetadata metadata = default)
        {
            Key = key;
            Amount = amount;
            Metadata = metadata;
        }
    }
}
