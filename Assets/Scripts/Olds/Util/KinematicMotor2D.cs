using System;
using System.Collections.Generic;
using UnityEngine;

public struct MoveResult
{
        public FixedVector2 actualDelta;
        public bool hitWall, hitEnemy;
        public Transform hitTransform;
        public FixedVector2 hitNormal;

        /// <summary>
        /// Helper accessor for legacy call sites that expect a float Vector2 delta.
        /// </summary>
        public readonly Vector2 ActualDeltaVector => actualDelta.AsVector2;

        /// <summary>
        /// Helper accessor for legacy call sites that expect a float Vector2 normal.
        /// </summary>
        public readonly Vector2 HitNormalVector => hitNormal.AsVector2;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[Obsolete("Use FixedMotor instead.")]
public class KinematicMotor2D : MonoBehaviour
{
	[Header("Defaults")]
	public CollisionPolicy defaultPolicy = new()
	{
		wallsMask = 0,
		enemyMask = 0,
		enemyAsBlocker = true,
		unitRadius = 500,
		unitSkin = 125,
		allowWallSlide = true
	};
        private Rigidbody2D _rb;
        private Collider2D _col;
        /*** Migration note:
         * 1) Replace the obsolete Rigidbody2D usage with a deterministic CoreTransform source (e.g., inject via CoreTransform.FromTransform).
         * 2) Introduce an IHitShape implementation matching the current Collider2D to provide overlap queries without relying on Unity physics components.
         * 3) Redirect DepenVector/Depenetration logic to operate on the new IHitShape while mirroring the resulting CoreTransform back to the scene when required.
         */
	private CollisionPolicy _current;

	private readonly List<FixedVector2> _pendingMoves = new();
	private CoreTransform _coreTransform;
	private MoveResult _lastMoveResult;
	private int _lastProcessedTick;
	private bool _needsTransformSync;

	private void Awake()
	{
		_rb = GetComponent<Rigidbody2D>();
		_rb.bodyType = RigidbodyType2D.Kinematic;
		_rb.gravityScale = 0f;
		_current = defaultPolicy;
		_col = _rb.GetComponent<Collider2D>();
		Debug.Log(_col.isTrigger);

		_coreTransform = CoreTransform.FromTransform(transform);
		_needsTransformSync = true;
	}

	private void OnEnable()
	{
		Ticker.Instance.OnTick += HandleTick;
	}

	private void OnDisable()
	{
		Ticker.Instance.OnTick -= HandleTick;
	}

	private void HandleTick(ushort tick)
	{
		ProcessPendingMoves();
		_lastProcessedTick = tick;
	}

	private void LateUpdate()
	{
		if (!_needsTransformSync)
		{
			return;
		}

		// Mirror the deterministic CoreTransform to the Unity Transform every frame when movement occurred.
		_coreTransform.ApplyTo(transform);
		_needsTransformSync = false;
	}

	public IDisposable With(in CollisionPolicy overridePolicy)
	{
		var prev = _current;
		_current = overridePolicy;
		return new Scope(() => _current = prev);
	}

	private sealed class Scope : IDisposable
	{
		private readonly Action _onDispose;

		public Scope(Action action)
		{
			_onDispose = action;
		}

		public void Dispose()
		{
			_onDispose?.Invoke();
		}
	}

	/// <summary>
	/// Queue a deterministic movement request that will resolve on the next BattleCore tick.
	/// </summary>
	/// <param name="desiredDelta">Desired displacement expressed in fixed units.</param>
	public void Move(FixedVector2 desiredDelta)
	{
		_pendingMoves.Add(desiredDelta);
	}

	/// <summary>
	/// Last resolved move result (updated after each tick).
	/// </summary>
	public MoveResult LastMoveResult => _lastMoveResult;

	/// <summary>
	/// Tick index corresponding to <see cref="LastMoveResult"/>.
	/// </summary>
	public int LastProcessedTick => _lastProcessedTick;

        private FixedVector2 RemoveNormalComponent(FixedVector2 vector, LayerMask mask, ref MoveResult result)
        {
                // Bridge deterministic data to Unity physics by operating in float space locally.
                Vector2 vfinalFloat = vector.AsVector2;
                float magnitude = vfinalFloat.magnitude;
                if (magnitude <= 0f)
                {
                        return new FixedVector2(0, 0);
                }

                Vector2 origin = _coreTransform.position.AsVector2;
                Vector2 direction = vfinalFloat.normalized;
                var maskHit = Physics2D.CircleCastAll(origin, _current.unitRadius, direction, magnitude, mask);
                foreach (var hit in maskHit)
                {
                        if (!hit.collider)
                        {
                                continue;
                        }

                        if (mask == _current.enemyMask && !_current.enemyAsBlocker)
                        {
                                continue;
                        }

                        if (mask == _current.enemyMask)
                        {
                                result.hitEnemy = true;
                        }
                        else
                        {
                                result.hitWall = true;
                        }

                        result.hitTransform = hit.transform;
                        result.hitNormal = FixedVector2.FromVector2(hit.normal.normalized);

                        Vector2 nFloat = hit.normal.normalized;
                        float dot = Vector2.Dot(vfinalFloat, nFloat);
                        if (Mathf.Abs(dot) > 0f)
                        {
                                vfinalFloat -= dot * nFloat;
                        }
                }

                return FixedVector2.FromVector2(vfinalFloat);
        }

