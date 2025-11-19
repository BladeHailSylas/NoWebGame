using System;
using Olds.Systems.Core.Logics;
using Systems.Data;
using UnityEngine;

namespace Olds.Systems.EntityWorld
{
	#region ===== Entity ID =====
	/// <summary>
	/// Represents a deterministic identifier for entities managed by <see cref="TheWorld"/>.
	/// Using a dedicated struct avoids accidental misuse of raw integers and keeps the
	/// simulation layer free from implicit conversions.
	/// </summary>
	[Serializable]
	public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
	{
		/// <summary>
		/// Sentinel value representing an invalid or unassigned entity reference.
		/// This maps to index 0 so that default(EntityId) is also treated as invalid.
		/// </summary>
		public static readonly EntityId Invalid = new(0);

		[SerializeField]
		private readonly ushort _value;
		[SerializeField] private readonly bool _isRoot; // Prevents compiler from auto-generating equality operators
		public EntityId(ushort value, bool isRoot = false)
		{
			_value = value;
			_isRoot = isRoot;
		}

		/// <summary>
		/// Raw identifier stored as a 1-based index for deterministic array access. Intended for serialization only.
		/// </summary>
		public ushort Value => _value;

		public bool IsValid => _value > 0;

		/// <summary>
		/// Converts the identifier into the zero-based index used by the world buffers.
		/// Throws if invoked on an invalid identifier to avoid silent underflow.
		/// </summary>
		public int ToIndex()
		{
			if (!IsValid)
			{
				throw new InvalidOperationException("Cannot convert an invalid EntityId to an index.");
			}

			return _value - 1;
		}

		/// <summary>
		/// Factory helper that converts a zero-based buffer index into an <see cref="EntityId"/>.
		/// Ensures we never overflow the ushort range when assigning identifiers.
		/// </summary>
		public static EntityId FromIndex(int index)
		{
			if (index < 0 || index >= ushort.MaxValue)
			{
				throw new ArgumentOutOfRangeException(nameof(index), index, "Entity index must map into the ushort range.");
			}

			return new EntityId((ushort)(index + 1));
		}

		public bool Equals(EntityId other)
		{
			return _value == other._value;
		}

		public override bool Equals(object obj)
		{
			return obj is EntityId other && Equals(other);
		}

		public override int GetHashCode()
		{
			return _value;
		}

		public int CompareTo(EntityId other)
		{
			return _value.CompareTo(other._value);
		}

		public static bool operator ==(EntityId left, EntityId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(EntityId left, EntityId right)
		{
			return !left.Equals(right);
		}

		public override string ToString()
		{
			return IsValid ? $"EntityId({_value})" : "EntityId(Invalid)";
		}
	}
	#endregion

	#region ===== Entity Data =====
	/// <summary>
	/// Atomic, fully deterministic state for a single simulation entity. The struct contains
	/// both persistent attributes (team, stats) and transient integrators (velocity, impulse).
	/// All numeric fields rely on fixed-point arithmetic to guarantee reproducibility across
	/// different hardware.
	/// </summary>
	[Serializable]
	public struct EntityData
	{
		/// <summary>
		/// Identifier assigned by <see cref="TheWorld"/>.
		/// </summary>
		[SerializeField]
		public EntityId id;

		/// <summary>
		/// Whether the slot is currently owned by a live entity.
		/// </summary>
		[SerializeField]
		public bool isActive;

		/// <summary>
		/// High-level classification used by systems that need to filter by archetype.
		/// This remains under <see cref="TheWorld"/>'s control so that entity ownership
		/// is centralized and deterministic.
		/// </summary>
		[SerializeField]
		public EntityType entityType;
	
		/// <summary>
		/// World-space position expressed in fixed units.
		/// </summary>
		[SerializeField]
		public FixedVector2 transform;
	
		/// <summary>
		/// Per-tick velocity delta in fixed units.
		/// </summary>
		[SerializeField]
		public FixedVector2 velocity;

		/// <summary>
		/// Additional forces accumulated during the tick. Cleared automatically once applied.
		/// </summary>
		[SerializeField]
		public FixedVector2 externalImpulse;

		/// <summary>
		/// Facing direction stored as milli-degrees (1000 equals one real degree).
		/// </summary>
		[SerializeField]
		public int facingMilliDegrees;

		/// <summary>
		/// Entity faction/team identifier. Systems use it for deterministic filtering.
		/// </summary>
		[SerializeField]
		public Team teamId;

		/// <summary>
		/// Hit points represented as fixed raw units (avoid floats for determinism).
		/// </summary>
		[SerializeField]
		public int hitPoints;

		/// <summary>
		/// Maximum hit points for clamping purposes.
		/// </summary>
		[SerializeField]
		public int maxHitPoints;

		/// <summary>
		/// Optional custom flags used by deterministic systems (bit-packed state machine).
		/// </summary>
		[SerializeField]
		public uint stateFlags;

		/// <summary>
		/// Size descriptor primarily consumed by deterministic collision systems.
		/// </summary>
		[SerializeField]
		public HitCircle collisionShape;

		/// <summary>
		/// User-defined metadata slot reserved for simulation subsystems.
		/// </summary>
		[SerializeField]
		public int customData;

		/// <summary>
		/// Tick index of the most recent deterministic update.
		/// </summary>
		[SerializeField]
		public int lastProcessedTick;

		/// <summary>
		/// Version counter incremented every time the entity is mutated. Enables rollback validation.
		/// </summary>
		[SerializeField]
		public ulong version;
	
	
		/// <summary>
		/// Factory helper that creates a clean entity template. Callers may further customize the
		/// struct before submitting it to <see cref="TheWorld.SpawnEntity"/>.
		/// </summary>
		public static EntityData CreateTemplate(FixedVector2 position, HitCircle collision, Team teamId = 0)
		{
			return new EntityData
			{
				id = EntityId.Invalid, //?
				isActive = false,
				entityType = EntityType.None,
				transform = position,
				velocity = new FixedVector2(0, 0),
				externalImpulse = new FixedVector2(0, 0),
				facingMilliDegrees = 0,
				teamId = teamId,
				hitPoints = 0,
				maxHitPoints = 0,
				stateFlags = 0,
				collisionShape = collision,
				customData = 0,
				lastProcessedTick = -1,
				version = 0
			};
		}

		/// <summary>
		/// Applies deterministic motion for one tick by consuming velocity and impulse.
		/// </summary>
		public void IntegrateMotion()
		{
			transform = transform + velocity + externalImpulse;
			externalImpulse = new FixedVector2(0, 0);
		}

		/// <summary>
		/// Increases or decreases health using fixed raw units. The method clamps values deterministically.
		/// </summary>
		public void ApplyDamage(int delta)
		{
			var newValue = hitPoints + delta;
			if (newValue < 0)
			{
				newValue = 0;
			}
			else if (maxHitPoints > 0 && newValue > maxHitPoints)
			{
				newValue = maxHitPoints;
			}
			hitPoints = newValue;
		}

		/// <summary>
		/// Utility that marks the entity as inactive without returning it to the pool. Used by snapshots.
		/// </summary>
		public void Deactivate()
		{
			isActive = false;
			version++;
		}

		public void ApplyTransform(FixedVector2 newTransform)
		{
			if (transform.Equals(newTransform)) return;
			transform = newTransform;
		}
	}
	#endregion
}