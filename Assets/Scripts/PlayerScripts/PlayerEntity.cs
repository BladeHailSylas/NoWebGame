using System.Collections.Generic;
using SkillInterfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

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
    [SerializeField][System.Obsolete] private FixedMotor motor;
    [SerializeField] private TargetResolver targetResolver;
    [SerializeField] private CommandCollector commandCollector;
    private InputSystem_Actions _controls;
    private PlayerLogger _logger;
    private PlayerContext _context;
    private PlayerStatsBridge _statsBridge;
    private PlayerEffect _effect;
    private PlayerActController _actController;
    private PlayerAttackController _attackController;
    private PlayerInputBinder _playerInputBinder;
    private PlayerStackManager _stackManager;

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
        _effect = new PlayerEffect(_context);
        _actController = new PlayerActController(_context, _statsBridge, _effect, GetComponent<Rigidbody2D>(), GetComponent<Collider2D>());
        _attackController = new PlayerAttackController(_context, transform, BuildSkillDictionary(), commandCollector);
        _playerInputBinder = new PlayerInputBinder(_actController, _attackController);
        _stackManager = new(_context);

        _context.RegisterStats(_statsBridge);
        _context.RegisterEffects(_effect);
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

        _playerInputBinder?.ClearMovementInput();

        if (Ticker.Instance != null)
        {
            Ticker.Instance.OnTick -= TickHandler;
        }
    }

    private void TickHandler(ushort tick)
    {
        _playerInputBinder.Tick(tick);
        _statsBridge.Tick(tick);
        _stackManager.Tick(tick);
        if (tick % 60 is 0)
        {
            Dev(tick);
        }
    }

    private void Dev(ushort tick)
    {
        if (StackRegistry.Instance.StackStorage.TryGetValue("아누비스신", out var stack))
        {
            ApplyStack(new StackKey(stack, gameObject.name), tick);
        }
    }
    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        //Debug.Log($"Got {ctx.ReadValue<Vector2>()}");
        _playerInputBinder.SetMovementInput(ctx.ReadValue<Vector2>());
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        _playerInputBinder.ClearMovementInput();
    }

    private void OnAttackPrepared(InputAction.CallbackContext ctx)
    {
        _playerInputBinder.PrepareAttack(SkillSlot.Attack);
    }

    private void OnAttackReleased(InputAction.CallbackContext ctx)
    {
        _playerInputBinder.ExecuteAttack(SkillSlot.Attack);
    }

    private void OnSkill1Released(InputAction.CallbackContext ctx)
    {
        _playerInputBinder.ExecuteAttack(SkillSlot.Skill1);
    }

    private void OnSkill2Released(InputAction.CallbackContext ctx)
    {
        _playerInputBinder.ExecuteAttack(SkillSlot.Skill2);
    }

    private void OnUltimateReleased(InputAction.CallbackContext ctx)
    {
        _playerInputBinder.ExecuteAttack(SkillSlot.Ultimate);
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

    public void ApplyStack(StackKey stackKey, ushort tick, int amount = 1)
    {
        _stackManager.ApplyStack(stackKey, amount, tick);
    }
}

public class Entity : MonoBehaviour
{
    public bool targetable = true;
}