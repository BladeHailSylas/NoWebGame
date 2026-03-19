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
            Instance ??= this;
        }

        public void UpdateOverlay(int tick, DelayId id, int endTick, int delta)
        {
            debugText.text =
                $"DelayID: {id}\n" +
                $"Tick: {tick}\n" +
                $"End: {endTick}\n" +
                $"Delta: {delta}";
        }
        public void UpdateOverlay(int tick, Dictionary<DelayId, (int, int)> entries)
        {
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