using UnityEngine;

namespace Moves.Effects.Definitions
{
    /// <summary>
    /// Melee 범위를 부채꼴 메쉬로 표시.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MeleeEffectEntity : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.2f;
        [SerializeField] private int segments = 30;

        public void Init(Vector2 origin, float radius, float angleDeg, Vector2 dir)
        {
            transform.position = origin;

            var mesh = new Mesh();
            TryGetComponent<MeshFilter>(out var mesher);
            mesher.mesh = mesh;

            var halfAngle = angleDeg * 0.5f;
            var baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            var vertexCount = segments + 2;
            var vertices = new Vector3[vertexCount];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;

            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = baseAngle - halfAngle + t * angleDeg;
                var rad = angle * Mathf.Deg2Rad;

                var x = Mathf.Cos(rad) * radius;
                var y = Mathf.Sin(rad) * radius;

                vertices[i + 1] = new Vector3(x, y, 0f);
            }

            for (var i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            Destroy(gameObject, lifetime);
        }
    }
}