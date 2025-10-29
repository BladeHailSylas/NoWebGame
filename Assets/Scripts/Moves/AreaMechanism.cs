using UnityEngine;

[CreateAssetMenu(menuName = "Skills/Mechanisms/Area")]
public class AreaMechanism : ObjectGeneratingMechanism
{
    public override void Execute(INewParams @params, Transform caster, Transform target)
    {
    }

    public override void Execute(CastContext ctx)
    {
        if (ctx.Params is not AreaParams param)
        {
            Debug.LogError("[AreaMechanism] Invalid parameter type.");
            return;
        }

        // Determine the center position
        var centerPos = ctx.Target?.position ?? ctx.Caster.position;

        // Spawn the area object
        GameObject areaObj = GenerateObject("AreaZone", centerPos);

        // Add collider for detection (temporary visualization)
        CircleCollider2D collider = areaObj.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;

        if (param.Area is CircleArea circle)
        {
            collider.radius = circle.Radius / 1000f; // Convert from deterministic unit
        }
        else
        {
            Debug.LogWarning("[AreaMechanism] Unsupported area shape type — default radius used.");
            collider.radius = 1f;
        }

        // Add placeholder AreaEntity (to be implemented next)
        //AreaEntity entity = areaObj.AddComponent<AreaEntity>();
        //entity.Initialize(param.Damage, param.LayerMask);

        Debug.Log($"[AreaMechanism] Spawned area at {centerPos} with radius {collider.radius}");
    }
}

public struct AreaParams : INewParams
{
    public short CooldownTicks { get; private set; }
    [SerializeReference] public IAreaShapes Area;

    public AreaParams(short cooldownTicks, IAreaShapes area)
    {
        CooldownTicks = cooldownTicks;
        Area = area;
    }
}

public interface IAreaShapes
{
    FixedVector2 CenterCoordinate { get; }
}

public struct CircleArea : IAreaShapes
{
    public FixedVector2 CenterCoordinate { get; private set; }
    public float Radius { get; private set; }

    public CircleArea(FixedVector2 center, float radius)
    {
        CenterCoordinate = center;
        Radius = radius;
    }
}

public struct BoxArea : IAreaShapes
{
    public FixedVector2 CenterCoordinate { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }

    public BoxArea(FixedVector2 center, float width, float height)
    {
        CenterCoordinate = center;
        Width = width;
        Height = height;
    }
}