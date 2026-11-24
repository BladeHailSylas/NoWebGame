using System;
using System.Collections.Generic;
using System.Linq;
using Characters;
using Moves;
using Moves.Mechanisms;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using Systems.Data;
using Systems.Time;
using UnityEngine;

namespace PlayerScripts.Acts
{
    /// <summary>
    /// Coordinates player attacks by resolving skill bindings and enqueueing
    /// commands to the shared command collector.
    /// </summary>
    public sealed class Attacker
    {
        // 대상은 입력 요청별로 보관합니다. null은 기존 플레이어의 커서 입력입니다.
        private readonly struct CastInput
        {
            public readonly AttackTrace Trace;
            public readonly SkillSlot Slot;
            public readonly ExplicitAttackTarget Target;
            public readonly long Generation, CollectorGeneration;
            public CastInput(SkillSlot slot, ExplicitAttackTarget target, AttackTrace trace, long generation, long collectorGeneration)
            { Slot = slot; Target = target; Trace = trace; Generation = generation; CollectorGeneration = collectorGeneration; }
        }
        private List<CastInput> _collectingCasts = new();
        private List<CastInput> _resolvingCasts = new();

        // === 쿨타임 상태 ===
        private readonly Dictionary<SkillSlot, DelayId> _onCooldown = new();
        private readonly Dictionary<SkillSlot, CastCommit> _pending = new();
        private readonly Dictionary<SkillSlot, long> _lastCommitTick = new();
        public bool IsPending(SkillSlot slot) => _pending.ContainsKey(slot);
        private readonly Context _context;
        private readonly CommandCollector _collector;
        private readonly Transform _caster;
        private readonly Dictionary<SkillSlot, SkillBinding> _skills;
        private long _generation;
        private bool _suspended, _processing, _disposed;
        public Attacker(Context context, Transform caster, Dictionary<SkillSlot, SkillBinding> skills, CommandCollector collector)
        {
            _context = context;
            _caster = caster;
            _skills = skills ?? new Dictionary<SkillSlot, SkillBinding>();
            _collector = collector;
            _context.RegisterAttacker(this);
            _context.Logger.Info($"Attack controller initialised with {_skills.Count} skills.");
        }
        /// <summary>
        /// Tick 경계에서 Cast 요청을 처리한다.
        /// - Cast 요청 버퍼 swap
        /// - 쿨타임 정리
        /// - TryCast 실행
        /// </summary>
        public void Tick(long tick, byte innoxiousCount)
        {
            if (_processing) return;
            _processing = true;
            try
            {
                // 1) Cast 요청 버퍼 swap
                (_collectingCasts, _resolvingCasts) = (_resolvingCasts, _collectingCasts);

                // 2) OnCooldown 정리 (TryCast 이전!)
                if (_onCooldown.Count > 0)
                {
                    var completedSlots = new List<SkillSlot>();

                    foreach (var kv in _onCooldown)
                    {
                        var delayId = kv.Value;
                        if (delayId.Equals(default))
                            continue;

                        if (_context.DelayScheduler.IsCompleted(delayId, tick))
                        {
                            _context.DelayScheduler.Remove(delayId);
                            completedSlots.Add(kv.Key);
                        }
                    }

                    // 완료된 슬롯 정리
                    foreach (var slot in completedSlots)
                    {
                        _onCooldown[slot] = default;
                    }
                }
                // 3) 이번 Tick에 요청된 Cast 처리
                while (_resolvingCasts.Count > 0)
                {
                    var input = _resolvingCasts[0];
                    _resolvingCasts.RemoveAt(0);
                    try { TryCast(input, innoxiousCount, tick); }
                    catch (Exception error)
                    {
                        AttackLog.Write(input.Trace, "InputException", $"slot={input.Slot} type={error.GetType().Name} message={error.Message}");
                        Debug.LogException(error);
                    }
                }
            }
            finally { _resolvingCasts.Clear(); _processing = false; }
        }

