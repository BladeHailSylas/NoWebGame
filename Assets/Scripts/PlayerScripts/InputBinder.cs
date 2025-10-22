// PlayerAttackController.cs — Intent 기반 입력 파이프라인.
// 잠재적 문제: Runner가 존재하지 않으면 입력이 조용히 무시되므로, 에디터 툴에서 검증 루틴을 추가해야 합니다.
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using SkillInterfaces;

public class InputBinder : MonoBehaviour
{
	public CharacterSpec spec;
	private PlayerActController _actor;
	private PlayerAttackController _attacker;
	private InputSystem_Actions _controls;
	private Vector2 _inputVector;

	private void Awake()
	{
		_controls = new InputSystem_Actions();
		_actor = new PlayerActController(GetComponent<FixedMotor>(), GetComponent<Rigidbody2D>());
		_attacker = new PlayerAttackController();
	}

	private void OnEnable()
	{
		Debug.Log("Hello again");
		_controls.Enable();

		// Move
		_controls.Player.Move.performed += ctx => _inputVector = ctx.ReadValue<Vector2>();

		_controls.Player.Move.canceled += _ => _inputVector = Vector2.zero;

		// Attack
		//_controls.Player.Attack.performed += _ => _attacker.OnAttackPressed();
		//_controls.Player.Attack.canceled += _ => _attacker.OnAttackReleased();
		Ticker.Instance.OnTick += TickHandler;
	}

	private void TickHandler(ushort tick)
	{
		if (_inputVector.SqrMagnitude() > 1e-6f)
		{
			//Debug.Log($"Moving {_inputVector} at {tick}");
			_actor.MakeMove(new FixedVector2(_inputVector));
		}
	}
	private void OnDisable()
	{
		_controls?.Disable();
	}
}