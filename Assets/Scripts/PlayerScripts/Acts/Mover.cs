using PlayerScripts.Core;
using PlayerScripts.Stats;
using Systems.Data;
using Systems.SubSystems;
using UnityEngine;

namespace PlayerScripts.Acts
{
    /// <summary>
    /// Handles locomotion and vulnerability logic for the player character. The
    /// logic is kept free from MonoBehaviour dependencies so it can be simulated in
    /// unit tests.
    /// </summary>
    public sealed class Mover
    {
        private readonly Context _context;
        private readonly StatsBridge _stats;
        private readonly FixedMotor _motor;

        public Mover(Context context, StatsBridge stats, Rigidbody2D rb, Collider2D col)
        {
            _context = context;
            _stats = stats;
            //_motor = context.Motor;
            _motor = new FixedMotor(rb, col);
        }

        /// <summary>
        /// Moves the player in the provided direction. Movement honours the effect
        /// system, so immobilizing conditions are respected.
        /// </summary>
        public void MakeMove(FixedVector2 move)
        {
            /*if (!_effects.IsMovable)
        {
            _context.Logger.Warn("Movement prevented due to status effect.");
            return;
        }*/
            _motor.Depenetrate();
            var speed = _stats.Stats.Speed;
            //Debug.Log($"Got {move.Normalized * speed}");
            _motor.Move(move.Normalized * speed);
            _motor.Depenetrate();
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            //Future hook: integrate with locomotion buffer when implemented.
            _context.Logger.Info($"Knockback requested direction={direction}, force={force}.");
        }
    }
}
