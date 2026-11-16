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
        private readonly Context _context;
        private readonly CommandCollector _collector;
        private readonly Transform _caster;
        private readonly Dictionary<SkillSlot, SkillBinding> _skills;
        private VariableStorage _storage;
        public Attacker(Context context, Transform caster, Dictionary<SkillSlot, SkillBinding> skills, CommandCollector collector)
        {
            _context = context;
            _caster = caster;
            _skills = skills ?? new Dictionary<SkillSlot, SkillBinding>();
            _collector = collector;
            _storage = _context.VariableStorage;
            _context.Logger.Info($"Attack controller initialised with {_skills.Count} skills.");
        }
        /// <summary>
        /// Tick 경계에서 Cast 요청을 처리한다.
        /// - Cast 요청 버퍼 swap
        /// - 쿨타임 정리
        /// - TryCast 실행
        /// </summary>
        public void Tick(ushort tick, byte innoxiousCount)
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


        private void TryCast(CastInput input, byte innoxiousCount, ushort tick)
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
            if (!_skills.TryGetValue(slot, out var binding))
            {
                AttackLog.Write(input.Trace, "CastRejected", "reason=MissingBinding");
                return;
            }

            if (!binding.skillData || binding.skillData.mechanismData?.mechanism == null) { Reject("MissingSkillDataOrMechanism"); return; }
            if (input.Target != null)
            {
                var reason = binding.mode != TargetMode.TowardsEntity ? "UnsupportedExplicitTargetMode" : input.Target.Validate(_caster, binding.skillData.mechanismData.mechanism);
                AttackLog.Write(input.Trace, "TargetValidation", $"result={reason} {input.Target.Describe(binding.skillData.mechanismData.mechanism)}");
                if (reason != "Allowed") { Reject(reason); return; }
            }

            SkillCommand cmd;
            if (binding.skillData.mechanismData.mechanism is SwitchMechanism switcher)
            {
                SwitchVariable sv = default;
                foreach (var sw in switcher.cases)
                {
                    if (!_storage.Has(sw.variable)) continue;
                    sv = _storage.GetVariable(sw.variable);
                    break;
                }
                cmd = new SkillCommand(
                    caster: _caster,
                    mode: binding.mode,
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
                    mode: binding.mode,
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
            _collector.EnqueueCommand(cmd);
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
