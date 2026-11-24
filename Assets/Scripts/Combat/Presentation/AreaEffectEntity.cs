using UnityEngine;

namespace Moves.Effects.Definitions
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AreaEffectEntity : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.3f;

        public void InitCircle(Vector2 center, float radius)
        {
            transform.position = center;
            GenerateCircleMesh(radius);
            AutoDestroy();
        }

        public void InitBox(Vector2 center, Vector2 size, float angle)
        {
            transform.position = center;
            transform.rotation = Quaternion.Euler(0,0,angle);
            GenerateBoxMesh(size);
            AutoDestroy();
        }

        public void InitLaser(Vector2 start, Vector2 end, float width)
        {
            GenerateLaserMesh(start, end, width);
            AutoDestroy();
        }

        private void AutoDestroy()
        {
            Destroy(gameObject, lifetime);
        }
        private void GenerateCircleMesh(float radius)
        {
            const int segments = 30;
            const int angleDeg = 360;
            var dir = Vector3.zero;
            var mesh = new Mesh();
            TryGetComponent<MeshFilter>(out var mesher);
            mesher.mesh = mesh;

            var halfAngle = angleDeg * 0.5f;
            var baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            const int vertexCount = segments + 2;
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
        }
        private void GenerateBoxMesh(Vector2 size)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[4];
            var triangles = new int[6];

            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;

            vertices[0] = new Vector3(-hx, -hy);
            vertices[1] = new Vector3(hx, -hy);
            vertices[2] = new Vector3(-hx, hy);
            vertices[3] = new Vector3(hx, hy);

            triangles[0] = 0;
            triangles[1] = 2;
            triangles[2] = 1;

            triangles[3] = 2;
            triangles[4] = 3;
            triangles[5] = 1;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            TryGetComponent<MeshFilter>(out var mesher);
            mesher.mesh = mesh;
        }
        private void GenerateLaserMesh(Vector2 start, Vector2 end, float width)
        {
            var dir = (end - start).normalized;
            var normal = new Vector2(-dir.y, dir.x);

            var halfW = width * 0.5f;

            var vertices = new Vector3[4];
            
            vertices[0] = start + normal * halfW;
            vertices[1] = start - normal * halfW;
            vertices[2] = end + normal * halfW;
            vertices[3] = end - normal * halfW;
            int[] triangles = {
                0, 2, 1,
                2, 3, 1
            };

            var mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            TryGetComponent<MeshFilter>(out var mesher);
            mesher.mesh = mesh;
        }
    }
}