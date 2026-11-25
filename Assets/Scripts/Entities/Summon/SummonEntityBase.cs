using global::NoWebGame.Characters;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Entities.Core;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Combat.Actions;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Interaction;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Combat.Stats;
using global::NoWebGame.Movement;
using global::NoWebGame.Systems.Time;
using System.Collections.Generic;

using JetBrains.Annotations;










using UnityEngine;
using Logger = global::NoWebGame.Combat.Runtime.Logger;
using Time = global::NoWebGame.Systems.Time;

namespace NoWebGame.Entities.Summon
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves.ObjectEntity", "Assembly-CSharp", "SummonEntityBase")]
    public abstract class SummonEntityBase : Entity
    {
        [Header("Configuration")]
        [SerializeField] [CanBeNull] private TargetResolver targetResolver;
        [SerializeField] [CanBeNull] private CommandCollector commandCollector;
        protected Logger _logger;
        protected Context _context;
        protected StatsBridge _statsBridge;
        protected Mover _mover;
        protected Attacker _attacker;
        protected ActBridge _actBridge;
        protected StackManager _stackManager;
        protected InteractionFilter _filter;
        protected VariableStorage _storage;
        private Ticker _subscribedTicker;
        public int baseHp; public int baseHpGen; public int baseMana; public int baseManaGen; public int baseAttack; public int baseDefense; public int baseSpeed;
        protected void Awaken(SummonMechanism spec)
        {
            Debug.Log("SummonEntity Awaken... AYAYAY AYAYYYYYY");
            _logger = new Logger(gameObject.name);
            TryGetComponent<TargetResolver>(out var resolver);
            targetResolver ??= resolver;
            TryGetComponent<CommandCollector>(out var collector);
            commandCollector ??= collector;

            if (!ValidateDependencies())
            {
                enabled = false;
                return;
            }

            _context = new Context(this, gameObject, transform, targetResolver, commandCollector, null, _logger);
            var baseStats = new BaseStatsContainer(
                baseHp,
                baseHpGen,
                baseMana,
                baseManaGen,
                baseAttack,
                baseDefense,
                baseSpeed
            );
            _statsBridge = new StatsBridge(_context, baseStats);
            TryGetComponent<Rigidbody2D>(out var rb);
            TryGetComponent<Collider2D>(out var col);
            _mover = new Mover(_context, _statsBridge, rb, col);
            _attacker = new Attacker(_context, transform, BuildSkillDictionary(), commandCollector);
            _actBridge = new ActBridge(_mover, _attacker);
            _context.RegisterScheduler(global::NoWebGame.Systems.Time.Time.DelayScheduler);
            _context.RegisterStats(_statsBridge);
            _context.RegisterAct(_actBridge);
            _storage = new VariableStorage();
            _stackManager = new StackManager(_context, _storage);
            _context.RegisterVariableStorage(_storage);
            _context.RegisterStackManager(_stackManager);
            _filter = new InteractionFilter(this, _statsBridge, _stackManager);
        }

        private bool ValidateDependencies()
        {
            return true;
        }

        private void OnEnable()
        {
            if (_context != null) OnEnabled();
        }

        protected void OnEnabled()
        {
            _attacker?.Resume();
            if (_subscribedTicker != null) return;
            if (global::NoWebGame.Systems.Time.Time.Ticker != null && _subscribedTicker == null)
            {
                _subscribedTicker = global::NoWebGame.Systems.Time.Time.Ticker;
                _subscribedTicker.OnTick += TickHandler;
            }
            else
            {
                _logger.Warn("Ticker instance missing. Movement tick updates disabled.");
            }
        }

        private void OnDisable()
        {
            _attacker?.Suspend("OwnerDisabled");
            _actBridge?.ClearMovementInput();

            if (_subscribedTicker != null) _subscribedTicker.OnTick -= TickHandler;
            _subscribedTicker = null;
        }

        private void OnDestroy() => _attacker?.Dispose();

        private void TickHandler(long tick)
        {
            if (_statsBridge.IsDead)
            {
                Die();
            }
            _stackManager.Tick(tick);
            _actBridge.Tick(tick);
            _statsBridge.Tick(tick);
            if (tick % 60 is 0)
            {
                Dev(tick);
            }
        }

        private void Dev(long tick)
        {
            //Debug.Log($"Hello again at {tick}");
        }
        public new void ApplyStack(StackKey stackKey, long tick, int amount = 1, StackMetadata metadata = default)
        {
            if (metadata.Metadata is 0)
            {
                _stackManager.EnqueueStack(stackKey, amount);
            }
            else
            {
                _stackManager.EnqueueStack(stackKey, amount, metadata);
            }
        }

        private Dictionary<SkillSlot, SkillBinding> BuildSkillDictionary()
        {
            return new Dictionary<SkillSlot, SkillBinding>();
        }
        
        
        public new void TakeDamage(DamageData damage)
        {
            Debug.Log("Taking damage");
            _filter.FilterDamage(damage);
        }
        public new void Die()
        {
            Debug.Log("Oof");
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        public new void RemoveStack(StackKey stackKey, long tick, int amount = 0)
        {
            //TODO: Get response from the CommandCollector and remove VariableStack
            if (stackKey.def is not VariableDefinition)
            {
                return;
            }
            _stackManager.ConsumeVariable(stackKey, amount, tick);
        }
    }
}
