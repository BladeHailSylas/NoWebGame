#if UNITY_EDITOR
using System;
using UnityEngine;

namespace ExitLab
{
    // 종결 함수 진입 횟수와 실제 파괴 시점을 혼동하지 않기 위한 관측 컴포넌트입니다.
    public sealed class ExitLabLifecycle : MonoBehaviour
    {
        [NonSerialized] public Action onDestroyed;
        private void OnDestroy() => onDestroyed?.Invoke();
    }
}
#endif
