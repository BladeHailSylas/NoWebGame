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

        // Collider 생성 분기
        Collider2D collider;

        if (param.Area is CircleArea circle)
        {
            circle.SetCenter(new FixedVector2(centerPos));
            // 원형 콜라이더 생성
            var circleCol = areaObj.AddComponent<CircleCollider2D>();
            circleCol.isTrigger = true;
            circleCol.radius = circle.Radius / 1000f;
            collider = circleCol;
        }
        else if (param.Area is BoxArea box)
        {
            box.SetCenter(new FixedVector2(centerPos));
            // 박스형 콜라이더 생성
            var boxCol = areaObj.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(box.Height / 1000f, box.Width / 1000f);
            // AreaEntity가 Caster를 바라보도록 회전
            Vector2 areaPos = areaObj.transform.position;
            Vector2 casterPos = ctx.Caster.position;

            // Area → Caster 방향 벡터
            Vector2 dir = (casterPos - areaPos).normalized;

            // 방향 각도 계산
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 회전 적용
            areaObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            Debug.Log($"[AreaMechanism] BoxArea rotated toward caster ({angle:F1}°).");
            
            collider = boxCol;
        }
        else
        {
            Debug.LogWarning("[AreaMechanism] Unsupported area shape type — using default circle.");
            var circleCol = areaObj.AddComponent<CircleCollider2D>();
            circleCol.isTrigger = true;
            circleCol.radius = 1f;
            collider = circleCol;
        }

        // AreaEntity 구성
        AreaEntity entity = areaObj.AddComponent<AreaEntity>();
        entity.Init(param.Area, ctx.Damage, param.OnAreaEnter, param.OnAreaExpire, param.lifeTick);

        Debug.Log($"[AreaMechanism] Spawned area ({param.Area.GetType().Name}) at {centerPos} with collider {collider.GetType().Name}");
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
    public float Height;
    public float Width;
    public void SetCenter(FixedVector2 center)
    {
        CenterCoordinate = center;
    }
}