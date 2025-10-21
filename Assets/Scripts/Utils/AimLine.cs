// AimLine.cs
// - 플레이어 위치에서 마우스 월드 좌표까지 선을 그립니다.
// - 장애물 레이어를 지정하면 Raycast로 선 길이를 그 지점까지 "클램프"할 수 있습니다.
// - URP/2D 프로젝트에서도 바로 사용 가능.
// AimLine이 범위 지정형 기술의 최대 사거리를 지정해 주는 역할을 하면 안 될까?
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class AimLine : MonoBehaviour
{
    [Header("Line Settings")]
    public float width = 0.05f;          // 선 굵기
    public float maxLength = 1.2f;         // 선 최대 길이
    public Color color = new(0.3f, 0.3f, 0.3f, 0.4f); // 선 색

    [Header("Obstacles (Optional)")]
    public bool clampToObstacle = false; // true면 장애물까지 선을 자릅니다.
    public LayerMask obstacleMask;       // 장애물 레이어 마스크

    [Header("Sorting (2D draw order)")]
    public string sortingLayerName = "Default";
    public int sortingOrder = 50;        // 본체보다 위에 보이도록 충분히 높게

    private LineRenderer _lr;
    private Camera _cam;

    void Awake()
    {
        _cam = Camera.main;

        // 자식 오브젝트에 LineRenderer를 만들어도 되고, 현재 오브젝트에 추가해도 됩니다.
        _lr = gameObject.AddComponent<LineRenderer>();

        // 2D에서 보기 좋게 라운드 캡/두께 설정
        _lr.positionCount = 2;
        _lr.useWorldSpace = true;
        _lr.numCapVertices = 8;
        _lr.numCornerVertices = 0;

        _lr.startWidth = width;
        _lr.endWidth = width;

        _lr.startColor = color;
        _lr.endColor = color;

        // 머티리얼: 기본 Unlit 계열이면 충분합니다(색만 쓰면 OK).
        // URP 프로젝트라면 "Sprites/Default" 또는 "Universal Render Pipeline/Unlit" 중 편한 걸 사용.
        var mat = new Material(Shader.Find("Sprites/Default"));
        _lr.material = mat;

        // 2D 정렬 (스프라이트와 겹칠 때 위로)
        _lr.sortingLayerName = sortingLayerName;
        _lr.sortingOrder = sortingOrder;

        obstacleMask = LayerMask.GetMask("Walls");
    }

    void Update()
    {
        if (_cam == null) return;

        // 1) 시작점: 플레이어(현재 오브젝트) 위치
        Vector3 start = transform.position;

        // 2) 마우스 스크린좌표 → 월드좌표
        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = start.z; // 2D 평면(z 고정) 정렬

        // 3) 최대 길이 적용
        Vector3 dir = (mouseWorld - start);
        float dist = dir.magnitude;
        if (dist > Mathf.Epsilon) dir /= dist;

        float length = Mathf.Min(dist, maxLength);
        Vector3 end = start + dir * length;

        // 4) 장애물에 부딪히면 그 지점까지로 잘라내기(선택)
        if (clampToObstacle)
        {
            var hit = Physics2D.Raycast(start, dir, length, obstacleMask);
            if (hit.collider != null)
                end = hit.point;
        }

        // 5) 선 좌표 갱신
        _lr.SetPosition(0, start);
        _lr.SetPosition(1, end);

        // (옵션) 마우스 오른쪽 버튼을 누를 때만 보이게
        // _lr.enabled = Input.GetMouseButton(1);
    }
}