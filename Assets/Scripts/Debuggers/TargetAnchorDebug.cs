using UnityEngine;

/// <summary>
/// TargetAnchor(��Ÿ�� ��Ŀ) �ð�ȭ �����.
/// - ����: ���� ��, �̸� ��, (����) �ֱ� �̵� ����/���� ��
/// - ���Ӻ�: LineRenderer�� ���� ���� ǥ��(�ɼ�)
/// </summary>
[DisallowMultipleComponent]
public sealed class TargetAnchorDebug : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] float radius = 0.15f;            // ȭ�鿡�� ���� ���� �ݰ�(�ð�ȭ ����)
    [SerializeField] bool enableRuntimeVisual = true;
    [SerializeField] int segments = 24;

    LineRenderer _lr; Vector3[] _ring;

    void Awake()
    {
        if (enableRuntimeVisual)
        {
            _lr = gameObject.GetComponent<LineRenderer>();
            if (!_lr) _lr = gameObject.AddComponent<LineRenderer>();
            _lr.positionCount = segments + 1;
            _lr.loop = true;
            _lr.useWorldSpace = true;
            _lr.widthMultiplier = 0.03f;      // ȭ�鿡�� ������ ���̴� �β�
            _lr.material = new Material(Shader.Find("Sprites/Default"));
            _lr.textureMode = LineTextureMode.Stretch;

            _ring = new Vector3[segments + 1];
            BuildRing();
        }
    }

    void Update()
    {
        if (!enableRuntimeVisual || _lr == null) return;

        // ��¦ �޽� ȿ��(������ �ð�ȭ)
        var r = radius * (1f + 0.05f * Mathf.Sin(Time.time * 6f));
        for (var i = 0; i <= segments; i++)
        {
            var a = (Mathf.PI * 2f) * i / segments;
            _ring[i] = transform.position + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }
        _lr.SetPositions(_ring);
    }

    void BuildRing()
    {
        for (var i = 0; i <= segments; i++)
        {
            var a = (Mathf.PI * 2f) * i / segments;
            _ring[i] = transform.position + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }
        _lr.SetPositions(_ring);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // ���信�� ���� ��
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        DrawWireDisc(transform.position, Vector3.forward, radius, 32);

        // ��
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.Label(transform.position + Vector3.up * (radius + 0.05f), $"ANCHOR\n{gameObject.name}");
    }

    static void DrawWireDisc(Vector3 center, Vector3 normal, float r, int seg)
    {
        var prev = center + new Vector3(r, 0, 0);
        for (var i = 1; i <= seg; i++)
        {
            var a = (Mathf.PI * 2f) * i / seg;
            var cur = center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }
    }
#endif
}