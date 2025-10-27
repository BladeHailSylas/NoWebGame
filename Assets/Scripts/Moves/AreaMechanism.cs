using UnityEngine;

public class AreaMechanism : ScriptableObject
{
    
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