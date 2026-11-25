using global::NoWebGame.Systems.Data;
using System;
using System.Collections.Generic;


namespace NoWebGame.Combat.Stats
{
    public sealed partial class StatsContainer
    {
        // 기존 public AP/DR 목록은 레거시 호출 전용입니다. Stack 기여를 중복 삽입하지 않습니다.
        private readonly HashSet<BuffContribution> _buffContributions = new();

        internal readonly struct BuffEffect
        {
            internal readonly int Attack, Armor, Speed;
            internal readonly EffectType Type;
            internal readonly byte Percentage;
            internal BuffEffect(EffectType type, int attack = 0, int armor = 0, int speed = 0, byte percentage = 0)
            { Type = type; Attack = attack; Armor = armor; Speed = speed; Percentage = percentage; }
        }

        /// <summary>Key의 한 활성 주기가 소유하는 실제 기여입니다. 정의나 요청 배치를 보관하지 않습니다.</summary>
        internal sealed class BuffContribution
        {
            internal readonly StatsContainer Owner;
            internal BuffEffect Effect;
            internal bool Active;
            internal long Revision;
            internal BuffContribution(StatsContainer owner) { Owner = owner; }
        }

        /// <summary>
        /// 계산/범위 검증은 준비 시 수행합니다. Commit에는 사용자 delegate나 관측 콜백이 없습니다.
        /// StackManager가 상태/예약을 함께 확정하기 전까지 정확한 옛 값으로 복구할 수 있습니다.
        /// </summary>
        internal sealed class BuffChange
        {
            private readonly StatsContainer _owner;
            internal BuffContribution Handle { get; }
            private readonly BuffEffect _before, _after;
            private readonly bool _wasActive, _willBeActive;
            private readonly long _revision;
            private readonly int _oldAttack, _oldArmor, _oldSpeed, _attack, _armor, _speed;
            private bool _committed, _finished;

            internal BuffChange(StatsContainer owner, BuffContribution previous, BuffEffect after, bool active)
            {
                _owner = owner;
                Handle = previous ?? new BuffContribution(owner);
                if (!ReferenceEquals(Handle.Owner, owner) || (previous != null && !Handle.Active))
                    throw new InvalidOperationException("Buff contribution owner or lifecycle mismatch");
                _before = Handle.Effect; _after = after; _wasActive = Handle.Active; _willBeActive = active;
                _revision = Handle.Revision;
                _oldAttack = owner.AttackDamage; _oldArmor = owner.Armor; _oldSpeed = owner.Speed;
                _attack = checked((int)((long)_oldAttack - _before.Attack + after.Attack));
                _armor = checked((int)((long)_oldArmor - _before.Armor + after.Armor));
                _speed = checked((int)((long)_oldSpeed - _before.Speed + after.Speed));
            }
            internal void Commit()
            {
                if (_finished || _committed) throw new InvalidOperationException("Buff change already used");
                if (Handle.Revision != _revision || Handle.Active != _wasActive ||
                    _owner.AttackDamage != _oldAttack || _owner.Armor != _oldArmor || _owner.Speed != _oldSpeed)
                    throw new InvalidOperationException("Buff state changed before commit");
                var nextRevision = checked(_revision + 1);
                // 새 항목의 할당 가능 작업을 먼저 끝내고, 아래 필드 갱신은 외부 호출 없이 수행합니다.
                if (_willBeActive) _owner._buffContributions.Add(Handle);
                else _owner._buffContributions.Remove(Handle);
                Handle.Effect = _after; Handle.Active = _willBeActive; Handle.Revision = nextRevision;
                _owner.AttackDamage = _attack; _owner.Armor = _armor; _owner.Speed = _speed;
                _committed = true;
            }
            internal void Complete() { _finished = true; }
            internal void Rollback()
            {
                if (_finished) return;
                if (_committed)
                {
                    if (Handle.Revision != _revision + 1) throw new InvalidOperationException("Cannot roll back a newer Buff change");
                    if (_wasActive) _owner._buffContributions.Add(Handle);
                    else _owner._buffContributions.Remove(Handle);
                    Handle.Effect = _before; Handle.Active = _wasActive; Handle.Revision = _revision;
                    _owner.AttackDamage = _oldAttack; _owner.Armor = _oldArmor; _owner.Speed = _oldSpeed;
                }
                _finished = true;
            }
        }

        internal BuffChange PrepareBuffChange(BuffContribution previous, EffectType type, byte value, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            // 종료 시 현재 정의를 재계산하지 않고 기록된 기여만 제거합니다.
            if (amount == 0) return new BuffChange(this, previous, default, false);
            var percentage = checked((long)value * amount);
            int Rounded(int basis) => checked((int)(checked((long)basis * percentage + 50) / 100));
            var effect = type switch
            {
                EffectType.DamageBoost => new BuffEffect(type, attack: Rounded(BaseAttackDamage)),
                EffectType.ArmorBoost => new BuffEffect(type, armor: Rounded(BaseArmor)),
                EffectType.Haste => new BuffEffect(type, speed: Rounded(BaseSpeed)),
                EffectType.Slow => new BuffEffect(type, speed: checked(-Rounded(BaseSpeed))),
                EffectType.APBoost or EffectType.DRBoost => new BuffEffect(type, percentage: (byte)Math.Clamp(percentage, 0L, 100L)),
                _ => throw new ArgumentException("Unsupported Buff type", nameof(type))
            };
            return new BuffChange(this, previous, effect, true);
        }

        // 비율 0도 유효한 기여입니다. 역나눗셈 대신 남은 소유 항목으로 다시 계산합니다.
        private double OwnedPercentageFactor(EffectType type)
        {
            var total = 1.0;
            foreach (var contribution in _buffContributions)
                if (contribution.Effect.Type == type) total *= 1 - contribution.Effect.Percentage / 100.0;
            return total;
        }

        internal void ReleaseBuffsForDisposal(IEnumerable<BuffContribution> contributions)
        {
            long attack = AttackDamage, armor = Armor, speed = Speed;
            foreach (var item in contributions)
                if (ReferenceEquals(item.Owner, this) && item.Active)
                { attack -= item.Effect.Attack; armor -= item.Effect.Armor; speed -= item.Effect.Speed; }
            // 기존 직접 효과 API는 unchecked int 연산입니다. 종료도 그 표현 범위를 유지합니다.
            AttackDamage = unchecked((int)attack); Armor = unchecked((int)armor); Speed = unchecked((int)speed);
            foreach (var item in contributions)
                if (ReferenceEquals(item.Owner, this) && item.Active)
                { _buffContributions.Remove(item); item.Active = false; item.Effect = default; }
        }

        private void GetResetBuffStats(out int attack, out int armor, out int speed)
        {
            long a = BaseAttackDamage, d = BaseArmor, s = BaseSpeed;
            foreach (var contribution in _buffContributions)
            {
                a = checked(a + contribution.Effect.Attack);
                d = checked(d + contribution.Effect.Armor);
                s = checked(s + contribution.Effect.Speed);
            }
            attack = checked((int)a); armor = checked((int)d); speed = checked((int)s);
        }
    }
}
