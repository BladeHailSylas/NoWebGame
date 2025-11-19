using System.Collections.Generic;
using Characters;
using Moves;
using PlayerScripts.Acts;
using PlayerScripts.Skills;
using PlayerScripts.Stack;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.Stacks;
using Systems.Stacks.Definition;
using Systems.Ticker;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerScripts.Core
{
    /// <summary>
    /// MonoBehaviour entry point that orchestrates all player-related modules. It
    /// centralises lifecycle management and bridges Unity callbacks into pure C#
    /// systems for maintainability.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerEntity : Entity, IEntity
    {
        [Header("Configuration")]
        [SerializeField] private CharacterSpec spec;
        [SerializeField] private TargetResolver targetResolver;
        [SerializeField] private CommandCollector commandCollector;
        private List<VariableDefinition> characterVariables;
        private InputSystem_Actions _controls;
        private Logger _logger;
        private Context _context;
        private StatsBridge _statsBridge;
        private Mover _mover;
        private Attacker _attacker;
        private ActBridge _actBridge;
        private StackManager _stackManager;

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
            _stackManager = new StackManager(_context);

            _context.RegisterStats(_statsBridge);
            _context.RegisterAct(_actBridge);
            _context.RegisterStackManager(_stackManager);

            _controls = new InputSystem_Actions();
        }

        private bool ValidateDependencies()
        {
            if (spec == null)
            {
                _logger.Error("CharacterSpec reference missing.");
                return false;
            }

            if (targetResolver == null)
            {
                _logger.Error("TargetResolver component missing.");
                return false;
            }

            if (commandCollector == null)
            {
                _logger.Error("CommandCollector component missing.");
                return false;
            }

            return true;
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                return;
            }
            _controls.Enable();
            _controls.Player.Move.performed += OnMovePerformed;
            _controls.Player.Move.canceled += OnMoveCanceled;
            _controls.Player.Attack.performed += OnAttackPrepared;
            _controls.Player.Attack.canceled += OnAttackReleased;
            _controls.Player.Skill1.canceled += OnSkill1Released;
            _controls.Player.Skill2.canceled += OnSkill2Released;
            _controls.Player.Ultimate.canceled += OnUltimateReleased;
            if (Ticker.Instance != null)
            {
                Ticker.Instance.OnTick += TickHandler;
            }
            else
            {
                _logger.Warn("Ticker instance missing. Movement tick updates disabled.");
            }
            characterVariables = spec.CharacterVariables;
            InitStacks();
        }

        private void OnDisable()
        {
            if (_controls != null)
            {
                _controls.Player.Move.performed -= OnMovePerformed;
                _controls.Player.Move.canceled -= OnMoveCanceled;
                _controls.Player.Attack.performed -= OnAttackPrepared;
                _controls.Player.Attack.canceled -= OnAttackReleased;
                _controls.Player.Skill1.canceled -= OnSkill1Released;
                _controls.Player.Skill2.canceled -= OnSkill2Released;
                _controls.Player.Ultimate.canceled -= OnUltimateReleased;
                _controls.Disable();
            }

            _actBridge?.ClearMovementInput();

            if (Ticker.Instance != null)
            {
                Ticker.Instance.OnTick -= TickHandler;
            }
        }

        private void TickHandler(ushort tick)
        {
            _stackManager.Tick(tick);
            _actBridge.Tick(tick);
            _statsBridge.Tick(tick);
            if (tick % 240 is 0)
            {
                Dev(tick);
            }
        }

        private void Dev(ushort tick)
        {
            _stackManager.Storage.Tell();
            RemoveStack(new StackKey(characterVariables[0]), tick);
            _stackManager.Storage.Tell();
        }
        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            //Debug.Log($"Got {ctx.ReadValue<Vector2>()}");
            _actBridge.SetMovementInput(ctx.ReadValue<Vector2>());
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            _actBridge.ClearMovementInput();
        }

        private void OnAttackPrepared(InputAction.CallbackContext ctx)
        {
            _actBridge.PrepareAttack(SkillSlot.Attack);
        }

        private void OnAttackReleased(InputAction.CallbackContext ctx)
        {
            _actBridge.ExecuteAttack(SkillSlot.Attack);
        }

        private void OnSkill1Released(InputAction.CallbackContext ctx)
        {
            _actBridge.ExecuteAttack(SkillSlot.Skill1);
        }

        private void OnSkill2Released(InputAction.CallbackContext ctx)
        {
            _actBridge.ExecuteAttack(SkillSlot.Skill2);
        }

        private void OnUltimateReleased(InputAction.CallbackContext ctx)
        {
            _actBridge.ExecuteAttack(SkillSlot.Ultimate);
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
            foreach (var stack in characterVariables)
            {
                ApplyStack(new StackKey(stack), 0, stack.maxStacks);
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
            _statsBridge.TakeDamage(damage);
        }
        public void Die()
        {
            Debug.Log("Oof");
            gameObject.SetActive(false);
        }

        public void ApplyStack()
        {
            Debug.Log("Stakataka");
        }

        public new void ApplyStack(StackKey stackKey, ushort tick, int amount = 1)
        {
            _stackManager.ApplyStack(stackKey, amount, tick);
        }

        public new void RemoveStack(StackKey stackKey, ushort tick, int amount = 0)
        {
            //TODO: Get response from the CommandCollector and remove VariableStack
            if (stackKey.def is not VariableDefinition)
            {
                return;
            }
            _stackManager.DetachVariable(stackKey, tick, amount);
        }
    }

    public abstract class Entity : MonoBehaviour
    {
        public bool targetable = true;

        public void ApplyStack(StackKey key, ushort tick, int amount = 1)
        {
        }

        public void RemoveStack(StackKey key, ushort tick, int amount = 0)
        {
        }
    }
}