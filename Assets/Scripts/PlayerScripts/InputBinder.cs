using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using SkillInterfaces;

public class InputBinder : MonoBehaviour
{
    [Header("Character Data")]
    public CharacterSpec spec;

    private PlayerActController _actor;
    private PlayerAttackController _attacker;
    private InputSystem_Actions _controls;
    private Vector2 _inputVector;

    private void Awake()
    {
        _controls = new InputSystem_Actions();

        //Prepare movement controller
        _actor = new PlayerActController(GetComponent<FixedMotor>(), GetComponent<Rigidbody2D>());

        //Prepare attack controller
        var resolver = GetComponent<TargetResolver>();
        if (resolver == null)
        {
            Debug.LogError("[InputBinder] TargetResolver missing on player object!");
            return;
        }

        // Build skill dictionary from CharacterSpec
        var skillDict = new Dictionary<SkillSlot, SkillBinding>
        {
            { SkillSlot.Attack, spec.attack },
            { SkillSlot.Skill1, spec.skill1 },
            { SkillSlot.Skill2, spec.skill2 },
            { SkillSlot.Ultimate, spec.ultimate }
        };

        _attacker = new PlayerAttackController(transform, resolver, skillDict, GetComponent<CommandCollector>());
    }

    private void OnEnable()
    {
        _controls.Enable();

        //Movement input
        _controls.Player.Move.performed += ctx => _inputVector = ctx.ReadValue<Vector2>();
        _controls.Player.Move.canceled += _ => _inputVector = Vector2.zero;
        //Attack input
        _controls.Player.Attack.performed += _ => _attacker.PrepareCast(SkillSlot.Attack);
        _controls.Player.Attack.canceled += _ => _attacker.TryCast(SkillSlot.Attack);
        _controls.Player.Skill1.canceled += _ => _attacker.TryCast(SkillSlot.Skill1);
        _controls.Player.Skill2.canceled += _ => _attacker.TryCast(SkillSlot.Skill2);
        _controls.Player.Ultimate.canceled += _ => _attacker.TryCast(SkillSlot.Ultimate);
        // You can add more when needed, e.g. Skill1, Skill2, Ultimate

        Ticker.Instance.OnTick += TickHandler;
    }

    private void TickHandler(ushort tick)
    {
        if (_inputVector.SqrMagnitude() > 1e-6f)
        {
            _actor.MakeMove(new FixedVector2(_inputVector));
        }
    }

    private void OnDisable()
    {
        _controls?.Disable();
        if (Ticker.Instance != null)
            Ticker.Instance.OnTick -= TickHandler;
    }
}
