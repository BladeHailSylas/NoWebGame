using EffectInterfaces;
using SkillInterfaces;
using UnityEngine;

/// <summary>
/// Pure C# binder that stores the latest player input values and routes them to
/// the appropriate player modules. The actual Unity input callbacks are
/// invoked from <see cref="PlayerEntity"/>, keeping this component free from
/// Unity lifecycle dependencies for easier testing.
/// </summary>
public sealed class ActBridge
{
    private readonly Mover _mover;
    private readonly Attacker _attacker;
    private Vector2 _moveVector;
    private Vector2 _inputVector;
    private byte _innoxiousCount;
    private byte _immovableCount;
    public byte InnoxiousCount => _innoxiousCount;
    public bool CanAttack => _innoxiousCount == 0;
    public byte ImmovableCount => _immovableCount;
    public bool CanMove => _immovableCount == 0;
    
    public ActBridge(Mover mover, Attacker attacker)
    {
        _mover = mover;
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
        _moveVector = (_immovableCount == 0) ? _inputVector : Vector2.zero;
        if (_moveVector.sqrMagnitude > 1e-6f)
        {
            //Debug.Log($"Sending {_inputVector}");
            _mover.MakeMove(new FixedVector2(_moveVector));
            
        }
    }

    /// <summary>
    /// Prepares an attack for the specified slot. Preparation is separated from
    /// execution so we can later inject UI feedback or casting bars.
    /// </summary>
    public void PrepareAttack(SkillSlot slot)
    {
        if(_innoxiousCount > 0) _attacker.PrepareCast(slot);
        //Preparing might be allowed, but block for now
    }

    /// <summary>
    /// Attempts to execute the attack bound to the provided slot.
    /// </summary>
    public void ExecuteAttack(SkillSlot slot)
    {
        if (_innoxiousCount == 0)
        {
            _attacker.TryCast(slot);
        }
    }

    public void ApplyCC(CCData cc)
    {
        switch (cc.Type)
        {
            case EffectType.Rooted:
                _immovableCount += 1;
                break;
            case EffectType.Suppressed:
                _innoxiousCount += 1;
                break;
            case EffectType.Stunned:
            case EffectType.Tumbled:
                _innoxiousCount += 1;
                _immovableCount += 1;
                break;
        }
    }
    public void RemoveCC(CCData cc)
    {
        switch (cc.Type)
        {
            case EffectType.Rooted:
                _immovableCount = (byte)Mathf.Max(0, _immovableCount - 1);
                break;
            case EffectType.Suppressed:
                _innoxiousCount = (byte)Mathf.Max(0, _innoxiousCount - 1);
                break;
            case EffectType.Stunned:
            case EffectType.Tumbled:
                _innoxiousCount = (byte)Mathf.Max(0, _innoxiousCount - 1);
                _immovableCount = (byte)Mathf.Max(0, _immovableCount - 1);
                break;
        }
    }
    private void Immovable()
    {
        _immovableCount += 1;
    }

    private void Innoxious()
    {
        _innoxiousCount += 1;
        //Should cancel attacks(make CommandCollector cease all the commands)
    }
}

public readonly struct CCData
{
    public readonly EffectType Type;
    public readonly byte Value;

    public CCData(EffectType type, byte value)
    {
        Type = type;
        Value = value;
    }
}
