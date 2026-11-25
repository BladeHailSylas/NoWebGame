using global::NoWebGame.Characters;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Combat.Stacks.Definitions;
using global::NoWebGame.Entities.Core;
using global::NoWebGame.Moves;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.UIs;
using global::NoWebGame.Combat.Actions;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Interaction;
using global::NoWebGame.Combat.Runtime;
using global::NoWebGame.Combat.Stacks.Runtime;
using global::NoWebGame.Combat.Stats;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Entities.Relations;
using global::NoWebGame.Movement;
using global::NoWebGame.Prototypes.Enemy;
using global::NoWebGame.Systems.Time;
using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;













using UnityEngine;
using Logger = global::NoWebGame.Combat.Runtime.Logger;

namespace NoWebGame.Entities.Enemy
{
    /// <summary>
    /// MonoBehaviour entry point that orchestrates all player-related modules. It
    /// centralises lifecycle management and bridges Unity callbacks into pure C#
    /// systems for maintainability.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Data", "Assembly-CSharp", "EnemyEntity")]
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
        [Header("활성화되면 자동으로 Triggerable을 중첩합니다.")]
        [SerializeField] private bool enableTriggerableStack = false;
        // Enemy마다 별도 런타임 정의를 소유합니다. 프로젝트 에셋으로 저장하지 않습니다.
        private TriggerableDefinition _temporaryTriggerableStack;
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
            _context.RegisterScheduler(global::NoWebGame.Systems.Time.Time.DelayScheduler);
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
                global::NoWebGame.Systems.Time.Time.Ticker == null || global::NoWebGame.Systems.Time.Time.DelayScheduler == null)
            {
                _logger.Error("Enemy에는 CharacterSpec, Rigidbody2D, Collider2D와 먼저 활성화된 TickerRunner가 필요합니다.");
                return false;
            }
            return true;

        }

        private void OnEnable()
        {
            if (!_initialized || _subscribedTicker != null) return;
            _attacker?.Resume();
            _subscribedTicker = global::NoWebGame.Systems.Time.Time.Ticker;
            if (_subscribedTicker != null) _subscribedTicker.OnTick += TickHandler;
        }

        private void OnDisable()
        {
            _attacker?.Suspend("OwnerDisabled");
            _actBridge?.ClearMovementInput();
            StopAI();
            if (_subscribedTicker != null) _subscribedTicker.OnTick -= TickHandler;
            _subscribedTicker = null;
        }

        private void OnDestroy()
        {
            try { _attacker?.Dispose(); }
            finally
            {
                try { _stackManager?.Dispose(); }
                finally { if (_temporaryTriggerableStack) Destroy(_temporaryTriggerableStack); }
            }
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
            if (enableTriggerableStack && tick % 60 == 0)
            {
                EnsureTemporaryTriggerableStack();
                // 요청당 +1 중첩/+1 payload. 큐 방식이므로 다음 정상 Tick에서 처리합니다.
                ApplyStack(new StackKey(_temporaryTriggerableStack, name, transform), tick,
                    amount: 1, metadata: new StackMetadata(1));
            }
        }

        private void EnsureTemporaryTriggerableStack()
        {
            if (_temporaryTriggerableStack) return;
            var damage = new TemporarySelfDamageMechanism
            {
                damageValue = 100,
                // 알려진 DR 정수 절삭/방어력의 영향을 배제하여 payload 피해를 확인합니다.
                type = DamageType.Fixed
            };
            damage.ConfigureRelativeTargets(TargetRelationMask.Self);
            _temporaryTriggerableStack = ScriptableObject.CreateInstance<TriggerableDefinition>();
            _temporaryTriggerableStack.name = "TemporarySelfDamageTrigger";
            _temporaryTriggerableStack.hideFlags = HideFlags.DontSave;
            _temporaryTriggerableStack.threshold = 3;
            _temporaryTriggerableStack.maxStacks = 3;
            _temporaryTriggerableStack.duration = 180;
            _temporaryTriggerableStack.effect = new MechanismData
            {
                mechanism = damage,
                mode = TargetMode.TowardsSelf
            };
        }

        /// <summary>
        /// 임시 검증용 Damage입니다. 공통 Trigger의 null Target 계약은 바꾸지 않고,
        /// 이 예시에서만 자신을 지정한 뒤 실제 DamageMechanism의 피해 처리를 호출합니다.
        /// </summary>
        private sealed class TemporarySelfDamageMechanism : DamageMechanism
        {
            protected override void ExecuteCore(CastContext ctx)
            {
                if (!ctx.Caster || !ctx.Caster.TryGetComponent<EnemyEntity>(out var owner)) return;
                var before = owner._statsBridge.Stats.Health;
                var selfContext = new CastContext(ctx.Mech, ctx.Caster, SkillTarget.ForEntity(owner),
                    ctx.Damage, ctx.Var, ctx.Mode, ctx.Exit, ctx.OriginCastPosition, ctx.Trace, ctx.Source, ctx.SwitchSelection);
                base.ExecuteCore(selfContext);
                Debug.Log($"[TemporaryTrigger] {owner.name}: payload={ctx.Damage.Attack}, HP={before}->{owner._statsBridge.Stats.Health}", owner);
            }
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
