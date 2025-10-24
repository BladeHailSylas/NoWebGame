using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#region ===== Entity Definitions =====
/// <summary>
/// Pure deterministic system contract. Implementations must avoid random generators,
/// floating point math, or Unity API access to guarantee replay stability.
/// </summary>
public interface IWorldSystem
{
	string Name { get; }
	void Execute(TheWorld world);
}
public enum EntityType : byte
{
	None = 0,
	Player = 1,
	Projectile = 2,
	Walls = 3,
}
public enum Team : byte
{
	Me = 0,
	Ally = 1,
	Enemy = 2,
	Walls = 3,
	What = 255
}
/// <summary>
/// Serializable snapshot of the entire world state. Used for rollback, save/load,
/// or deterministic verification between peers.
/// </summary>
[Serializable]
public struct WorldSnapshot
{
	public ushort tick;
	public ulong worldVersion;
	public EntityData[] entities;
}
#endregion


/// <summary>
/// Deterministic container responsible for owning every <see cref="EntityData"/> instance.
/// The class enforces a strict update order, stable entity identifiers, and snapshot
/// capabilities that make lockstep or rollback netcode feasible.
/// </summary>
[Serializable]
public sealed class TheWorld
{
	private readonly List<EntityData> _entities = new();
	private readonly List<int> _freeIds = new();
	[NonSerialized] private readonly List<SystemRegistration> _systems = new();
	[SerializeField] private ulong worldVersion;

	/// <summary>
	/// Delegate used to customize freshly created entities before they are spawned.
	/// Keeping this callback deterministic allows callers to inject additional data
	/// without bypassing <see cref="TheWorld"/>'s ownership guarantees.
	/// </summary>
	public delegate void EntityConfigurator(ref EntityData entityData);

	public TheWorld()
	{
		Initialize();
	}
	public void Initialize()
	{
		_entities.Clear();
		_freeIds.Clear();
		_systems.Clear();
		ActiveEntityCount = 0;
		worldVersion = 0;
		Debug.Log("The World!");
	}
	public ushort CurrentTick { get; private set; }
	public ulong WorldVersion => worldVersion;
	public ushort ActiveEntityCount { get; private set; }
	
	/// <summary>
	/// Optional deterministic random access for the Unity bridge.
	/// The list reference must never be mutated externally.
	/// </summary>
	public IReadOnlyList<EntityData> Entities => _entities;

	/// <summary>
	/// Registers a deterministic system that will be executed each tick using the provided order.
	/// Lower order values run first. Registration is idempotent based on instance reference.
	/// </summary>
	public void RegisterSystem(IWorldSystem system, int order)
	{
		if (system == null)
		{
			throw new ArgumentNullException(nameof(system));
		}

		for (int i = 0; i < _systems.Count; i++)
		{
			if (ReferenceEquals(_systems[i].System, system))
			{
				return;
			}
		}

		_systems.Add(new SystemRegistration(order, system));
		_systems.Sort((a, b) => a.Order.CompareTo(b.Order));
	}

