using System.Collections.Generic;
using Unity.VisualScripting;
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
            circle.SetCenter(new FixedVector2(centerPos));
        }
        else if (param.Area is BoxArea box)
        {
            Debug.Log("Box!");
        }
        else {
            Debug.LogWarning("[AreaMechanism] Unsupported area shape type — default radius used.");
            collider.radius = 1f;
        }

        // Add placeholder AreaEntity (to be implemented next)
        AreaEntity entity = areaObj.AddComponent<AreaEntity>();
        entity.Init(param.Area, ctx.Damage, param.OnAreaEnter, param.OnAreaExpire, param.lifeTick);
        //entity.Initialize(param.Damage, param.LayerMask);

        Debug.Log($"[AreaMechanism] Spawned area at {centerPos} with radius {collider.radius}");
    }
}

public class AreaParams : INewParams
{
    public short CooldownTicks { get; private set; }
    [SerializeReference] public IAreaShapes Area;
    public ushort lifeTick;
    public List<MechanismRef> OnAreaEnter;
    public List<MechanismRef> OnAreaExpire;
}

public interface IAreaShapes
{
    FixedVector2 CenterCoordinate { get; }
}

public class CircleArea : IAreaShapes
{
    public FixedVector2 CenterCoordinate { get; private set; }
    public float Radius;

    public void SetCenter(FixedVector2 center)
    {
        CenterCoordinate = center;
    }
    /*public CircleArea(FixedVector2 center, float radius)
    {
        CenterCoordinate = center;
        Radius = radius;
    }*/
}

public class BoxArea : IAreaShapes
{
    public FixedVector2 CenterCoordinate { get; private set; }
    public float Width;
    public float Height;
    public void SetCenter(FixedVector2 center)
    {
        CenterCoordinate = center;
    }
}