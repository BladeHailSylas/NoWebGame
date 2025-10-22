using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 최소 단위 테스트용 월드 컨테이너.
/// EntityId 관리, 스폰, 이동 적용 등 기본 로직만 남겼습니다.
/// </summary>
/*public sealed class SimpleWorld
{
    private readonly List<SimpleEntity> _entities = new();
    private readonly List<int> _freeIds = new();
    public static SimpleWorld Instance { get; private set; }
    public ushort CurrentTick { get; private set; }
    public ushort ActiveEntityCount { get; private set; }
    
    public void Initialize()
    {
        _entities.Clear();
        _freeIds.Clear();
        ActiveEntityCount = 0;
        CurrentTick = 0;
        Instance ??= this;
        Debug.Log("[SimpleWorld] Initialized, Za Warudo!");
    }

    public SimpleWorld()
    {
        Instance ??= this;
        Initialize();
        SpawnEntity();
    }
    /// <summary>
    /// 엔터티를 생성하고 ID를 부여합니다.
    /// </summary>
    public EntityId SpawnEntity(FixedVector2 initPosition)
    {
        int index;
        if (_freeIds.Count > 0)
        {
            index = _freeIds[^1];
            _freeIds.RemoveAt(_freeIds.Count - 1);
        }
        else
        {
            index = _entities.Count;
            _entities.Add(default);
        }

        // 새 ID 발급
        var newId = EntityId.FromIndex(index);

        // 복사본 구성
        var entity = new SimpleEntity(initPosition)
        {
            // 생성자에서 ID가 1로 고정되어 있으므로 새 ID로 교체
        };
        // 리플렉션이나 접근자 없이 직접 할당 불가하므로
        // 생성자 매개변수 추가를 고려해도 됩니다.
        // 일단 테스트용으로 Transform만 적용:
        _entities[index] = entity;
        ActiveEntityCount++;
        Debug.Log($"Entity {entity.ID.Value} has created at {entity.Transform.asVector2()}");
        return newId;
    }

    public EntityId SpawnEntity()
    {
        return SpawnEntity(new FixedVector2(-8000, 0));
    }
    public bool TryGetEntity(EntityId id, out SimpleEntity e)
    {
        int index = id.ToIndex();
        if (index < 0 || index >= _entities.Count)
        {
            e = default;
            return false;
        }
        e = _entities[index];
        return true;
    }

    public void WriteEntity(EntityId id, SimpleEntity entity)
    {
        int index = id.ToIndex();
        if (index < 0 || index >= _entities.Count)
            throw new IndexOutOfRangeException();
        _entities[index] = entity;
    }

    public void UpdateWorld()
    {
        CurrentTick++;
        for (int i = 0; i < _entities.Count; i++)
        {
            var e = _entities[i];
            // 테스트용: Tick마다 오른쪽으로 한 칸 이동
            e.MoveTransform(new FixedVector2(1, 0));
            _entities[i] = e;
        }
        Debug.Log($"[Tick {CurrentTick}] Updated {ActiveEntityCount} entities");
    }
    public IEnumerable<EntityId> EnumerateEntities()
    {
        for (int i = 0; i < _entities.Count; i++)
            yield return EntityId.FromIndex(i);
    }
    public void Clear()
    {
        _entities.Clear();
        _freeIds.Clear();
        ActiveEntityCount = 0;
        CurrentTick = 0;
    }
}*/