	/// <summary>
	/// Removes a previously registered system. Useful for deterministic scenario swapping.
	/// </summary>
	public bool UnregisterSystem(IWorldSystem system)
	{
		if (system == null)
		{
			return false;
		}

		for (int i = 0; i < _systems.Count; i++)
		{
			if (!ReferenceEquals(_systems[i].System, system))
			{
				continue;
			}
			_systems.RemoveAt(i);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Creates a new entity in a deterministic fashion and registers it with the world.
	/// Internally, this method leverages the <see cref="EntityData.CreateTemplate"/> helper
	/// to guarantee that every entity starts from the same zeroed baseline. Callers may
	/// optionally provide a configurator to fill in gameplay-specific fields before the
	/// entity is spawned.
	/// </summary>
	/// <param name="entityType">High-level classification for downstream systems.</param>
	/// <param name="position">Initial world transform expressed in fixed units.</param>
	/// <param name="collisionShape">Deterministic collision primitive assigned to the entity.</param>
	/// <param name="teamId">Owning team; defaults to the player's side for convenience.</param>
	/// <param name="configurator">Optional deterministic callback for additional initialization.</param>
	/// <returns>The <see cref="EntityId"/> assigned by the world.</returns>
	public EntityId CreateEntity(
		EntityType entityType,
		FixedVector2 position,
		HitCircle collisionShape,
		Team teamId = Team.Me,
		EntityConfigurator configurator = null)
	{
		var entity = EntityData.CreateTemplate(position, collisionShape, teamId);
		entity.entityType = entityType;

		configurator?.Invoke(ref entity);

		// Delegate to SpawnEntity so that identifier allocation remains centralized.
		return SpawnEntity(entity);
	}
	/** Future extension: expose overloads that accept prefab-like blueprints for richer authoring. */

	/// <summary>
	/// Spawns an entity by reserving a deterministic identifier and copying the template.
	/// The caller remains responsible for populating component fields before invoking this method.
	/// </summary>
	public EntityId SpawnEntity(EntityData template)
	{
                int index;
                if (_freeIds.Count > 0)
                {
                        int last = _freeIds.Count - 1;
                        index = _freeIds[last];
                        _freeIds.RemoveAt(last);
                }
                else
                {
                        index = _entities.Count;
                        if (index >= ushort.MaxValue)
                        {
                                // Guard against overflowing the ushort-backed EntityId range.
                                throw new InvalidOperationException("Entity capacity exceeded: cannot allocate more than ushort.MaxValue entries.");
                        }
                        _entities.Add(default);
                }

                template.id = EntityId.FromIndex(index);
                template.isActive = true;
                template.version++;
                template.lastProcessedTick = CurrentTick;
                _entities[index] = template;
                ActiveEntityCount++;
		worldVersion++;
		return template.id;
	}

	/// <summary>
	/// Marks an entity as inactive and recycles the identifier for future reuse.
	/// </summary>
	public bool DespawnEntity(EntityId id)
	{
                if (!TryGetEntity(id, out var entity) || !entity.isActive)
                {
                        return false;
                }

                entity.isActive = false;
                entity.version++;
                int index = id.ToIndex();
                _entities[index] = entity;
                ActiveEntityCount--;
                _freeIds.Add(index);
                worldVersion++;
                return true;
        }

	/// <summary>
	/// Attempts to fetch a copy of the entity. Use <see cref="WriteEntity"/> to commit mutations.
	/// </summary>
	public bool TryGetEntity(EntityId id, out EntityData entity)
	{
                if (!id.IsValid)
                {
                        entity = default;
                        return false;
                }

                int index = id.ToIndex();
                if ((uint)index >= (uint)_entities.Count)
                {
                        entity = default;
                        return false;
                }

                entity = _entities[index];
                return entity.isActive;
        }

	/// <summary>
	/// Commits an updated entity struct back into the world buffer. The identifier must remain unchanged.
	/// </summary>
	public void WriteEntity(EntityData entity)
	{
                if (!entity.id.IsValid)
                {
                        throw new ArgumentException("Entity must have a valid identifier before writing.", nameof(entity));
                }
                int index = entity.id.ToIndex();
                if ((uint)index >= (uint)_entities.Count)
                {
                        throw new IndexOutOfRangeException("Entity identifier exceeds buffer capacity.");
                }
                _entities[index] = entity;
                worldVersion++;
        }

	/// <summary>
	/// Executes the deterministic tick loop. Order of operations:
	/// 1) Integrate velocity for every active entity.
	/// 2) Invoke registered systems in ascending order.
	/// </summary>
	public void UpdateWorld() // Call once per tick by the Ticker
	{
		CurrentTick++;

                for (int i = 0; i < _entities.Count; i++)
                {
                        var entity = _entities[i];
			if (!entity.isActive)
			{
				continue;
			}

			entity.lastProcessedTick = CurrentTick;
			entity.IntegrateMotion();
			entity.version++;
			_entities[i] = entity;
		}

		for (int i = 0; i < _systems.Count; i++)
		{
			_systems[i].System.Execute(this);
		}

		worldVersion++;
	}

	/// <summary>
	/// Produces a deep copy snapshot of the world buffer for deterministic rollback.
	/// </summary>
	public WorldSnapshot CreateSnapshot()
	{
		return new WorldSnapshot
		{
			tick = CurrentTick,
			worldVersion = worldVersion,
			entities = _entities.ToArray()
		};
	}

	/// <summary>
	/// Restores world state from a snapshot generated by <see cref="CreateSnapshot"/>.
	/// </summary>
	public void ApplySnapshot(WorldSnapshot snapshot)
	{
		if (snapshot.entities == null)
		{
			throw new ArgumentException("Snapshot must contain entity data.", nameof(snapshot));
		}

		_entities.Clear();
		_entities.AddRange(snapshot.entities);
		_freeIds.Clear();
		for (int i = 0; i < _entities.Count; i++)
		{
			if (!_entities[i].isActive)
			{
				_freeIds.Add(i);
			}
		}

		CurrentTick = snapshot.tick;
		worldVersion = snapshot.worldVersion;
		ActiveEntityCount = CountActiveEntities();
	}

	/// <summary>
	/// Returns a deterministic enumeration of active entity identifiers.
	/// </summary>
	public IEnumerable<EntityId> EnumerateActiveEntities()
	{
                for (int i = 0; i < _entities.Count; i++)
                {
                        if (_entities[i].isActive)
                        {
                                yield return EntityId.FromIndex(i);
                        }
                }
	}

	/// <summary>
	/// Clears all entities and resets counters. Intended for deterministic tests.
	/// </summary>
	public void Clear()
	{
		_entities.Clear();
		_freeIds.Clear();
		ActiveEntityCount = 0;
		CurrentTick = 0;
		worldVersion = 0;
	}

	/// <summary>
	/// Unity bridge helper: fetches the latest entity copy for visualization without mutating state.
	/// </summary>
	public EntityData PeekEntity(EntityId id)
	{
		if (!TryGetEntity(id, out var entity))
		{
			throw new KeyNotFoundException($"Entity {id} is not active.");
		}
		return entity;
	}

	/// <summary>
	/// Calculates the deterministic number of active entities, used when restoring snapshots.
	/// </summary>
	private ushort CountActiveEntities()
	{
		ushort count = 0;
		foreach (var entity in _entities.Where(entity => entity.isActive))
		{
			count++;
		}
		return count;
	}

	/// <summary>
	/// Internal record describing a registered deterministic system.
	/// </summary>
	[Serializable]
	private readonly struct SystemRegistration
	{
		public readonly int Order;
		public readonly IWorldSystem System;

		public SystemRegistration(int order, IWorldSystem system)
		{
			Order = order;
			System = system;
		}
	}

	/** Optional debug hook: enable to dump the active entity list per tick.
	public bool VerboseLogging;
	*/
}
