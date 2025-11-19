using UnityEngine;

namespace Debuggers
{
    /// <summary>
    /// 임의 위치(현 위치 또는 previewPosition)에서 벽과의 평균 법선/MTV를 구해 시각화합니다.
    /// - Projectile/Teleport 프리뷰/Player 센서 디버깅 등 범용.
    /// - 위치를 바꾸지 않으며, 샘플링만 수행.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NormalMtvVisualizer2D : MonoBehaviour
    {
        [Header("Probe")]
        [SerializeField] LayerMask wallsMask;
        [SerializeField] float probeRadius = 0.5f;
        [SerializeField] float nearThreshold = 0.03f;

        [Header("Preview")]
        [SerializeField] bool previewMode = false;
        [SerializeField] Vector2 previewPosition;  // true면 이 위치를 기준으로 샘플링

        [Header("Draw")]
        [SerializeField] float arrowLength = 0.6f;
        [SerializeField] Color normalColor = Color.cyan;
        [SerializeField] Color mtvColor = Color.magenta;
        [SerializeField] Color ringColor = new(1f, 1f, 1f, 0.2f);

        Vector2 _avgNormal, _mtvDir; bool _intruding; float _minDist; Vector3 _samplePos;

        void Update()
        {
            _samplePos = previewMode ? (Vector3)previewPosition : transform.position;
            SampleAt(_samplePos);
        }

        /// <summary>외부에서 임의 지점을 주고 바로 샘플링 가능</summary>
        public void SampleAt(Vector3 pos)
        {
            _avgNormal = Vector2.zero; _mtvDir = Vector2.zero; _intruding = false; _minDist = float.PositiveInfinity;

            var cols = Physics2D.OverlapCircleAll(pos, probeRadius, wallsMask);
            var n = 0; var best = float.PositiveInfinity;

            foreach (var c in cols)
            {
                var p = c.ClosestPoint(pos);
                var v = (Vector2)pos - p;
                var d = v.magnitude;

                if (d <= nearThreshold) _intruding = true;
                if (d < best) best = d;

                if (d > 1e-4f) { _avgNormal += v / d; n++; }
            }

            if (n > 0) _avgNormal = _avgNormal.normalized;
            _minDist = (best < float.PositiveInfinity) ? best : _minDist;
            if (_intruding && _avgNormal != Vector2.zero) _mtvDir = _avgNormal;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var pos = previewMode ? (Vector3)previewPosition : transform.position;

            Gizmos.color = ringColor;
            Gizmos.DrawWireSphere(pos, probeRadius);

            if (_avgNormal != Vector2.zero)
            {
                Gizmos.color = normalColor;
                DrawArrow(pos, _avgNormal.normalized, arrowLength);
            }
            if (_mtvDir != Vector2.zero)
            {
                Gizmos.color = mtvColor;
                DrawArrow(pos, _mtvDir.normalized, arrowLength * 0.9f);
            }

            UnityEditor.Handles.color = _intruding ? Color.red : Color.white;
            UnityEditor.Handles.Label(pos + Vector3.up * 0.07f,
                _intruding ? $"MTV dir shown • minDist≈{_minDist:F3}" : $"avgN shown • minDist≈{_minDist:F3}");
        }

        static void DrawArrow(Vector3 o, Vector2 dir, float len)
        {
            var a = (Vector3)dir.normalized * len;
            Gizmos.DrawLine(o, o + a);
            var right = Quaternion.Euler(0, 0, +25f) * (-a.normalized);
            var left = Quaternion.Euler(0, 0, -25f) * (-a.normalized);
            Gizmos.DrawLine(o + a, o + a + right * (len * 0.25f));
            Gizmos.DrawLine(o + a, o + a + left * (len * 0.25f));
        }
#endif
    }
}
