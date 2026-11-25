using global::NoWebGame.Entities.Core;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Data;
using System;


using UnityEngine;

namespace NoWebGame.Movement.Physics
{
    /// <summary>투사체의 Rigidbody 설정과 공통 이동 검사를 연결합니다.</summary>
    public sealed class ThinMotor
    {
        private readonly Rigidbody2D _rb;
        private readonly Collider2D _col;
        private FixedVector2 _pos;
        public LayerMask wallMask;
        public LayerMask enemyMask;
        public ThinMotor(Rigidbody2D rb, Collider2D col)
        {
            _rb = rb; _col = col;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0;
            _pos = new FixedVector2(rb.position);
        }
        public TravelMotion CreateTravel(CastContext ctx, int speed, bool penetrative, Action<Entity> onHit)
            => new(_rb, _col, ctx, speed, penetrative, wallMask, enemyMask, onHit);
        public TravelResult? Move(TravelMotion travel)
        {
            var result = travel.Advance();
            var direction = travel.RequestedPosition - _rb.position;
            if (direction.sqrMagnitude > 0) _rb.MoveRotation(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            _pos = new FixedVector2(travel.RequestedPosition);
            return result;
        }
    }
}