        public void Suspend(string reason) { _suspended = true; CancelPending(reason); }
        public void Resume() { if (!_disposed) _suspended = false; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; Suspend("OwnerDestroyed");
            // 비활성화 때는 CD를 유지하지만, 영구 파괴된 소유자의 예약은 더 이상 조회되지 않습니다.
            foreach (var delay in _onCooldown.Values) _context.DelayScheduler?.Remove(delay);
            _onCooldown.Clear(); _lastCommitTick.Clear();
        }
        public void CancelPending(string reason)
        {
            _generation++;
            _collectingCasts.Clear(); _resolvingCasts.Clear();
            if (_collector) _collector.CancelPending(_caster, reason);
            // Collector가 파괴되거나 현재 명령을 큐에서 분리 중이어도 잔여 루트 핸들을 정리합니다.
            foreach (var commit in _pending.Values.ToArray())
            {
                _context.DelayScheduler?.Remove(commit.DelayId);
                commit.Cancel(reason);
            }
        }
        private string ValidateLifetime(long generation, long collectorGeneration)
        {
            if (generation != _generation) return "OwnerGenerationChanged";
            if (_suspended || !_caster || !_context.Owner || !_context.Owner.isActiveAndEnabled) return "OwnerDisabled";
            if (_context.Stats == null || _context.Stats.IsDead) return "CasterDead";
            if (_context.Act != null && !_context.Act.CanAttack) return "AttackDisabled";
            if (!_collector || collectorGeneration != _collector.Generation || !_collector.CanAcceptRoot(_context.DelayScheduler))
                return "CollectorUnavailableOrGenerationChanged";
            return null;
        }

        
        /// <summary>
        /// 버튼 Up 시 호출되는 Cast 요청 수집 API.
        /// 즉시 실행하지 않으며, 다음 Tick에서 처리된다.
        /// </summary>
        public AttackTrace BeginInput(SkillSlot slot, AttackTrace trace = default)
        {
            if (trace.AttackId == 0) trace = AttackTrace.Root(_caster);
            AttackLog.Write(trace, "Input", $"slot={slot}");
            return trace;
        }
        public void EnqueueCast(SkillSlot slot) => EnqueueCast(slot, null, BeginInput(slot));
        public void EnqueueCast(SkillSlot slot, ExplicitAttackTarget target, AttackTrace trace)
        {
            var collectorGeneration = _collector ? _collector.Generation : -1;
            var reason = ValidateLifetime(_generation, collectorGeneration);
            if (reason != null) { AttackLog.Write(trace, "InputRejected", $"reason={reason} slot={slot}"); return; }
            _collectingCasts.Add(new CastInput(slot, target, trace, _generation, collectorGeneration));
            AttackLog.Write(trace, "InputQueued", $"slot={slot} target={AttackLog.Identity(target?.Target)}");
        }

        public void EnqueueCast(SkillSlot slot, ExplicitAttackTarget target)
        {
            if (target != null) EnqueueCast(slot, target, BeginInput(slot));
        }


        private void TryCast(CastInput input, byte innoxiousCount, long tick)
        {
            // 검사 → Variable 캡처 → 명령 생성 → Target 준비 → 수락 순서를 유지합니다.
            if (!TryValidateRootRequest(input, innoxiousCount, tick, out var binding, out var cost, out var cooldown)) return;
            // 루트 선택 방식은 스킬의 계약입니다. 슬롯의 이전 mode 값은 사용하지 않습니다.
            var rootMode = binding.skillData.mechanismData.mode;
            if (input.Target != null && rootMode != TargetMode.TowardsEntity)
            { RejectCast(input, tick, "UnsupportedExplicitTargetMode"); return; }

            if (!TryCaptureRootVariable(binding.skillData.mechanismData.mechanism, out var variable, out var reason))
            { RejectCast(input, tick, reason); return; }
            var command = CreateRootCommand(input, binding, rootMode, variable);
            if (_collector == null) { RejectCast(input, tick, "MissingCollector"); return; }
            // 입력 준비 훅이 아니라 실제 입력 처리 경계에서 Target을 확정합니다.
            if (!_collector.TryPrepareRoot(command, out var prepared, out reason))
            { RejectCast(input, tick, reason); return; }
            TryAcceptPreparedRoot(input, tick, prepared, cost, cooldown);
        }

        private static void RejectCast(CastInput input, long tick, string reason)
            => AttackLog.Write(input.Trace, "CastRejected", $"reason={reason} slot={input.Slot} tick={tick}");

