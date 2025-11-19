using Systems.Data;
using UnityEngine;

namespace Moves.ObjectEntity
{
    public abstract class ObjectPrefab : MonoBehaviour
    {
        public string name;
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
}