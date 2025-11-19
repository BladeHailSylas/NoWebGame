using Systems.Data;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// 화면 커서의 월드 좌표를 얻는 모듈.
    /// 마우스 또는 게임패드 조준 입력을 받아, 지정된 평면 상의 커서 위치를 계산한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CursorResolver : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("커서를 투영할 카메라 (비워두면 Camera.main 사용)")]
        public Camera mainCamera;

        [Tooltip("커서가 투영될 레이어 (Ground, Field 등)")]
        public LayerMask groundMask;

        [Tooltip("커서 감지를 위한 최대 거리")]
        public float rayDistance = 50f;

        [Tooltip("디버그용 로그 출력 여부")]
        public bool debugLog = false;

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        /// <summary>
        /// 현재 커서의 월드 좌표를 계산합니다.
        /// </summary>
        /// <param name="worldPos">2D 월드 좌표 (Vector2)</param>
        /// <param name="fixedPos">결정적 좌표 (FixedVector2)</param>
        /// <returns>커서 감지 성공 여부</returns>
        public bool TryGetCursorWorld(out Vector2 worldPos, out FixedVector2 fixedPos)
        {
            worldPos = Vector2.zero;
            fixedPos = default;

            // 1️⃣ 입력 좌표 가져오기
            var screenPos = Input.mousePosition;
            if (mainCamera is null)
            {
                if (debugLog) Debug.LogWarning("[CursorResolver] 카메라가 지정되지 않았습니다.");
                return false;
            }

            // 2️⃣ 스크린 → 월드 변환
            var world = mainCamera.ScreenToWorldPoint(screenPos);
            world.z = 0f;

            // 3️⃣ 커서가 Ground 위에 있는지 검사
            var hit = Physics2D.Raycast(world, Vector2.zero, rayDistance, groundMask);

            if (hit.collider is not null)
            {
                // 히트 포인트를 사용
                worldPos = hit.point;
                fixedPos = FixedVector2.FromVector2(worldPos);

                if (debugLog) Debug.Log($"[CursorResolver] Ground 히트 감지: {worldPos}");
                return true;
            }

            // Ground가 없으면 ScreenToWorldPoint 결과를 그대로 사용
            worldPos = world;
            fixedPos = FixedVector2.FromVector2(worldPos);

            if (debugLog) Debug.Log($"[CursorResolver] Ground 미감지, 기본 좌표 사용: {worldPos}");
            return true; // 감지는 실패했지만 커서 좌표는 유효
        }
    }
}