        // 같은 시점의 기존 검사도 삭제하지 않습니다. 거절 우선순위와 비용 캡처 시점을 보존합니다.
        private bool TryValidateRootRequest(CastInput input, byte innoxiousCount, long tick,
            out SkillBinding binding, out int cost, out short cooldown)
        {
            binding = default; cost = default; cooldown = default;
            var slot = input.Slot;
            void Reject(string reason) => RejectCast(input, tick, reason);
            var lifetimeReason = ValidateLifetime(input.Generation, input.CollectorGeneration);
            if (lifetimeReason != null) { Reject(lifetimeReason); return false; }
            if (innoxiousCount > 0)
            {
                Reject("CrowdControl");
                return false;
            } 
            
            if (_onCooldown.TryGetValue(slot, out var delayId) && !delayId.Equals(default))
            {
                Reject("Cooldown");
                // 쿨타임 중 → 발동 불가
                return false;
            }

            if (!_caster || !_context.Owner || !_context.Owner.isActiveAndEnabled || _context.Stats.IsDead)
            { Reject("CasterUnavailable"); return false; }
            if (_pending.ContainsKey(slot)) { Reject("PendingCast"); return false; }
            if (_lastCommitTick.TryGetValue(slot, out var lastTick) && lastTick == tick)
            { Reject("AlreadyCommittedThisTick"); return false; }
            if (!_skills.TryGetValue(slot, out binding))
            {
                AttackLog.Write(input.Trace, "CastRejected", "reason=MissingBinding");
                return false;
            }

            if (!binding.skillData || binding.skillData.mechanismData?.mechanism == null) { Reject("MissingSkillDataOrMechanism"); return false; }
            cost = binding.skillData.ManaCost;
            cooldown = binding.skillData.mechanismData.mechanism.CooldownTicks;
            if (cost < 0 || cooldown < 0) { Reject("InvalidCostOrCooldown"); return false; }
            if (tick < 0 || tick > long.MaxValue - binding.skillData.mechanismData.mechanism.DelayTicks)
            { Reject("InvalidCommitTick"); return false; }
            if (!_collector || !_collector.CanAcceptRoot(_context.DelayScheduler)) { Reject("CollectorUnavailable"); return false; }
            return true;
        }

        // 선택만 수행합니다. 정확한 수량 소비는 준비 후 수락 단계에 남깁니다.
        private bool TryCaptureRootVariable(INewMechanism mechanism, out SwitchVariable variable, out string reason)
        {
            variable = default; reason = null;
            if (mechanism is SwitchMechanism switcher)
            {
                foreach (var sw in switcher.cases ?? Array.Empty<SwitchCase>())
                {
                    if (sw?.variable == null) continue;
                    // Summon은 Attacker 생성 뒤 Storage를 등록하므로 생성 시점의 null을 캐시하지 않습니다.
                    if (_context.VariableStorage == null) { reason = "MissingVariableStorage"; return false; }
                    if (!_context.VariableStorage.Has(sw.variable)) continue;
                    if (_context.StackManager == null) { reason = "MissingStackManager"; return false; }
                    variable = _context.StackManager.CaptureVariable(sw.variable);
                    break;
                }
            }
            return true;
        }

        // 일반/Switch 모두 같은 명령을 구성하며 Variable만 다릅니다. DamageData는 지금 캡처합니다.
        private SkillCommand CreateRootCommand(CastInput input, SkillBinding binding, TargetMode rootMode, SwitchVariable variable)
        {
            return new SkillCommand(
                    caster: _caster,
                    mode: rootMode,
                    castPosition: FixedVector2.FromVector2(_caster.position),
                    mech: binding.skillData.mechanismData.mechanism,
                    damage: _context.Stats.DamageData(),
                    va: variable,
                    masker: binding.skillData.mechanismData.mechanism.Mask,
                    target: input.Target == null ? default : SkillTarget.ForEntity(input.Target.Target),
                    explicitTarget: input.Target, trace: input.Trace,
                    source: $"Skill:{binding.skillData.name}#{binding.skillData.GetInstanceID()}/slot:{input.Slot}/root"
                );
        }

