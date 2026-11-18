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
        private bool _processing;

        // 현재 Tick 캐시 (모듈이 "마지막으로 실행된 시점"만 기억)
        private long _currentTick;

        private readonly Dictionary<StackKey, StackStatus> _stackStorage = new();
        private readonly Dictionary<StackKey, StackMetadata> _metadata = new();
        private readonly Dictionary<StackKey, ActBridge.CCContribution> _ccContributions = new();
        private readonly Dictionary<StackKey, StatsContainer.BuffContribution> _buffContributions = new();

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
        public void Tick(long tick)
        {
            // 외부 콜백이 Tick을 다시 호출해도 처리 중인 버퍼를 교환하지 않습니다.
            if (_processing) return;
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            _processing = true;
            try { TickCore(tick); }
            finally { _processing = false; }
        }

        private void TickCore(long tick)
        {
            _currentTick = tick;
            SwapAndApply(tick);
            // 순회 중 컬렉션 변경을 피하기 위해 처리 대상 수집
            List<KeyValuePair<StackKey, StackStatus>> expireKeys = null;
            List<KeyValuePair<StackKey, StackStatus>> periodicKeys = null;

            foreach (var entry in
                     from kv in _stackStorage 
                     let key = kv.Key 
                     let status = kv.Value 
                     where IsValidDelay(status.DelayId) 
                     where _scheduler.IsCompleted(status.DelayId, _currentTick) 
                     select kv)
            {
                var key = entry.Key;
                // DelayId가 완료되었을 때 "무슨 의미인지"는 Definition이 결정
                if (key.def is VariableDefinition { isPeriodic: true })
                {
                    periodicKeys ??= new();
                    periodicKeys.Add(entry);
                }
                else
                {
                    // Buff/CC/Expirable Variable은 완료 시 제거로 해석
                    expireKeys ??= new();
                    expireKeys.Add(entry);
                }
            }

            // 1) Expiration 제거 처리
            if (expireKeys != null)
            {
                foreach (var entry in expireKeys)
                {
                    try
                    {
                        if (IsCurrentReservation(entry)) RemoveStackCompletely(entry.Key);
                    }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }

            // 2) Periodic 완료 처리 (+1 후 필요 시 재시작)
            if (periodicKeys != null)
            {
                foreach (var entry in periodicKeys)
                {
                    try
                    {
                        if (IsCurrentReservation(entry)) HandlePeriodicCompleted(entry.Key, _currentTick);
                    }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
        }

        /// <summary>
        /// 공용 Apply 진입점.
        /// - Buff/CC: Expiration Delay(def.duration) 부여
        /// - Variable(Periodic): Apply 트리거( after가 max 미만 )일 때 Periodic Delay(def.periodTick) 시작
        /// - Variable(NonPeriodic): 기본은 Expiration(def.duration)일 수도 있고 영구(duration==65535)일 수도 있음
        /// </summary>
        public void ApplyStack(StackKey stackKey, int amount, long tick, ushort durationOverride = 0, StackMetadata metadata = default)
        {
            if (_processing)
            {
                // 재진입 요청은 다음 정상 Tick에서 처리합니다. 기간 override도 보존합니다.
                EnqueueStack(new StackDelta(stackKey, amount, metadata, durationOverride));
                return;
            }
            _processing = true;
            try { ApplyStackCore(stackKey, amount, tick, durationOverride, metadata); }
            finally { _processing = false; }
        }

        private void ApplyStackCore(StackKey stackKey, int amount, long tick, ushort durationOverride, StackMetadata metadata)
        {
            var def = stackKey.def;
            if (!def || stackKey.applierName == null) throw new ArgumentException("Stack definition and applier name are required", nameof(stackKey));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            //Debug.Log($"{def.displayName}을(를) 적용하려 합니다.");
            _stackStorage.TryGetValue(stackKey, out var oldStatus);
            var before = oldStatus.Amount;

            var after = TotalStack(def.maxStacks, before, amount);

            // DelayId는 케이스별로 의미가 달라짐(Periodic 또는 Expiration)
            var nextDelayId = oldStatus.DelayId;

            switch (def)
            {
                case VariableDefinition va:
                {
                    ApplyVariable(stackKey, va, after, tick, durationOverride, oldStatus);
                    return;
                }
                case BuffDefinition buff:
                {
                    ApplyBuff(stackKey, buff, after, tick, durationOverride, oldStatus);
                    return;
                }

                case CCDefinition cc:
                {
                    ApplyCrowdControl(stackKey, cc, after, tick, durationOverride, oldStatus);
                    return;
                }

                case TriggerableDefinition trigger:
                {
                    ApplyTrigger(stackKey, trigger, after, tick, durationOverride, metadata, oldStatus);
                    return;
                }

                default:
                {
                    // 기타 정의는 기본적으로 Expirable로 취급(필요 시 분기 확장)
                    int durationTick = durationOverride != 0 ? durationOverride : def.duration;

                    if (durationTick != 65535)
                    {
                        nextDelayId = ReplaceReservation(oldStatus.DelayId, tick, durationTick);
                    }

                    _stackStorage[stackKey] = new StackStatus(after, nextDelayId);
                    ResolveApply(stackKey, tick, amount);
                    return;
                }
            }
        }

        private bool IsCurrentReservation(KeyValuePair<StackKey, StackStatus> entry) =>
            _stackStorage.TryGetValue(entry.Key, out var current) && current.DelayId.Equals(entry.Value.DelayId);

        // 효과 없는 일반 정의의 기존 예약을 새 예약 확보 전까지 보존합니다.
        private DelayId ReplaceReservation(DelayId previous, long tick, int duration)
        {
            var next = _scheduler.Start(tick, duration);
            _scheduler.Remove(previous);
            return next;
        }

        /// <summary>Key 총량의 최종 기여를 교체합니다. 실패 복구에는 산식 재계산을 사용하지 않습니다.</summary>
        private void ApplyBuff(StackKey key, BuffDefinition definition, int after, long tick,
            ushort durationOverride, StackStatus previous)
        {
            if (after == 0) { RemoveStackCompletely(key); return; }
            var hadState = _stackStorage.ContainsKey(key);
            _buffContributions.TryGetValue(key, out var contribution);
            var change = _context.Stats.PrepareBuffChange(contribution, definition.Type, definition.Value, after);
            var replacement = _scheduler.Start(tick, durationOverride != 0 ? durationOverride : definition.duration);
            try
            {
                // 이 구간은 외부 콜백이 없습니다. Stats Commit 실패도 아래에서 원상 복구합니다.
                _buffContributions[key] = change.Handle;
                _stackStorage[key] = new StackStatus(after, replacement);
                change.Commit();
            }
            catch
            {
                change.Rollback();
                if (contribution != null) _buffContributions[key] = contribution;
                else _buffContributions.Remove(key);
                if (hadState) _stackStorage[key] = previous;
                else _stackStorage.Remove(key);
                _scheduler.Remove(replacement);
                throw;
            }
            _scheduler.Remove(previous.DelayId);
            change.Complete();
        }

        private void ApplyVariable(StackKey key, VariableDefinition definition, int after, long tick,
            ushort durationOverride, StackStatus previous)
        {
            var duration = durationOverride != 0 ? durationOverride : definition.duration;
            var needsDelay = definition.isPeriodic ? ShouldStartPeriodicOnApply(after, definition.maxStacks) : duration != 65535 && after > 0;
            var next = needsDelay ? _scheduler.Start(tick, definition.isPeriodic ? definition.periodTick : duration) : default;
            var hadState = _stackStorage.ContainsKey(key);
            var hadVisible = Storage.TryGetRaw(key, out var visible);
            try
            {
                if (after <= 0) Storage.RemoveStorage(key);
                else Storage.AddStorage(key, new VariableState(after, tick));
                _stackStorage[key] = new StackStatus(after, next);
            }
            catch
            {
                _scheduler.Remove(next);
                if (hadState) _stackStorage[key] = previous;
                else _stackStorage.Remove(key);
                if (hadVisible) Storage.AddStorage(key, visible);
                else Storage.RemoveStorage(key);
                throw;
            }
            _scheduler.Remove(previous.DelayId);
        }

        /// <summary>
        /// CC는 Key당 제한 하나를 소유합니다. 예약 실패 시 옛 예약/효과를 보존하고,
        /// 수량과 소유 증표를 확정한 뒤에만 되돌릴 수 없는 Pending 취소를 알립니다.
        /// </summary>
        private void ApplyCrowdControl(StackKey key, CCDefinition cc, int after, long tick,
            ushort durationOverride, StackStatus previous)
        {
            if (after == 0) { RemoveStackCompletely(key); return; }
            var hadState = _stackStorage.ContainsKey(key);
            _ccContributions.TryGetValue(key, out var contribution);
            var fresh = contribution == null;
            // 새 예약 확보가 실패해도 현재 상태에는 아직 아무 변경이 없습니다.
            var replacement = _scheduler.Start(tick, durationOverride != 0 ? durationOverride : cc.duration);
            try
            {
                if (fresh)
                {
                    contribution = _context.Act.AcquireCC(new CCData(cc.Type, cc.Value));
                    _ccContributions.Add(key, contribution);
                }
                _stackStorage[key] = new StackStatus(after, replacement);
            }
            catch
            {
                if (fresh)
                {
                    _ccContributions.Remove(key);
                    _context.Act.ReleaseCC(contribution);
                }
                if (hadState) _stackStorage[key] = previous;
                else _stackStorage.Remove(key);
                _scheduler.Remove(replacement);
                throw;
            }
            _scheduler.Remove(previous.DelayId);
            // 이 이후 실패는 확정된 CC를 되돌리지 않습니다. 재적용도 하지 않습니다.
            if (fresh)
            {
                try { _context.Act.NotifyCCCommitted(contribution); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }


        public void TryConsume(SwitchVariable sv)
        {
            if (sv.Consumption != null)
            {
                if (!sv.Consumption.CanConsume || sv.Consumption.Consumed) return;
                var change = sv.Consumption.Begin(_currentTick); change?.Complete();
                return;
            }
            // 호환 호출도 ID만으로 임의 적용자의 스택을 제거하지 않습니다.
            if (sv.Variable && Storage.TrySelect(sv.Variable, out var key, out _) && CanConsumeExact(key, sv.Amount))
                ConsumeVariable(key, sv.Amount, _currentTick);
        }

        public SwitchVariable CaptureVariable(VariableDefinition definition)
        {
            return Storage.TrySelect(definition, out var key, out var value)
                ? new SwitchVariable(definition, value.Amount, new VariableConsumption(this, key, value.Amount)) : default;
        }
        internal bool CanConsumeExact(StackKey key, int amount) => amount > 0 && key.def is VariableDefinition &&
            _stackStorage.TryGetValue(key, out var status) && status.Amount >= amount &&
            Storage.TryGetPublic(key, out var visible) && visible.Amount >= amount;

        internal ConsumptionChange BeginConsumption(VariableConsumption receipt, long tick)
        {
            if (!CanConsumeExact(receipt.Key, receipt.Amount)) throw new InvalidOperationException("VariableChangedBeforeCommit");
            return new ConsumptionChange(this, receipt, tick);
        }

        // 기존 충전 예약은 성공 확정까지 제거하지 않습니다. 실패 시 원래 핸들과 Delta까지 그대로 보존합니다.
        internal sealed class ConsumptionChange
        {
            private readonly StackManager _owner;
            private readonly VariableConsumption _receipt;
            private readonly StackStatus _before;
            private readonly VariableState _visibleBefore;
            private readonly DelayId _replacement;
            private bool _finished;
            internal ConsumptionChange(StackManager owner, VariableConsumption receipt, long tick)
            {
                _owner = owner; _receipt = receipt;
                var key = receipt.Key; var va = (VariableDefinition)key.def;
                _before = owner._stackStorage[key]; owner.Storage.TryGetRaw(key, out _visibleBefore);
                var after = _before.Amount - receipt.Amount;
                if (va.isPeriodic && ShouldStartPeriodicOnRemove(_before.Amount, after, va.maxStacks))
                    _replacement = owner._scheduler.Start(tick, va.periodTick);
                try
                {
                    if (after == 0) owner.Storage.RemoveStorage(key);
                    else owner.Storage.AddStorage(key, new VariableState(after, tick));
                    owner._stackStorage[key] = new StackStatus(after, IsValidDelay(_replacement) ? _replacement : _before.DelayId);
                }
                catch { Rollback(); throw; }
            }
            internal void Complete()
            {
                if (_finished) return;
                if (IsValidDelay(_replacement)) _owner._scheduler.Remove(_before.DelayId);
                _receipt.MarkConsumed(); _finished = true;
            }
            internal void Rollback()
            {
                if (_finished) return;
                _owner._scheduler.Remove(_replacement);
                _owner._stackStorage[_receipt.Key] = _before;
                _owner.Storage.AddStorage(_receipt.Key, _visibleBefore);
                _finished = true;
            }
        }
        /// <summary>
        /// Variable 소비(Amount 감소) 전용 API.
        /// - 합의된 "제거 트리거" 공식을 적용:
        ///   before == maxStacks && after가 maxStacks 미만이면 periodic 시작
        /// - Recharge/Accumulate 구분 없이 maxStacks 기반으로 동작
        /// </summary>
        public void ConsumeVariable(StackKey key, int consumeAmount, long tick)
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

            // 최대치에서 내려갈 때만 충전을 새로 시작합니다. 예약 확보 실패는 소비 전입니다.
            if (va.isPeriodic && ShouldStartPeriodicOnRemove(before, after, va.maxStacks))
            {
                ApplyVariable(key, va, after, tick, 0, status);
                return;
            }
            if (after <= 0) Storage.RemoveStorage(key);
            else Storage.AddStorage(key, new VariableState(after, tick));
            _stackStorage[key] = new StackStatus(after, status.DelayId);
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

            if (key.def is TriggerableDefinition)
            {
                // 소비·만료·명시적 0 전이는 같은 종료 경로입니다. 실행 후 cleanup은 없습니다.
                _metadata.Remove(key);
                _stackStorage.Remove(key);
                _scheduler.Remove(status.DelayId);
                return;
            }

            if (key.def is BuffDefinition)
            {
                if (!_buffContributions.TryGetValue(key, out var contribution))
                    throw new InvalidOperationException("Active Buff has no owned contribution");
                var change = _context.Stats.PrepareBuffChange(contribution, default, 0, 0);
                // 준비/Commit 실패 시 기존 수량과 예약을 유지합니다.
                change.Commit();
                _buffContributions.Remove(key);
                _stackStorage.Remove(key);
                _scheduler.Remove(status.DelayId);
                change.Complete();
                return;
            }

            if (key.def is CCDefinition)
            {
                // 저장된 증표의 종류를 사용하므로 정의 변경이나 중복 종료로 다른 CC를 풀지 않습니다.
                _ccContributions.Remove(key, out var contribution);
                _stackStorage.Remove(key);
                _scheduler.Remove(status.DelayId);
                _context.Act.ReleaseCC(contribution);
                return;
            }

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
        private void HandlePeriodicCompleted(StackKey key, long tick)
        {
            if (key.def is not VariableDefinition va || !va.isPeriodic)
            {
                // 안전장치: periodic이 아닌데 periodicKeys에 들어온 경우
                RemoveStackCompletely(key);
                return;
            }

            if (!_stackStorage.TryGetValue(key, out var status))
                return;

            if (status.Amount >= va.maxStacks)
            {
                _stackStorage[key] = new StackStatus(status.Amount, default);
                _scheduler.Remove(status.DelayId);
                return;
            }
            // 다음 예약을 확보한 뒤 +1과 공개 수량을 확정합니다. 실패하면 기존 충전 상태를 유지합니다.
            var after = TotalStack(va.maxStacks, status.Amount, 1);
            ApplyVariable(key, va, after, tick, 0, status);
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

        private void ResolveApply(StackKey stack, long tick, int amp = 1)
        {
            switch (stack.def)
            {
                case VariableDefinition:
                    // Variable은 Storage 갱신이 핵심이며,
                    // 별도 적용 효과(예: UI, 상태 플래그)가 필요하면 여기서 처리.
                    break;

            }
        }

        private void ApplyTrigger(StackKey key, TriggerableDefinition trigger, int after, long tick,
            ushort durationOverride, StackMetadata incoming, StackStatus previous)
        {
            if (after == 0) { RemoveStackCompletely(key); return; }
            var hadState = _stackStorage.ContainsKey(key);
            var hadPayload = _metadata.TryGetValue(key, out var previousPayload);
            // 수량 증가가 0이어도 요청 payload는 한 번 합산합니다. overflow는 요청 전체 거부입니다.
            var payload = new StackMetadata(checked(previousPayload.Metadata + incoming.Metadata));
            var next = _scheduler.Start(tick, durationOverride != 0 ? durationOverride : trigger.duration);
            try
            {
                _metadata[key] = payload;
                _stackStorage[key] = new StackStatus(after, next);
            }
            catch
            {
                if (hadPayload) _metadata[key] = previousPayload; else _metadata.Remove(key);
                if (hadState) _stackStorage[key] = previous; else _stackStorage.Remove(key);
                _scheduler.Remove(next);
                throw;
            }
            _scheduler.Remove(previous.DelayId);
            if (trigger.threshold <= 0 || trigger.threshold > trigger.maxStacks)
            { Debug.LogWarning("StackTrigger preparation rejected: InvalidThreshold"); return; }
            if (after < trigger.threshold) return;

            // payloadは値コピー。準備中/実行中の再入Applyは_processingにより次のTickへ送られます。
            if (!StackTriggerExecutor.TryPrepare(trigger.effect?.mechanism, trigger.effect?.mode ?? default,
                    key.applier, payload.Metadata, $"Stack:{trigger.name}#{trigger.GetInstanceID()}/effect", out var prepared)) return;
            // DelayIdをこの累積状態の識別子として再確認します。別の世代を消費しません。
            if (!_stackStorage.TryGetValue(key, out var current) || !current.DelayId.Equals(next)) return;
            RemoveStackCompletely(key);
            StackTriggerExecutor.ExecutePrepared(prepared);
        }

        private void ResolveCache(StackKey stack)
        {
            switch (stack.def)
            {
                case VariableDefinition:
                    // Variable의 "해제 효과"가 필요하다면 여기서 처리 가능.
                    break;

            }
        }

        private static int TotalStack(int max, int before, int requested)
        {
            if (max < 0) throw new ArgumentOutOfRangeException(nameof(max));
            // int.MaxValue에 추가하는 요청도 clamp 이전에 순환하지 않습니다.
            var total = checked((long)before + requested);
            return (int)Math.Min(max, Math.Max(0L, total));
        }

        private void SwapAndApply(long tick)
        {
            (_resolving, _collecting) = (_collecting, _resolving);
            if (_resolving.Count <= 0) return;
            try
            {
                for (var i = 0; i < _resolving.Count; i++)
                {
                    var delta = _resolving[i];
                    // 콜백/예외 전에 항목을 소비하여 다음 Tick에 재생되지 않게 합니다.
                    _resolving[i] = default;
                    try { ApplyStackCore(delta.Key, delta.Amount, tick, delta.DurationOverride, delta.Metadata); }
                    catch (Exception error) { Debug.LogException(error); }
                }
            }
            finally { _resolving.Clear(); }
        }
    }

    public readonly struct StackDelta
    {
        public readonly StackKey Key;
        public readonly int Amount;
        public readonly StackMetadata Metadata;
        public readonly ushort DurationOverride;

        public StackDelta(StackKey key, int amount, StackMetadata metadata = default, ushort durationOverride = 0)
        {
            Key = key;
            Amount = amount;
            Metadata = metadata;
            DurationOverride = durationOverride;
        }
    }
}
