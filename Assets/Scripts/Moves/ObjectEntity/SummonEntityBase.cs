using System.Collections.Generic;
using Characters;
using JetBrains.Annotations;
using Moves.Mechanisms;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using Systems.Time;
using UnityEngine;
using Logger = PlayerScripts.Core.Logger;
using Time = Systems.Time;

namespace Moves.ObjectEntity
{
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
        public int baseHp; public int baseHpGen; public int baseMana; public int baseManaGen; public int baseAttack; public int baseDefense; public int baseSpeed;
        protected void Awaken(SummonParams spec)
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
            _context.RegisterScheduler(Time.Time.DelayScheduler);
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

        protected void OnEnabled()
        {
            if (Ticker.Instance != null)
            {
                Time.Time.Ticker.OnTick += TickHandler;
            }
            else
            {
                _logger.Warn("Ticker instance missing. Movement tick updates disabled.");
            }
        }

        private void OnDisable()
        {
            _actBridge?.ClearMovementInput();

            if (Ticker.Instance != null)
            {
                Time.Time.Ticker.OnTick -= TickHandler;
            }
        }

        private void TickHandler(ushort tick)
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

        private void Dev(ushort tick)
        {
            //Debug.Log($"Hello again at {tick}");
        }
        public new void ApplyStack(StackKey stackKey, ushort tick, int amount = 1, StackMetadata metadata = default)
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

        public new void RemoveStack(StackKey stackKey, ushort tick, int amount = 0)
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