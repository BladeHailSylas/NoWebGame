using System.Collections.Generic;
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
        private List<SkillSlot> _collectingCasts = new();
        private List<SkillSlot> _resolvingCasts = new();

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

            foreach (var kvp in _skills)
            {
                var binding = kvp.Value;
                if (binding.mechanism is not INewMechanism)
                {
                    _context.Logger.Error($"Invalid mechanism in slot {kvp.Key}.");
                    continue;
                }

                if (binding.@params is null)
                {
                    _context.Logger.Error($"Invalid params in slot {kvp.Key}.");
                }
            }
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
            foreach (var slot in _resolvingCasts)
            {
                TryCast(slot, innoxiousCount, tick);
            }
            // 4) resolving 버퍼 정리
            _resolvingCasts.Clear();
        }

        
        /// <summary>
        /// 버튼 Up 시 호출되는 Cast 요청 수집 API.
        /// 즉시 실행하지 않으며, 다음 Tick에서 처리된다.
        /// </summary>
        public void EnqueueCast(SkillSlot slot)
        {
            _collectingCasts.Add(slot);
        }


        private void TryCast(SkillSlot slot, byte innoxiousCount, ushort tick)
        {
            if (innoxiousCount > 0)
            {
                return;
            } 
            
            if (_onCooldown.TryGetValue(slot, out var delayId) && !delayId.Equals(default))
            {
                _context.Logger.Warn($"Skill in slot {slot} is on cooldown.");
                // 쿨타임 중 → 발동 불가
                return;
            }
            if (!_skills.TryGetValue(slot, out var binding))
            {
                _context.Logger.Warn($"No skill bound to slot {slot}.");
                return;
            }

            if (binding.mechanism is not INewMechanism mech)
            {
                _context.Logger.Error($"Skill in slot {slot} has invalid mechanism.");
                return;
            }

            if (binding.@params is not { } param)
            {
                _context.Logger.Error($"Skill in slot {slot} has invalid params.");
                return;
            }

            SkillCommand cmd;
            if (binding.@params is SwitchParams switcher)
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
                    mech: mech,
                    @params: param,
                    damage: _context.Stats.DamageData(),
                    va : sv,
                    masker: binding.@params.Mask
                );
            }
            else {
                cmd = new SkillCommand(
                    caster: _caster,
                    mode: binding.mode,
                    castPosition: FixedVector2.FromVector2(_caster.position),
                    mech: mech,
                    @params: param,
                    damage: _context.Stats.DamageData(),
                    va: default,
                    masker: binding.@params.Mask
                );}
            _collector?.EnqueueCommand(cmd);
            _context.Logger.Info($"Casted skill from slot {slot} ({mech.GetType().Name}).");
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

            if (binding.mechanism is not INewMechanism mech)
            {
                _context.Logger.Error($"Skill in slot {slot} has invalid mechanism.");
                return;
            }

            if (binding.@params is not { } param)
            {
                _context.Logger.Error($"Skill in slot {slot} has invalid params.");
                return;
            }

            _context.Logger.Info($"Preparing {mech.GetType().Name} (cooldown {param.CooldownTicks}).");
        }
    }
}
