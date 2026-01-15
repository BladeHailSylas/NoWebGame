using Systems.Data;
using UnityEngine;

namespace Moves.ObjectEntity
{
    public abstract class ObjectPrefab : MonoBehaviour
    {
        public new string name;
    }
    public interface IAreaShapes
    {
        FixedVector2 CenterCoordinate { get; }
    }
    public interface IBoxLikeArea : IAreaShapes
    {
        /// <summary>
        /// OverlapBoxAll에 전달할 크기 (월드 단위)
        /// </summary>
        Vector2 GetBoxSize();

        /// <summary>
        /// OverlapBoxAll에 전달할 회전값 (degree)
        /// </summary>
        float GetRotation();
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

    public class BoxArea : IBoxLikeArea
    {
        public FixedVector2 CenterCoordinate { get; private set; }
        public int Width;
        public int Height;
        public float RotationZ;

        public Vector2 GetBoxSize() => new Vector2(Width, Height) / 1000f;

        public float GetRotation() => RotationZ;

        public void SetCenter(FixedVector2 center)
        {
            CenterCoordinate = center;
        }

        public void SetCenter(Vector2 center)
        {
            CenterCoordinate = new FixedVector2(center);
        }
    }
    public class LaserArea : IBoxLikeArea
    {
        public int Width;
        public int MaxRange { get; private set; }

        // Init에서 채워질 값
        public FixedVector2 Start { get; private set; }
        public FixedVector2 End   { get; private set; }

        public void SetMaxRange(int maxRange)
        {
            MaxRange = maxRange;
        }

        public FixedVector2 CenterCoordinate { get; private set; }

        public void ResolveFromContext(FixedVector2 start, FixedVector2 end)
        {
            Start = start;
            End   = end;
            CenterCoordinate = (start + end) / 2;
        }

        public Vector2 GetBoxSize()
        {
            var length = Vector2.Distance(Start.AsVector2, End.AsVector2);
            return new Vector2(length, Width / 1000f);
        }

        public float GetRotation()
        {
            var dir = End.AsVector2 - Start.AsVector2;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
    }

}