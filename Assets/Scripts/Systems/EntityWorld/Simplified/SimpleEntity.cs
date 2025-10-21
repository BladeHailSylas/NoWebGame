#region ===== SimpleEntity =====

public interface IEntityData
{
    public EntityId ID { get; }
    public Team Team { get; }
    public EntityType Type { get; }
    public FixedVector2 Transform { get; }
}
public struct SimpleEntity
{
    public EntityId ID { get; private set; }
    public Team Team { get; private set; }
    public EntityType Type { get; private set; }
    public FixedVector2 Transform { get; private set; }
    public IHitShape Collider { get; private set; }
    public SimpleEntity(FixedVector2 initPosition)
    {
        ID = new EntityId(1);
        Team = Team.Me;
        Type = EntityType.Player;
        Transform = initPosition;
        Collider = new HitCircle();
    }

    public void MoveTransform(FixedVector2 delta)
    {
        Transform += delta;
    }
}
#endregion