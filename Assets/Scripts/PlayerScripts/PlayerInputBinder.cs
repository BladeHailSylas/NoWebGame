using SkillInterfaces;
using UnityEngine;

/// <summary>
/// Pure C# binder that stores the latest player input values and routes them to
/// the appropriate player modules. The actual Unity input callbacks are
/// invoked from <see cref="PlayerEntity"/>, keeping this component free from
/// Unity lifecycle dependencies for easier testing.
/// </summary>
[System.Obsolete]
public sealed class PlayerInputBinder
{
    private readonly PlayerActController _actor;
    private readonly PlayerAttackController _attacker;
    private Vector2 _inputVector;

    public PlayerInputBinder(PlayerActController actor, PlayerAttackController attacker)
    {
        _actor = actor;
        _attacker = attacker;
    }

    /// <summary>
    /// Records movement input from the player.
    /// </summary>
    public void SetMovementInput(Vector2 input)
    {
        _inputVector = input;
    }

    /// <summary>
    /// Clears the cached movement input, usually when the input action is cancelled.
    /// </summary>
    public void ClearMovementInput()
    {
        _inputVector = Vector2.zero;
    }

    /// <summary>
    /// Called every tick by <see cref="PlayerEntity"/> to process movement.
    /// </summary>
    public void Tick(ushort tick)
    {
        if (_inputVector.sqrMagnitude > 1e-6f)
        {
            //Debug.Log($"Sending {_inputVector}");
            _actor.MakeMove(new FixedVector2(_inputVector));
        }
    }

    /// <summary>
    /// Prepares an attack for the specified slot. Preparation is separated from
    /// execution so we can later inject UI feedback or casting bars.
    /// </summary>
    public void PrepareAttack(SkillSlot slot)
    {
        _attacker.PrepareCast(slot);
    }

    /// <summary>
    /// Attempts to execute the attack bound to the provided slot.
    /// </summary>
    public void ExecuteAttack(SkillSlot slot)
    {
        _attacker.TryCast(slot);
    }
}
