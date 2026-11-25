using global::NoWebGame.Entities.Core;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Actions;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Diagnostics.Runtime;
using global::NoWebGame.Entities.Relations;
using System;




using UnityEngine;

namespace NoWebGame.Prototypes.Enemy
{
    /// <summary>경로 탐색 없이 매 N틱에 공격 또는 8방향 배회를 선택하는 검증용 AI입니다.</summary>
    public sealed class TemporaryEnemyBrain
    {
        private readonly Entity _caster;
        private readonly ActBridge _act;
        private readonly INewMechanism _attack;
        private readonly TargetRelationMask _relations;
        private readonly System.Random _random;
        private readonly int _interval;
        private readonly Func<bool> _isEnabled;
        private readonly bool _log;
        private int _elapsed;
        private bool _running = true;
        public int DecisionCount { get; private set; }
        public Entity LastTarget { get; private set; }
        public Vector2 Movement { get; private set; }

        public TemporaryEnemyBrain(Entity caster, ActBridge act, INewMechanism attack,
            TargetRelationMask relations, int seed, int interval = 60, Func<bool> isEnabled = null, bool log = false)
        {
            if (interval < 1) throw new ArgumentOutOfRangeException(nameof(interval));
            _caster = caster;
            _act = act ?? throw new ArgumentNullException(nameof(act));
            _attack = attack ?? throw new ArgumentNullException(nameof(attack));
            _relations = relations;
            _random = new System.Random(seed);
            _interval = interval;
            _isEnabled = isEnabled;
            _log = log;
        }

        public void Tick(long tick)
        {
            if (!_running) return;
            if (!_caster || !_caster.isActiveAndEnabled || (_isEnabled != null && !_isEnabled()))
            { Stop(); return; }
            if (++_elapsed < _interval) return;
            _elapsed = 0;
            DecisionCount++;
            var policy = TargetPolicy.From(_attack);
            var relations = policy.IsRelative ? (policy.IsValid ? policy.Relations : TargetRelationMask.None) : _relations;
            LastTarget = EntityAttackSelection.FindNearest(_caster, relations, _attack.MinRange, _attack.MaxRange);
            if (LastTarget)
            {
                Movement = Vector2.zero;
                _act.ClearMovementInput();
                // 요청마다 대상과 이 AI 활성 주기를 보존합니다. 다음 판단으로 앞 요청을 덮어쓰지 않습니다.
                var request = new ExplicitAttackTarget(_caster, LastTarget, relations,
                    () => _running && (_isEnabled == null || _isEnabled()));
                AttackLog.Write(request.Trace, "AIDecision", $"action=Attack decision={DecisionCount} {request.Describe(_attack)}");
                _act.ExecuteAttack(SkillSlot.Attack, request);
            }
            else
            {
                Movement = _random.Next(8) switch
                {
                    0 => Vector2.up, 1 => Vector2.right, 2 => Vector2.down, 3 => Vector2.left,
                    4 => new Vector2(1, 1).normalized, 5 => new Vector2(1, -1).normalized,
                    6 => new Vector2(-1, -1).normalized, _ => new Vector2(-1, 1).normalized
                };
                _act.SetMovementInput(Movement);
            }
            if (_log) Debug.Log($"[TemporaryAI] tick={tick}, caster={_caster.name}, target={(LastTarget ? LastTarget.name : "none")}, mask={_relations}, move={Movement}");
        }

        public void Stop()
        {
            _running = false; // 이미 Collector에 들어간 이 활성 주기의 요청도 무효화합니다.
            _elapsed = 0;
            LastTarget = null;
            Movement = Vector2.zero;
            _act.ClearMovementInput();
        }
    }
}
