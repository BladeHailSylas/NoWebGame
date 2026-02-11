using System;
using System.Collections.Generic;
using Characters;
using JetBrains.Annotations;
using Moves;
using PlayerScripts.Acts;
using PlayerScripts.Core;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Stacks;
using Systems.Stacks.Definition;
using Systems.Stacks.Instances;
using Systems.Time;
using UnityEngine;
using Logger = PlayerScripts.Core.Logger;
using Random = System.Random;

namespace Systems.Data
{
    /// <summary>
    /// MonoBehaviour entry point that orchestrates all player-related modules. It
    /// centralises lifecycle management and bridges Unity callbacks into pure C#
    /// systems for maintainability.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyEntity : Entity, IEntity
    {
        [Header("Configuration")]
        [SerializeField] private CharacterSpec spec;
        [SerializeField] [CanBeNull] private TargetResolver targetResolver;
        [SerializeField] [CanBeNull] private CommandCollector commandCollector;
        private List<VariableDefinition> _characterVariables;
        private Logger _logger;
        private Context _context;
        private StatsBridge _statsBridge;
        private Mover _mover;
        private Attacker _attacker;
        private ActBridge _actBridge;
        private StackManager _stackManager;
        private InteractionFilter _filter;
        private Random _rand;
        private Vector2 _vec;

        private void Awake()
        {
            _logger = new Logger(gameObject.name);
            targetResolver ??= GetComponent<TargetResolver>();
            commandCollector ??= GetComponent<CommandCollector>();

            if (!ValidateDependencies())
            {
                enabled = false;
                return;
            }

            _rand = new Random();

            _context = new Context(this, gameObject, transform, targetResolver, commandCollector, spec, _logger);
            var baseStats = new BaseStatsContainer(
                spec.baseHp,
                spec.baseHpGen,
                spec.baseMana,
                spec.baseManaGen,
                spec.baseAttack,
                spec.baseDefense,
                spec.baseSpeed
            );
            _statsBridge = new StatsBridge(_context, baseStats);
            _mover = new Mover(_context, _statsBridge, GetComponent<Rigidbody2D>(), GetComponent<Collider2D>());
            _attacker = new Attacker(_context, transform, BuildSkillDictionary(), commandCollector);
            _actBridge = new ActBridge(_mover, _attacker);
            _context.RegisterScheduler(Time.Time.DelayScheduler);
            _context.RegisterStats(_statsBridge);
            _context.RegisterAct(_actBridge);
            _stackManager = new StackManager(_context, new VariableStorage());
            _context.RegisterStackManager(_stackManager);
            
            _filter = new InteractionFilter(this, _statsBridge, _stackManager);
        }

        private bool ValidateDependencies()
        {
            if (spec is not null) return true;
            _logger.Error("CharacterSpec reference missing.");
            return false;

        }

        private void OnEnable()
        {
            if (Ticker.Instance != null)
            {
                Time.Time.Ticker.OnTick += TickHandler;
            }
            else
            {
                _logger.Warn("Ticker instance missing. Movement tick updates disabled.");
            }
            _characterVariables = spec.CharacterVariables;
            InitStacks();
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
            _stackManager.Tick(tick);
            _actBridge.Tick(tick);
            _statsBridge.Tick(tick);
            switch (tick % 30)
            {
                case >= 15:
                    return;
                case 0:
                {
                    var movement = _rand.Next(8);
                    _vec = movement switch
                    {
                        0 => new Vector2(-1, 1).normalized,
                        1 => new Vector2(1, 0),
                        2 => new Vector2(1, 1).normalized,
                        3 => new Vector2(0, 1),
                        4 => new Vector2(-1, 0),
                        5 => new Vector2(-1, -1).normalized,
                        6 => new Vector2(0, -1),
                        7 => new Vector2(1, -1).normalized,
                        _ => Vector2.zero
                    };
                    break;
                }
                default:
                    //Dev(tick, _vec);
                    break;
            }
        }

        private void Dev(ushort tick, Vector2 vec)
        {
            _actBridge.SetMovementInput(vec);
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
            return new Dictionary<SkillSlot, SkillBinding>
            {
                { SkillSlot.Attack, spec.attack },
                { SkillSlot.Skill1, spec.skill1 },
                { SkillSlot.Skill2, spec.skill2 },
                { SkillSlot.Ultimate, spec.ultimate }
            };
        }

        private void InitStacks()
        {
            foreach (var stack in _characterVariables)
            {
                ApplyStack(new StackKey(stack), 0);
            }
        }

        /// <summary>
        /// Allows installers to inject a character specification before the player
        /// is initialised.
        /// </summary>
        public void InstallSpec(CharacterSpec newSpec)
        {
            spec = newSpec;
        }
        public void TakeDamage(DamageData damage)
        {
            _filter.FilterDamage(damage);
        }
        public void Die()
        {
            Debug.Log("Oof");
            gameObject.SetActive(false);
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