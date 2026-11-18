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
            public CastInput(SkillSlot slot, ExplicitAttackTarget target, AttackTrace trace) { Slot = slot; Target = target; Trace = trace; }
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
        public Attacker(Context context, Transform caster, Dictionary<SkillSlot, SkillBinding> skills, CommandCollector collector)
        {
            _context = context;
            _caster = caster;
            _skills = skills ?? new Dictionary<SkillSlot, SkillBinding>();
            _collector = collector;
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
            foreach (var input in _resolvingCasts)
            {
                TryCast(input, innoxiousCount, tick);
            }
            // 4) resolving 버퍼 정리
            _resolvingCasts.Clear();
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
            _collectingCasts.Add(new CastInput(slot, target, trace));
            AttackLog.Write(trace, "InputQueued", $"slot={slot} target={AttackLog.Identity(target?.Target)}");
        }

        public void EnqueueCast(SkillSlot slot, ExplicitAttackTarget target)
        {
            if (target != null) EnqueueCast(slot, target, BeginInput(slot));
        }


        private void TryCast(CastInput input, byte innoxiousCount, long tick)
        {
            var slot = input.Slot;
            void Reject(string reason) => AttackLog.Write(input.Trace, "CastRejected", $"reason={reason} slot={slot} tick={tick}");
            if (innoxiousCount > 0)
            {
                Reject("CrowdControl");
                return;
            } 
            
            if (_onCooldown.TryGetValue(slot, out var delayId) && !delayId.Equals(default))
            {
                Reject("Cooldown");
                // 쿨타임 중 → 발동 불가
                return;
            }

            if (!_caster || !_context.Owner || !_context.Owner.isActiveAndEnabled || _context.Stats.IsDead)
            { Reject("CasterUnavailable"); return; }
            if (_pending.ContainsKey(slot)) { Reject("PendingCast"); return; }
            if (_lastCommitTick.TryGetValue(slot, out var lastTick) && lastTick == tick)
            { Reject("AlreadyCommittedThisTick"); return; }
            if (!_skills.TryGetValue(slot, out var binding))
            {
                AttackLog.Write(input.Trace, "CastRejected", "reason=MissingBinding");
                return;
            }

            if (!binding.skillData || binding.skillData.mechanismData?.mechanism == null) { Reject("MissingSkillDataOrMechanism"); return; }
            var cost = binding.skillData.ManaCost;
            var cooldown = binding.skillData.mechanismData.mechanism.CooldownTicks;
            if (cost < 0 || cooldown < 0) { Reject("InvalidCostOrCooldown"); return; }
            if (tick < 0 || tick > long.MaxValue - binding.skillData.mechanismData.mechanism.DelayTicks)
            { Reject("InvalidCommitTick"); return; }
            if (!_collector || !_collector.CanAcceptRoot(_context.DelayScheduler)) { Reject("CollectorUnavailable"); return; }
            // 루트 선택 방식은 스킬의 계약입니다. 슬롯의 이전 mode 값은 사용하지 않습니다.
            var rootMode = binding.skillData.mechanismData.mode;
            if (input.Target != null && rootMode != TargetMode.TowardsEntity)
            {
                Reject("UnsupportedExplicitTargetMode");
                return;
            }

            SkillCommand cmd;
            if (binding.skillData.mechanismData.mechanism is SwitchMechanism switcher)
            {
                SwitchVariable sv = default;
                foreach (var sw in switcher.cases ?? Array.Empty<SwitchCase>())
                {
                    if (sw?.variable == null) continue;
                    // Summon은 Attacker 생성 뒤 Storage를 등록하므로 생성 시점의 null을 캐시하지 않습니다.
                    if (_context.VariableStorage == null) { Reject("MissingVariableStorage"); return; }
                    if (!_context.VariableStorage.Has(sw.variable)) continue;
                    if (_context.StackManager == null) { Reject("MissingStackManager"); return; }
                    sv = _context.StackManager.CaptureVariable(sw.variable);
                    break;
                }
                cmd = new SkillCommand(
                    caster: _caster,
                    mode: rootMode,
                    castPosition: FixedVector2.FromVector2(_caster.position),
                    mech: binding.skillData.mechanismData.mechanism,
                    damage: _context.Stats.DamageData(),
                    va : sv,
                    masker: binding.skillData.mechanismData.mechanism.Mask,
                    target: input.Target == null ? default : SkillTarget.ForEntity(input.Target.Target),
                    explicitTarget: input.Target, trace: input.Trace,
                    source: $"Skill:{binding.skillData.name}#{binding.skillData.GetInstanceID()}/slot:{slot}/root"
                );
            }
            else {
                cmd = new SkillCommand(
                    caster: _caster,
                    mode: rootMode,
                    castPosition: FixedVector2.FromVector2(_caster.position),
                    mech: binding.skillData.mechanismData.mechanism,
                    damage: _context.Stats.DamageData(),
                    va: default,
                    masker: binding.skillData.mechanismData.mechanism.Mask,
                    target: input.Target == null ? default : SkillTarget.ForEntity(input.Target.Target),
                    explicitTarget: input.Target, trace: input.Trace,
                    source: $"Skill:{binding.skillData.name}#{binding.skillData.GetInstanceID()}/slot:{slot}/root"
                );}
            if (_collector == null) { Reject("MissingCollector"); return; }
            // Hold/PrepareCast가 아니라 실제 입력 처리 경계에서 Target만 확정합니다.
            if (!_collector.TryPrepareRoot(cmd, out var prepared, out var reason)) { Reject(reason); return; }
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
            if (!_collector.TryAcceptRoot(committedCommand, ApplyResources, RollbackResources, out reason)) Reject(reason);
        }

        public void PrepareCast(SkillSlot slot, byte innoxiousCount)
        {
            // TODO:
            // HoldMechanism 도입 시:
            // - 쿨타임 중이면 홀드 진입 차단
            // - 여기서 홀드 시작 처리
            // 현재는 의도적으로 비워 둔다
            
            if (innoxiousCount > 0) return;
            if (!_skills.TryGetValue(slot, out var binding))
            {
                _context.Logger.Warn($"No skill bound to slot {slot}.");
                return;
            }

            _context.Logger.Info($"Preparing {binding.skillData.mechanismData.mechanism.GetType().Name} (cooldown {binding.skillData.mechanismData.mechanism.CooldownTicks}).");
        }
    }
}
