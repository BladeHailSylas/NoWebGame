using System;
using System.Collections.Generic;
using System.Linq;
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
using Systems.SubSystems;
using UIs;
using UnityEngine;
using Logger = PlayerScripts.Core.Logger;

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
        [Header("Temporary AI (검증용, 기존 Enemy는 기본 비활성)")]
        [SerializeField] private bool temporaryAIEnabled;
        [SerializeField, Min(1)] private int decisionInterval = 60;
        [SerializeField] private int randomSeed = 12345;
        [SerializeField] private bool logAIDecisions;
        [Tooltip("임시 AI의 선택 조건입니다. 기존 Mechanism의 물리 Mask/명중 필터를 변환하지 않습니다.")]
        [SerializeField] private TargetRelationMask aiTargetRelations = TargetRelationMask.Enemy;
        private List<VariableDefinition> _characterVariables;
        private Logger _logger;
        private Context _context;
        private StatsBridge _statsBridge;
        private Mover _mover;
        private Attacker _attacker;
        private ActBridge _actBridge;
        private StackManager _stackManager;
        private InteractionFilter _filter;
        private TemporaryEnemyBrain _brain;
        private Ticker _subscribedTicker;
        private bool _initialized;
        public int AIDecisionCount => _brain?.DecisionCount ?? 0;
        public Entity AITarget => _brain?.LastTarget;
        public Vector2 AIMovement => _brain?.Movement ?? Vector2.zero;
        public bool TemporaryAIEnabled
        {
            get => temporaryAIEnabled;
            set { temporaryAIEnabled = value; if (!value) StopAI(); }
        }

        // 서비스들의 OnEnable 이후 구성합니다. 비활성 오브젝트에 InstallSpec을 먼저 호출할 수 있습니다.
        private void Start()
        {
            _logger = new Logger(gameObject.name);
            targetResolver ??= GetComponent<TargetResolver>();
            commandCollector ??= GetComponent<CommandCollector>();
            if (!commandCollector) commandCollector = CommandCollector.Instance;

            if (!ValidateDependencies())
            {
                enabled = false;
                return;
            }

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
            _context.RegisterScheduler(Time.Time.DelayScheduler);
            _context.RegisterStats(_statsBridge);
            _stackManager = new StackManager(_context, new VariableStorage());
            _context.RegisterStackManager(_stackManager);
            _context.RegisterVariableStorage(_stackManager.Storage);
            _mover = new Mover(_context, _statsBridge, GetComponent<Rigidbody2D>(), GetComponent<Collider2D>());
            _attacker = new Attacker(_context, transform, BuildSkillDictionary(), commandCollector);
            _actBridge = new ActBridge(_mover, _attacker);
            _context.RegisterAct(_actBridge);
            
            _filter = new InteractionFilter(this, _statsBridge, _stackManager);
            gameObject.TryGetComponent<SpriteManager>(out var spriteManager);
            _context.RegisterSpriteManager(spriteManager);
            _initialized = true;
            _characterVariables = spec.CharacterVariables ?? new List<VariableDefinition>();
            InitStacks();
            OnEnable();
        }

        private bool ValidateDependencies()
        {
            if (!spec || !GetComponent<Rigidbody2D>() || !GetComponent<Collider2D>() ||
                Time.Time.Ticker == null || Time.Time.DelayScheduler == null)
            {
                _logger.Error("Enemy에는 CharacterSpec, Rigidbody2D, Collider2D와 먼저 활성화된 TickerRunner가 필요합니다.");
                return false;
            }
            return true;

        }

        private void OnEnable()
        {
            if (!_initialized || _subscribedTicker != null) return;
            _subscribedTicker = Time.Time.Ticker;
            if (_subscribedTicker != null) _subscribedTicker.OnTick += TickHandler;
        }

        private void OnDisable()
        {
            _actBridge?.ClearMovementInput();
            StopAI();
            if (_subscribedTicker != null) _subscribedTicker.OnTick -= TickHandler;
            _subscribedTicker = null;
        }

        private void TickHandler(long tick)
        {
            _stackManager.Tick(tick);
            if (temporaryAIEnabled)
            {
                if (_brain == null) StartAI();
                _brain?.Tick(tick);
            }
            else if (_brain != null) StopAI();
            _actBridge.Tick(tick);
            _statsBridge.Tick(tick);
        }

        private void StartAI()
        {
            string error = null;
            if (!commandCollector || !commandCollector.isActiveAndEnabled || decisionInterval < 1 ||
                !TargetRelationMasks.IsValid(aiTargetRelations) || !TemporaryAttackSupport.Validate(spec.attack, out error))
            {
                _logger.Warn("Temporary AI disabled: " + (error ?? "활성 CommandCollector/판단 간격/관계 Mask를 확인하세요."));
                temporaryAIEnabled = false;
                return;
            }
            _brain = new TemporaryEnemyBrain(this, _actBridge, spec.attack.skillData.mechanismData.mechanism,
                aiTargetRelations, randomSeed, decisionInterval, () => temporaryAIEnabled && isActiveAndEnabled, logAIDecisions);
        }

        private void StopAI()
        {
            _brain?.Stop();
            _brain = null;
        }

        /// <summary>검증 fixture에서 기존 에셋을 저장하지 않고 AI를 구성할 수 있습니다.</summary>
        public void ConfigureTemporaryAI(CommandCollector collector, int seed = 12345, int interval = 60,
            TargetRelationMask relations = TargetRelationMask.Enemy, bool log = false)
        {
            if (_initialized && commandCollector != collector)
                throw new InvalidOperationException("Collector는 Enemy 초기화 전에 주입해야 합니다.");
            StopAI();
            commandCollector = collector;
            randomSeed = seed;
            decisionInterval = interval;
            aiTargetRelations = relations;
            logAIDecisions = log;
            temporaryAIEnabled = true;
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
                if (!stack) continue;
                Debug.Log($"{name}: Applying stack {stack.name}");
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