	private void ProcessPendingMoves()
	{
		if (_pendingMoves.Count == 0)
		{
			_lastMoveResult = default;
			return;
		}

                MoveResult aggregated = default;
                FixedVector2 totalActual = new(0, 0);
                FixedVector2 zeroNormal = new(0, 0);

		for (int i = 0; i < _pendingMoves.Count; i++)
		{
			FixedVector2 requested = _pendingMoves[i];
			MoveResult step = ExecuteSweep(requested);
			totalActual += step.actualDelta;
			aggregated.hitWall |= step.hitWall;
			aggregated.hitEnemy |= step.hitEnemy;
			if (!aggregated.hitTransform && step.hitTransform)
			{
				aggregated.hitTransform = step.hitTransform;
			}

                        if (!step.hitNormal.Equals(zeroNormal))
                        {
                                aggregated.hitNormal = step.hitNormal;
                        }
		}

		aggregated.actualDelta = totalActual;
		_lastMoveResult = aggregated;
		_pendingMoves.Clear();
	}

	private MoveResult ExecuteSweep(FixedVector2 desiredDelta)
	{
		MoveResult result = new()
		{
			actualDelta = new FixedVector2(0, 0)
		};

                Vector2 desiredFloat = desiredDelta.ToVector2();
                if (desiredFloat.sqrMagnitude <= 0f)
                {
                        return result;
                }

                FixedVector2 originFixed = _coreTransform.position;
                float remaining = desiredFloat.magnitude;
                Vector2 wishDirFloat = desiredFloat.normalized;

                const int kMaxSlideIters = 4;
                int iters = 0;

                while (remaining > 1e-5f && iters++ < kMaxSlideIters)
                {
                        Vector2 vfinalFloat = wishDirFloat * remaining;
                        FixedVector2 vfinal = FixedVector2.FromVector2(vfinalFloat);
                        vfinal = RemoveNormalComponent(vfinal, _current.wallsMask, ref result);
                        vfinal = RemoveNormalComponent(vfinal, _current.enemyMask, ref result);

                        MoveResult wallProbe = result;
                        FixedVector2 checkWalls = RemoveNormalComponent(vfinal, _current.wallsMask, ref wallProbe);
                        MoveResult enemyProbe = result;
                        FixedVector2 checkEnemies = RemoveNormalComponent(vfinal, _current.enemyMask, ref enemyProbe);
                        Vector2 vfinalCheck = vfinal.ToVector2();
                        if (vfinalCheck != checkWalls.ToVector2() || vfinalCheck != checkEnemies.ToVector2())
                        {
                                break;
                        }
                        else if (vfinalCheck.sqrMagnitude > 1e-6f)
                        {
                                wishDirFloat = vfinalCheck.normalized;
                                remaining = vfinalCheck.magnitude;
                        }
                        else
                        {
                                break;
                        }

                        MoveDiscrete(vfinal);
                        remaining = 0f;
                }

		result.actualDelta = _coreTransform.position - originFixed;
		return result;
	}

	private void MoveDiscrete(FixedVector2 delta)
	{
		Vector2 deltaVector = delta.ToVector2();
		if (deltaVector.sqrMagnitude <= 0f)
		{
			return;
		}

		_coreTransform.position += delta;
		_needsTransformSync = true;
	}

	public CollisionPolicy CurrentPolicy => _current;

