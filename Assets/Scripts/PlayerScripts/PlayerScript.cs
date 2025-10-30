using System.Collections.Generic;
using EffectInterfaces;
using SkillInterfaces;
using StatsInterfaces;
using Unity.Properties;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MonoBehaviour entry point that orchestrates all player-related modules. It
/// centralises lifecycle management and bridges Unity callbacks into pure C#
/// systems for maintainability.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerScript : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private CharacterSpec spec;
    [SerializeField][System.Obsolete] private FixedMotor motor;
    [SerializeField] private TargetResolver targetResolver;
    [SerializeField] private CommandCollector commandCollector;

    private PlayerLogger _logger;
    private PlayerContext _context;
    private PlayerStatsBridge _statsBridge;
    private PlayerEffects _effects;
    private PlayerActController _actController;
    private PlayerAttackController _attackController;
    private InputBinder _inputBinder;
    private InputSystem_Actions _controls;

    private void Awake()
    {
        _logger = new PlayerLogger(gameObject.name);
        //motor ??= GetComponent<FixedMotor>();
        targetResolver ??= GetComponent<TargetResolver>();
        commandCollector ??= GetComponent<CommandCollector>();

        if (!ValidateDependencies())
        {
            enabled = false;
            return;
        }

        _context = new PlayerContext(this, gameObject, transform, targetResolver, commandCollector, spec, _logger);

        var baseStats = new BaseStatsContainer(
            spec.baseHp,
            spec.baseHpGen,
            spec.baseMana,
            spec.baseManaGen,
            spec.baseAttack,
            spec.baseDefense,
            spec.baseSpeed
        );

        _statsBridge = new PlayerStatsBridge(_context, baseStats);
        _effects = new PlayerEffects(_context);
        _actController = new PlayerActController(_context, _statsBridge, _effects, GetComponent<Rigidbody2D>(), GetComponent<Collider2D>());
        _attackController = new PlayerAttackController(_context, transform, BuildSkillDictionary(), commandCollector);
        _inputBinder = new InputBinder(_actController, _attackController);

        _context.RegisterStats(_statsBridge);
        _context.RegisterEffects(_effects);

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

        _inputBinder?.ClearMovementInput();

        if (Ticker.Instance != null)
        {
            Ticker.Instance.OnTick -= TickHandler;
        }
    }

    private void TickHandler(ushort tick)
    {
        _inputBinder.Tick(tick);
        _statsBridge.Tick(tick);
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        //Debug.Log($"Got {ctx.ReadValue<Vector2>()}");
        _inputBinder.SetMovementInput(ctx.ReadValue<Vector2>());
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        _inputBinder.ClearMovementInput();
    }

    private void OnAttackPrepared(InputAction.CallbackContext ctx)
    {
        _inputBinder.PrepareAttack(SkillSlot.Attack);
    }

    private void OnAttackReleased(InputAction.CallbackContext ctx)
    {
        _inputBinder.ExecuteAttack(SkillSlot.Attack);
    }

    private void OnSkill1Released(InputAction.CallbackContext ctx)
    {
        _inputBinder.ExecuteAttack(SkillSlot.Skill1);
    }

    private void OnSkill2Released(InputAction.CallbackContext ctx)
    {
        _inputBinder.ExecuteAttack(SkillSlot.Skill2);
    }

    private void OnUltimateReleased(InputAction.CallbackContext ctx)
    {
        _inputBinder.ExecuteAttack(SkillSlot.Ultimate);
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

    /// <summary>
    /// Allows installers to inject a character specification before the player
    /// is initialised.
    /// </summary>
    public void InstallSpec(CharacterSpec newSpec)
    {
        spec = newSpec;
    }

    public void TakeDamage(int damage, int apRatio = 0, DamageType type = DamageType.Normal)
    {
        _actController.TakeDamage(damage, apRatio, type);
    }

    public void ApplyEffect(EffectType effectType, GameObject effecter, float duration = float.PositiveInfinity, int amp = 0, string name = null)
    {
        _effects.ApplyEffect(effectType, effecter, duration, amp, name);
    }
}