        private void TryAcceptPreparedRoot(CastInput input, long tick, SkillCommand prepared, int cost, short cooldown)
        {
            var slot = input.Slot;
            void Reject(string reason) => RejectCast(input, tick, reason);
            // Target 준비 중 취소/비활성화가 재진입했다면 오래된 입력을 Commit하지 않습니다.
            var lifetimeReason = ValidateLifetime(input.Generation, input.CollectorGeneration);
            if (lifetimeReason != null) { Reject(lifetimeReason); return; }
            if (!_context.Stats.CanSpendMana(cost)) { Reject("InsufficientMana"); return; }
            SwitchSelection selection = null;
            if (prepared.Mech is SwitchMechanism switchMechanism)
            {
                selection = new SwitchSelection(switchMechanism, prepared.Var);
                if (!selection.CanAccept(prepared.Var)) { Reject("VariableUnavailable"); return; }
                prepared = prepared.WithSwitchSelection(selection);
            }
            var previousMana = _context.Stats.Stats.Mana;
            var hadLast = _lastCommitTick.TryGetValue(slot, out var previousLast);
            var hadCooldown = _onCooldown.TryGetValue(slot, out var previousCooldown);
            DelayId nextCooldown = default;
            StackManager.ConsumptionChange consumption = null;
            CastCommit commit = null;
            commit = new CastCommit(tick, prepared.Mech.DelayTicks, () =>
            {
                if (_pending.TryGetValue(slot, out var current) && ReferenceEquals(current, commit)) _pending.Remove(slot);
            });
            commit.Configure(prepared.Trace.CommandId, slot, cost, () => ValidateLifetime(input.Generation, input.CollectorGeneration));
            var committedCommand = prepared.WithCommit(commit);
            void ApplyResources()
            {
                if (cooldown > 0) nextCooldown = _context.DelayScheduler.Start(tick, cooldown);
                consumption = selection?.Begin(committedCommand.Var, tick);
                if (!_context.Stats.TrySpendMana(cost)) throw new InvalidOperationException("ManaChangedBeforeCommit");
                _onCooldown[slot] = nextCooldown; _pending.Add(slot, commit); _lastCommitTick[slot] = tick;
                // 이 뒤에는 실패 가능한 도메인 작업/사용자 콜백 없이 Commit을 공개합니다.
                consumption?.Complete();
            }
            void RollbackResources()
            {
                consumption?.Rollback();
                _context.Stats.Stats.RestoreManaBeforeCommit(previousMana);
                _context.DelayScheduler.Remove(nextCooldown); _pending.Remove(slot);
                if (hadCooldown) _onCooldown[slot] = previousCooldown; else _onCooldown.Remove(slot);
                if (hadLast) _lastCommitTick[slot] = previousLast; else _lastCommitTick.Remove(slot);
            }
            if (!_collector.TryAcceptRoot(committedCommand, ApplyResources, RollbackResources, out var reason)) Reject(reason);
        }

        /// <summary>
        /// 기존 입력 호출자를 위한 호환 진입점입니다. 실제 Cast 준비는 처리 Tick에서 수행합니다.
        /// </summary>
        public void PrepareCast(SkillSlot slot, byte innoxiousCount) => NotifyInputPrepared(slot, innoxiousCount);

        /// <summary>
        /// 입력 준비를 알리는 훅입니다. Target 선택, 자원 소비, 예약 및 Hold 시작은 수행하지 않습니다.
        /// </summary>
        private void NotifyInputPrepared(SkillSlot slot, byte innoxiousCount)
        {
            if (innoxiousCount > 0) return;
            if (!_skills.TryGetValue(slot, out var binding))
            {
                _context.Logger.Warn($"No skill bound to slot {slot}.");
                return;
            }

            // 빈 슬롯의 설정은 진단 후 반환합니다. 실행 요청의 검증은 TryCast가 별도로 담당합니다.
            if (!binding.skillData || binding.skillData.mechanismData?.mechanism == null)
            {
                _context.Logger.Warn($"Cannot prepare input for slot {slot}: MissingSkillDataOrMechanism.");
                return;
            }

            var mechanism = binding.skillData.mechanismData.mechanism;
            _context.Logger.Info($"Preparing {mechanism.GetType().Name} (cooldown {mechanism.CooldownTicks}).");
        }
    }
}
