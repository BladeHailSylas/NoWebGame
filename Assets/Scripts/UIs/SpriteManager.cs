using System.Collections;
using UnityEngine;

namespace UIs
{
    /// <summary>
    /// SpriteManager
    /// 엔티티의 시각 표현을 담당.
    /// 로직과 분리하여 유지.
    /// </summary>
    public class SpriteManager : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Flash Settings")]
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private Color flashColor = Color.red;

        private Color _originalColor;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            spriteRenderer ??= GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
            _originalColor = spriteRenderer.color;
        }

        /// <summary>
        /// 피격 시 호출.
        /// 이전 Flash가 진행 중이면 중단 후 재시작.
        /// </summary>
        public void Flash()
        {
            Debug.Log("☆★☆");
            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);

            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = _originalColor;
        }
    }
}