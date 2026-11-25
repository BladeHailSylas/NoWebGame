using GameTime = global::NoWebGame.Systems.Time.Time;
using global::NoWebGame.Systems.Time;
using System.Collections;
using UnityEngine;

namespace NoWebGame.Systems.Time
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Time", "Assembly-CSharp", "TickerRunner")]
[DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public class TickerRunner : MonoBehaviour
    {
        private static TickerRunner _owner;
        private Ticker _ticker;
        private DelayScheduler _scheduler;
        private Coroutine _loop;

        private void Awake()
        {
            if (_owner && _owner != this)
            {
                Debug.LogError("Only one TickerRunner may own the combat clock.", this);
                enabled = false;
                return;
            }
            _owner = this;
            _ticker = new Ticker();
            _scheduler = new DelayScheduler();
            GameTime.Ticker = _ticker;
            GameTime.DelayScheduler = _scheduler;
            Ticker.Instance = _ticker;
            DelayScheduler.Instance = _scheduler;
            GameTime.Initialize();
        }

        private void OnEnable()
        {
            if (_owner != this || _ticker == null || _loop != null) return;
            _loop = StartCoroutine(TickLoop());
        }

        private void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            // disable은 일시 정지입니다. 시계/스케줄러/구독을 교체하지 않아 예약이 이어집니다.
        }

        private void OnDestroy()
        {
            if (_owner != this) return;
            GameTime.Release(_ticker, _scheduler);
            _owner = null;
        }

        private IEnumerator TickLoop()
        {
            var interval = new WaitForSecondsRealtime(1f / Ticker.TicksPerSecond);
            while (true)
            {
                // 첫 Tick도 초기화/구독이 끝난 뒤 발행합니다. 재활성화 자체는 시간을 증가시키지 않습니다.
                yield return interval;
                _ticker.Step();
            }
        }
    }
}
