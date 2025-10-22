using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic 2D motor that mirrors the legacy KinematicMotor2D but relies solely
/// on the BattleCore fixed-point math layer. The class collects sweep requests,
/// resolves deterministic sliding, and synchronizes the Unity Transform only via
/// <see cref="CoreTransform.ApplyTo"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class CoreMotor : MonoBehaviour
{
        public static CoreMotor Instance { get; private set; }

        public CoreMotor()
        {
                Instance = this;
        }
        /// <summary>
        /// Represents a deterministic collision body registered in the local collision world.
        /// A body can either expose a static shape instance or a dynamic factory. The owner
        /// is stored for bookkeeping so <see cref="MoveResult.hitTransform"/> can be populated
        /// without touching Unity physics.
        /// </summary>
        public readonly struct CollisionBody
        {
                private readonly IHitShape _staticShape;
                private readonly Func<IHitShape> _shapeFactory;
                public readonly object Owner;

                public CollisionBody(IHitShape shape, object owner = null)
                {
                        _staticShape = shape;
                        _shapeFactory = null;
                        Owner = owner;
                }

                public CollisionBody(Func<IHitShape> factory, object owner = null)
                {
                        _staticShape = null;
                        _shapeFactory = factory ?? throw new ArgumentNullException(nameof(factory));
                        Owner = owner;
                }

                /// <summary>
                /// Enumerates every deterministic shape attached to this body. Composite shapes
                /// can expose multiple primitives by overriding <see cref="IHitShape.OverlapShapes"/>.
                /// </summary>
                public IEnumerable<IHitShape> EnumerateShapes()
                {
                        IHitShape root = _shapeFactory != null ? _shapeFactory() : _staticShape;
                        if (root == null)
                        {
                                yield break;
                        }

                        IHitShape[] shapes = root.OverlapShapes();
                        if (shapes == null)
                        {
                                yield break;
                        }
                        for (int i = 0; i < shapes.Length; i++)
                        {
                                IHitShape shape = shapes[i];
                                if (shape != null)
                                {
                                        yield return shape;
                                }
                        }
                }
        }

        [Header("Defaults")]
        [Tooltip("Default deterministic collision configuration.")]
        public CollisionPolicy defaultPolicy = new()
        {
                //Would LayerMasks be replaced after all?
                //Since our EntityData doesn't deal with LayerMasks, I think that's true
                //LayerMasks rely on the Unity Engine, so... All Your LayerMasks Are Belong To Us
                wallsMask = 1 << 0,
                enemyMask = 1 << 1,
                enemyAsBlocker = true,
                unitradius = 500,
                unitskin = 125,
                allowWallSlide = true
        };

        private readonly List<FixedVector2> _pendingMoves = new();
        private readonly Dictionary<int, List<CollisionBody>> _collisionRegistry = new();

        private CollisionPolicy _currentPolicy;
        private CoreTransform _coreTransform;
        private MoveResult _lastMoveResult;
        private int _lastProcessedTick;
        private bool _needsTransformSync;

        private static readonly FixedVector2 ZeroVector = new(0, 0);

        private void Awake()
        {
                BattleCore.Initialize();
                _currentPolicy = defaultPolicy;
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

        private void LateUpdate()
        {
                if (!_needsTransformSync)
                {
                        return;
                }

                _coreTransform.ApplyTo(transform);
                _needsTransformSync = false;
        }

        /// <summary>
        /// Registers a deterministic collider for a specific mask value. The caller is expected
        /// to provide mutually exclusive mask bits when possible.
        /// </summary>
        public void RegisterCollider(LayerMask mask, CollisionBody body)
        {
                if (mask.value == 0)
                {
                        return;
                }

                List<CollisionBody> list = GetOrCreate(mask.value);
                list.Add(body);
        }

        /// <summary>
        /// Removes every collider bound to the provided mask. This is primarily intended for
        /// dynamic scenarios where the world is rebuilt per tick.
        /// </summary>
        public void ClearColliders(LayerMask mask)
        {
                if (mask.value == 0)
                {
                        return;
                }

                if (_collisionRegistry.TryGetValue(mask.value, out var list))
                {
                        list.Clear();
                }
        }

        /// <summary>
        /// Resets and replaces the collider collection stored under <paramref name="mask"/>.
        /// </summary>
        public void SetColliders(LayerMask mask, IEnumerable<CollisionBody> bodies)
        {
                if (mask.value == 0)
                {
                        return;
                }

                List<CollisionBody> list = GetOrCreate(mask.value);
                list.Clear();
                if (bodies == null)
                {
                        return;
                }

                foreach (var body in bodies)
                {
                        list.Add(body);
                }
        }

        private List<CollisionBody> GetOrCreate(int maskValue)
        {
                if (!_collisionRegistry.TryGetValue(maskValue, out var list))
                {
                        list = new List<CollisionBody>();
                        _collisionRegistry[maskValue] = list;
                }

                return list;
        }

        private void HandleTick(ushort tick)
        {
                ProcessPendingMoves();
                _lastProcessedTick = tick;
        }

        public IDisposable With(in CollisionPolicy overridePolicy)
        {
                CollisionPolicy previous = _currentPolicy;
                _currentPolicy = overridePolicy;
                return new Scope(() => _currentPolicy = previous);
        }

        private sealed class Scope : IDisposable
        {
                private readonly Action _onDispose;

                public Scope(Action onDispose)
                {
                        _onDispose = onDispose;
                }

                public void Dispose()
                {
                        _onDispose?.Invoke();
                }
        }

        public void Move(FixedVector2 desiredDelta, ushort entityID)
        {
                _pendingMoves.Add(desiredDelta);
        }

        public MoveResult LastMoveResult => _lastMoveResult;

        public int LastProcessedTick => _lastProcessedTick;

        public CollisionPolicy CurrentPolicy => _currentPolicy;

        public CoreTransform CoreTransform => _coreTransform;

        private void ProcessPendingMoves()
        {
                if (_pendingMoves.Count == 0)
                {
                        _lastMoveResult = default;
                        return;
                }

                MoveResult aggregated = default;
                FixedVector2 totalActual = ZeroVector;
                FixedVector2 zeroNormal = ZeroVector;

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

                        if (!IsZero(step.hitNormal))
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
                        actualDelta = ZeroVector,
                        hitNormal = ZeroVector
                };

                if (IsZero(desiredDelta))
                {
                        return result;
                }

                FixedVector2 origin = _coreTransform.position;
                FixedVector2 remaining = desiredDelta;
                const int maxSlideIterations = 4;

                for (int iteration = 0; iteration < maxSlideIterations; iteration++)
                {
                        if (IsZero(remaining))
                        {
                                break;
                        }

                        FixedVector2 adjusted = remaining;
                        adjusted = RemoveNormalComponent(adjusted, _currentPolicy.wallsMask, ref result, treatAsBlocker: true, isEnemyMask: false);
                        adjusted = RemoveNormalComponent(adjusted, _currentPolicy.enemyMask, ref result, treatAsBlocker: _currentPolicy.enemyAsBlocker, isEnemyMask: true);

                        if (IsZero(adjusted))
                        {
                                break;
                        }

                        MoveDiscrete(adjusted);

                        FixedVector2 progressed = _coreTransform.position - origin;
                        if (progressed.RawX == desiredDelta.RawX && progressed.RawY == desiredDelta.RawY)
                        {
                                remaining = ZeroVector;
                                break;
                        }

                        FixedVector2 newRemaining = desiredDelta - progressed;
                        if (newRemaining.RawX == remaining.RawX && newRemaining.RawY == remaining.RawY)
                        {
                                break;
                        }

                        remaining = newRemaining;
                }

                result.actualDelta = _coreTransform.position - origin;
                return result;
        }

        private FixedVector2 RemoveNormalComponent(FixedVector2 vector, LayerMask mask, ref MoveResult result, bool treatAsBlocker, bool isEnemyMask)
        {
                if (IsZero(vector) || mask.value == 0)
                {
                        return vector;
                }

                HitCircle motorShape = new HitCircle(_coreTransform.position + vector, _currentPolicy.unitradius);

                foreach (CollisionBody body in EnumerateBodies(mask))
                {
                        foreach (IHitShape shape in body.EnumerateShapes())
                        {
                                if (!FixedCollision.CheckOverlap(motorShape, shape))
                                {
                                        continue;
                                }

                                ContactInfo? contact = FixedCollision.ComputeContact(motorShape, shape);
                                if (!contact.HasValue)
                                {
                                        continue;
                                }

                                FixedVector2 rawNormal = ResolveContactNormal(motorShape, shape, contact.Value.normal);
                                FixedVector2 normal = NormalizeVector(rawNormal);
                                if (IsZero(normal))
                                {
                                        continue;
                                }

                                if (isEnemyMask)
                                {
                                        result.hitEnemy = true;
                                }
                                else
                                {
                                        result.hitWall = true;
                                }

                                if (!result.hitTransform && body.Owner is Transform transformOwner)
                                {
                                        result.hitTransform = transformOwner;
                                }

                                if (IsZero(result.hitNormal))
                                {
                                        result.hitNormal = normal;
                                }

                                if (!treatAsBlocker)
                                {
                                        continue;
                                }

                                if (!isEnemyMask && !_currentPolicy.allowWallSlide)
                                {
                                        return ZeroVector;
                                }

                                FixedVector2 removal = ProjectOntoNormal(vector, normal);
                                vector -= removal;
                                motorShape.center = _coreTransform.position + vector;
                        }
                }

                return vector;
        }

        private void MoveDiscrete(FixedVector2 delta)
        {
                if (IsZero(delta))
                {
                        return;
                }

                _coreTransform.position += delta;
                _needsTransformSync = true;
        }

        public FixedVector2 DepenVector(LayerMask blockersMask, int maxIterations = 4, float skin = 0.125f, float minEps = 0.001f, float maxTotal = 0.5f)
        {
                if (blockersMask.value == 0)
                {
                        return ZeroVector;
                }

                HitCircle motorShape = new HitCircle(_coreTransform.position, _currentPolicy.unitradius);
                int skinRaw = ToRaw(skin);
                int minEpsRaw = ToRaw(minEps);
                int maxTotalRaw = ToRaw(maxTotal);

                FixedVector2 accum = ZeroVector;

                foreach (CollisionBody body in EnumerateBodies(blockersMask))
                {
                        foreach (IHitShape shape in body.EnumerateShapes())
                        {
                                if (!FixedCollision.CheckOverlap(motorShape, shape))
                                {
                                        continue;
                                }

                                ContactInfo? contact = FixedCollision.ComputeContact(motorShape, shape);
                                if (!contact.HasValue)
                                {
                                        continue;
                                }

                                FixedVector2 normal = NormalizeVector(ResolveContactNormal(motorShape, shape, contact.Value.normal));
                                if (IsZero(normal))
                                {
                                        continue;
                                }

                                int pushUnits = contact.Value.depth + skinRaw;
                                if (pushUnits <= 0)
                                {
                                        continue;
                                }

                                FixedVector2 push = ScaleByRatio(normal, pushUnits, FixedVector2.UnitsPerFloat);
                                accum += push;
                        }
                }

                if (accum.Magnitude <= minEpsRaw)
                {
                        return ZeroVector;
                }

                int accumMagnitude = accum.Magnitude;
                if (accumMagnitude > maxTotalRaw)
                {
                        accum = ScaleByRatio(accum, maxTotalRaw, accumMagnitude);
                }

                return accum;
        }

        public void Depenetration()
        {
                LayerMask blockersMask = _currentPolicy.wallsMask;
                if (_currentPolicy.enemyAsBlocker)
                {
                        blockersMask |= _currentPolicy.enemyMask;
                }

                const int maxIterations = 4;
                int minEpsRaw = ToRaw(0.001f);
                int maxTotalRaw = ToRaw(0.5f);

                FixedVector2 total = ZeroVector;

                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                        FixedVector2 mtd = DepenVector(blockersMask, maxIterations, 0.03125f, 0.001f, 0.5f);
                        if (mtd.Magnitude <= minEpsRaw)
                        {
                                break;
                        }

                        FixedVector2 prospective = total + mtd;
                        int prospectiveMagnitude = prospective.Magnitude;
                        if (prospectiveMagnitude > maxTotalRaw)
                        {
                                int remaining = maxTotalRaw - total.Magnitude;
                                if (remaining <= 0)
                                {
                                        break;
                                }

                                int mtdMagnitude = mtd.Magnitude;
                                if (mtdMagnitude == 0)
                                {
                                        break;
                                }

                                mtd = ScaleByRatio(mtd, remaining, mtdMagnitude);
                                prospective = total + mtd;
                        }

                        MoveDiscrete(mtd);
                        total = prospective;

                        if (total.Magnitude >= maxTotalRaw)
                        {
                                break;
                        }
                }

                if (!IsZero(total))
                {
                        _needsTransformSync = true;
                }
        }

        private IEnumerable<CollisionBody> EnumerateBodies(LayerMask mask)
        {
                if (mask.value == 0)
                {
                        yield break;
                }

                int maskValue = mask.value;
                foreach (var kvp in _collisionRegistry)
                {
                        if ((maskValue & kvp.Key) == 0)
                        {
                                continue;
                        }

                        List<CollisionBody> list = kvp.Value;
                        for (int i = 0; i < list.Count; i++)
                        {
                                yield return list[i];
                        }
                }
        }

        private static FixedVector2 ProjectOntoNormal(FixedVector2 vector, FixedVector2 normal)
        {
                long dot = Dot(vector, normal);
                if (dot <= 0)
                {
                        return ZeroVector;
                }

                long normalLengthSq = Dot(normal, normal);
                if (normalLengthSq == 0)
                {
                        return ZeroVector;
                }

                return ScaleByRatio(normal, dot, normalLengthSq);
        }

        private static FixedVector2 ResolveContactNormal(HitCircle self, IHitShape other, FixedVector2 candidate)
        {
                if (!IsZero(candidate))
                {
                        return candidate;
                }

                switch (other)
                {
                        case HitCircle circle:
                                return circle.center - self.center;
                        case HitBox box:
                                int clampedX = Clamp(self.center.RawX, box.MinX, box.MaxX);
                                int clampedY = Clamp(self.center.RawY, box.MinY, box.MaxY);
                                FixedVector2 closest = new FixedVector2(clampedX, clampedY);
                                FixedVector2 diff = self.center - closest;
                                if (!IsZero(diff))
                                {
                                        return diff;
                                }

                                int left = self.center.RawX - box.MinX;
                                int right = box.MaxX - self.center.RawX;
                                int down = self.center.RawY - box.MinY;
                                int up = box.MaxY - self.center.RawY;

                                int min = Math.Min(Math.Min(left, right), Math.Min(down, up));
                                if (min == left)
                                {
                                        return new FixedVector2(-FixedVector2.UnitsPerFloat, 0);
                                }
                                if (min == right)
                                {
                                        return new FixedVector2(FixedVector2.UnitsPerFloat, 0);
                                }
                                if (min == down)
                                {
                                        return new FixedVector2(0, -FixedVector2.UnitsPerFloat);
                                }

                                return new FixedVector2(0, FixedVector2.UnitsPerFloat);
                        default:
                                return candidate;
                }
        }

        private static FixedVector2 NormalizeVector(FixedVector2 vector)
        {
                int magnitude = vector.Magnitude;
                if (magnitude == 0)
                {
                        return ZeroVector;
                }

                return ScaleByRatio(vector, FixedVector2.UnitsPerFloat, magnitude);
        }

        private static FixedVector2 ScaleByRatio(FixedVector2 vector, long numerator, long denominator)
        {
                if (denominator == 0)
                {
                        return ZeroVector;
                }

                long rawX = vector.RawX * numerator / denominator;
                long rawY = vector.RawY * numerator / denominator;

                return new FixedVector2(ClampToInt(rawX), ClampToInt(rawY));
        }

        private static long Dot(FixedVector2 a, FixedVector2 b)
        {
                return (long)a.RawX * b.RawX + (long)a.RawY * b.RawY;
        }

        private static bool IsZero(FixedVector2 value)
        {
                return value.RawX == 0 && value.RawY == 0;
        }

        private static int ToRaw(float value)
        {
                double scaled = value * FixedVector2.UnitsPerFloat;
                long rounded = (long)Math.Round(scaled);
                return ClampToInt(rounded);
        }

        private static int Clamp(int value, int min, int max)
        {
                if (value < min)
                {
                        return min;
                }

                if (value > max)
                {
                        return max;
                }

                return value;
        }

        private static int ClampToInt(long value)
        {
                if (value > int.MaxValue)
                {
                        return int.MaxValue;
                }

                if (value < int.MinValue)
                {
                        return int.MinValue;
                }

                return (int)value;
        }

        /** Optional debug hook: assign a callback to visualize deterministic sweeps. */
        // public Action<FixedVector2, MoveResult> OnSweepResolved;
}