	/// <summary>
	/// 현재 위치에서 Blocker(환경)들과의 겹침을 검사하여
	/// "한 번"의 최소 이동 벡터(MTD)를 계산해 반환합니다.
	/// - 실제 위치 이동은 하지 않습니다. (Depenetration()이 적용 담당)
	/// - 합성형(여러 침투벡터 합산) 방식으로 단일 MTD를 구합니다.
	/// </summary>
        public FixedVector2 DepenVector(LayerMask blockersMask, int maxIterations = 4, float skin = 0.125f, float minEps = 0.001f, float maxTotal = 0.5f)
        {
                if (_rb == null || _col == null)
                {
                        return new FixedVector2(0, 0);
                }

                ContactFilter2D filter = new() { useLayerMask = true };
                filter.SetLayerMask(blockersMask);
                filter.useTriggers = false;

                Collider2D[] hits = new Collider2D[16];
                int count = _col.Overlap(filter, hits);
                if (count <= 0)
                {
                        return new FixedVector2(0, 0);
                }

                Vector2 accumFloat = Vector2.zero;
                int validContacts = 0;

                for (int i = 0; i < count; i++)
                {
                        var other = hits[i];
			if (!other)
			{
				continue;
			}

			ColliderDistance2D d = _col.Distance(other);
                        if (!d.isOverlapped)
                        {
                                continue;
                        }

                        accumFloat += d.normal * d.distance;
                        validContacts++;
                }

                if (validContacts == 0)
                {
                        return new FixedVector2(0, 0);
                }

                float mag = accumFloat.magnitude;
                if (mag < minEps)
                {
                        return new FixedVector2(0, 0);
                }

                Vector2 mtdFloat = (accumFloat / mag) * (mag + skin);

                /*** Debug helper (disabled by default). Enable for MTV visualization. */
                //for (int i = 0; i < count; i++)
                //{
                //    var other = hits[i];
                //    if (!other) continue;
                //    var d = col.Distance(other);
                //    if (!d.isOverlapped) continue;
                //    Vector2 p = rb.position;
                //    Debug.DrawRay(p, d.normal * Mathf.Max(d.distance, 0.02f), Color.cyan, 0.02f);
                //}
                //Debug.DrawRay(rb.position, mtdFloat, new Color(1f, 0.5f, 0f), 0.02f);
                /***/

                return FixedVector2.FromVector2(mtdFloat);
        }

	/// <summary>
	/// DepenVector()를 반복 호출하여 실제로 위치 보정(MovePosition)을 수행합니다.
	/// - 최대 반복 4회
	/// - 스킨 0.03125
	/// - 문턱 0.001
	/// - 총 보정 상한 0.5m
	/// - 마스크: current.wallsMask | (current.enemyAsBlocker ? current.enemyMask : 0)
	/// </summary>
	public void Depenetration()
	{
		if (_rb == null || _col == null)
		{
			return;
		}

		LayerMask blockersMask = _current.wallsMask;
		if (_current.enemyAsBlocker)
		{
			blockersMask |= _current.enemyMask;
		}

		int maxIterations = 4;
		float skin = 0.125f;
		float minEps = 0.001f;
		float maxTotal = 0.5f;

                FixedVector2 total = new(0, 0);

                for (int it = 0; it < maxIterations; it++)
                {
                        FixedVector2 mtd = DepenVector(blockersMask, maxIterations, skin, minEps, maxTotal);
                        Vector2 mtdFloat = mtd.ToVector2();
                        if (mtdFloat.sqrMagnitude <= (minEps * minEps))
                        {
                                break;
                        }

                        Vector2 totalFloat = total.ToVector2();
                        if ((total + mtd).ToVector2().magnitude > maxTotal)
                        {
                                Vector2 dir = mtdFloat.normalized;
                                float remain = Mathf.Max(0f, maxTotal - totalFloat.magnitude);
                                mtd = FixedVector2.FromVector2(dir * remain);
                                mtdFloat = mtd.ToVector2();
                        }

                        _rb.MovePosition(_rb.position + mtdFloat);

                        total += mtd;

                        if (Mathf.Abs(maxTotal - total.ToVector2().magnitude) <= 1e-5f)
                        {
                                break;
                        }
                }

                _coreTransform.position = new FixedVector2(_rb.position);
		_needsTransformSync = true;

		/*** Optional debug ray (disabled by default). */
                //if (total.ToVector2().sqrMagnitude > 0f)
                //{
                //    Vector2 totalFloat = total.ToVector2();
                //    Debug.DrawRay(rb.position - totalFloat, totalFloat, Color.yellow, 0.05f);
                //}
                /***/
        }
}

/*
// 참고: "최심 침투 우선형" 알고리즘 개요(요구사항 4 — 실제 구현 X, 주석만)
//
// for (it=0; it<maxIterations; it++):
//   overlaps = OverlapCollider(...)
//   if overlaps.empty: break
//   pick = argmax(overlaps, by d.distance)   // 가장 깊은 침투 1개 선택
//   mtd  = pick.normal * (pick.distance + skin)
//   rb.MovePosition(rb.position + mtd)
//   // 다음 반복에서 겹침 재평가
//
// 장점: 안정적(최심 해소부터) / 단점: 반복 횟수가 늘 수 있음
*/
