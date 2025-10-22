using UnityEngine;

/// <summary>
/// 에디터/런타임에서 SimpleEntity들의 위치를 Gizmo로 시각화.
/// </summary>
/*public class SimpleWorldGizmoDrawer : MonoBehaviour
{
    private static SimpleWorld _world; // 테스트 중인 Core 월드 참조
    public Color entityColor = Color.cyan;
    public float pointSize = 0.1f;

    private void Awake()
    {
        _world ??= SimpleWorld.Instance;
    }

    private void OnEnable()
    {
        _world ??= SimpleWorld.Instance;
    }
    private void OnDrawGizmos()
    {
        Draw();
    }
    private void Draw()
    {
        _world ??= SimpleWorld.Instance;
        if (_world == null)
        {
            //Debug.LogError("Have ☆ children?");
            return;
        }
        //Debug.Log("Hello, I want to draw turmeric here?");
        Gizmos.color = entityColor;

        foreach (var id in _world.EnumerateEntities())
        {
            if (_world.TryGetEntity(id, out var entity))
            {
                var pos = entity.Transform.asVector2();
                Gizmos.DrawSphere(new Vector3(pos.x, pos.y, 0), pointSize);
            }
        }
    }
}*/