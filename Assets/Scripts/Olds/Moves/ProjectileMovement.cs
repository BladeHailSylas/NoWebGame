using System.Collections.Generic;
using Olds.Util;
using Systems.Data;
using UnityEngine;

namespace Olds.Moves
{
	public class ProjectileMovement : MonoBehaviour, IExpirable
	{
		MissileParams _p;
		Transform _owner, _target;
		Vector2 _dir;
		float _speed, _traveled, _life;
		public float Lifespan => _life;

		// ¡Ú ÀÌ¹Ì ¸ÂÃá ÄÝ¶óÀÌ´õ ÀçÅ¸°Ý ¹æÁö
		readonly HashSet<int> _hitIds = new();
		const float Skin = 0.01f; // Ãæµ¹¸éÀ» »ìÂ¦ ³Ñ¾î°¡µµ·Ï

		public void Init(MissileParams p, Transform owner, Transform target)
		{
			_p = p; this._owner = owner; this._target = target;
			Vector2 start = owner.position;
			var tgt = target ? (Vector2)target.position : start + Vector2.right;
			_dir = (tgt - start).normalized;
			_speed = _p.speed;

			var sr = gameObject.AddComponent<SpriteRenderer>();
			sr.sprite = GenerateDotSprite();
			sr.sortingOrder = 1000;
			transform.localScale = Vector3.one * (_p.radius * 2f);
			//Debug.Log($"target {this.target.name}");
		}

		void Update()
		{
			var dt = Time.deltaTime;
			_life += dt; if (_life > _p.maxLife) { Expire(); }

			// °¡¼Ó
			_speed = Mathf.Max(0f, _speed + _p.acceleration * dt);

			// Å¸±ê À¯È¿¼º È®ÀÎ + ÀçÅ¸±êÆÃ
			if (_target == null && _p.retargetOnLost)
				TryRetarget();

			Vector2 pos = transform.position;

			// ¿øÇÏ´Â ¹æÇâ(À¯µµ)
			//Vector2 desired = target ? ((Vector2)target.position - pos).normalized : dir;
			var desired = _target != null && _target.name == TargetingRuntimeUtil.AnchorName ? _dir : ((Vector2)_target.position - pos).normalized;
			var maxTurnRad = _p.maxTurnDegPerSec * Mathf.Deg2Rad * dt;
			_dir = Vector3.RotateTowards(_dir, desired, maxTurnRad, 0f).normalized;

			// === ÀÌµ¿/Ãæµ¹(¿©·¯ ¹ø) Ã³¸® ===
			var remaining = _speed * dt;

			while (remaining > 0f)
			{
				pos = transform.position;

				// 1) º® Ã¼Å©
				var wallHit = Physics2D.CircleCast(pos, _p.radius, _dir, remaining, _p.blockerMask);
				if (wallHit.collider)
				{
					// º®±îÁö ÀÌµ¿ ÈÄ ¼Ò¸ê
					Move(wallHit.distance);
					Expire();
					return;
				}

				// 2) Àû Ã¼Å©
				var enemyHit = Physics2D.CircleCast(pos, _p.radius, _dir, remaining, _p.enemyMask);
				if (enemyHit.collider)
				{
					var c = enemyHit.collider;

					// °°Àº ÄÝ¶óÀÌ´õ Áßº¹ Å¸°Ý ¹æÁö
					var id = c.GetInstanceID();
					if (!_hitIds.Contains(id))
					{
						// Ãæµ¹Á¡±îÁö ÀÌµ¿
						Move(enemyHit.distance);

						// ÇÇÇØ/³Ë¹é Àû¿ë
						if (c.TryGetComponent(out IVulnerable v))
							//v.TakeDamage(_p.damage, _p.apRatio);
							if (c.attachedRigidbody)
								c.attachedRigidbody.AddForce(_dir * _p.knockback, ForceMode2D.Impulse);

						_hitIds.Add(id); // ±â·Ï

						// °üÅë ºÒ°¡ÀÌ°Å³ª(=¸íÁß Áï½Ã ¼Ò¸ê) / Å¸±ê ±× ÀÚÃ¼¸é ¼Ò¸ê
						if (!_p.CanPenetrate || (_target != null && c.transform == _target))
						{
							Expire();
							return;
						}
					}
					else
					{
						// ÀÌ¹Ì ¸ÂÃá ´ë»óÀÌ¸é Ãæµ¹Á¡±îÁö´Â ±»ÀÌ ¾È ¸ØÃß°í Åë°ú Ã³¸®
						Move(enemyHit.distance);
					}

					// Ãæµ¹¸éÀ» »ìÂ¦ ³Ñ¾î°¡ ´ÙÀ½ Ä³½ºÆ®¿¡¼­ °°Àº ¸é¿¡ °É¸®Áö ¾Ê°Ô
					Move(Skin);

					// ÀÜ¿© °Å¸® °»½Å
					remaining -= enemyHit.distance + Skin;
					continue; // ´ÙÀ½ Ãæµ¹/ÀÌµ¿ Ã³¸®
				}

				// 3) Ãæµ¹ ¾øÀ¸¸é ³²Àº °Å¸®¸¸Å­ ÀÌµ¿ÇÏ°í Á¾·á
				Move(remaining);
				remaining = 0f;
			}

			// »ç°Å¸® Ã¼Å©
			if (_traveled >= _p.maxRange) Expire();
		}

		void Move(float d)
		{
			transform.position += (Vector3)(_dir * d);
			_traveled += d;
		}

		void TryRetarget()
		{
			var hits = Physics2D.OverlapCircleAll(transform.position, _p.retargetRadius, _p.enemyMask);
			var best = float.PositiveInfinity; Transform bestT = null;
			foreach (var h in hits)
			{
				var d = Vector2.SqrMagnitude((Vector2)h.bounds.center - (Vector2)transform.position);
				if (d < best) { best = d; bestT = h.transform; }
			}
			if (bestT) _target = bestT;
		}

		Sprite GenerateDotSprite()
		{
			var s = 8; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
			var col = new Color32[s * s]; for (var i = 0; i < col.Length; i++) col[i] = new Color32(255, 255, 255, 255);
			tex.SetPixels32(col); tex.Apply();
			return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
		}

		public void Expire()
		{
			//Á¦°ÅµÉ ¶§ ¹º°¡ ÇØ¾ß ÇÑ´Ù?
			Destroy(gameObject);
		}
	}
}