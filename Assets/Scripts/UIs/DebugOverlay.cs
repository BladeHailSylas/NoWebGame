using System.Collections.Generic;
using Systems.Time;
using UnityEngine;
using TMPro;
namespace UIs
{
    /// <summary>
    /// 디버그 정보 표시 전용 컴포넌트.
    /// Tick 종료 시 UpdateOverlay() 호출.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI debugText;
        public static DebugOverlay Instance { get; private set; }

        private void Awake()
        {
            if (!Instance) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void UpdateOverlay(long tick, DelayId id, long endTick, long delta)
        {
            if (!debugText) return;
            debugText.text =
                $"DelayID: {id}\n" +
                $"Tick: {tick}\n" +
                $"End: {endTick}\n" +
                $"Delta: {delta}";
        }
        public void UpdateOverlay(long tick, Dictionary<DelayId, (long, long)> entries)
        {
            if (!debugText) return;
            var text = $"Tick: {tick}\n";
            foreach (var kvp in entries)
            {
                var id = kvp.Key;
                var (endTick, delta) = kvp.Value;
                text += $"{id} End: {endTick} Delta: {delta}\n";
            }
            debugText.text = text;
        }
    }

}
