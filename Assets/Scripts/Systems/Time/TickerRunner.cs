using System.Collections;
using UnityEngine;

namespace Systems.Time
{
    [DisallowMultipleComponent]
    public class TickerRunner : MonoBehaviour
    {
        private Ticker _ticker;
        private DelayScheduler _scheduler;
        private void OnEnable()
        {
            _ticker = new Ticker();
            _scheduler = new DelayScheduler();
            Time.Ticker = _ticker;
            Time.DelayScheduler = _scheduler;
            Debug.Log("AYAYAY AYAYYYYY");
            StartCoroutine(TickLoop());
        }

        private IEnumerator TickLoop()
        {
            var interval = new WaitForSecondsRealtime(1f / Ticker.TicksPerSecond);
            while (true)
            {
                try
                {
                    _ticker.Step();
                }
                catch (TickCountOverflowException)
                {
                    Debug.LogError("Die yobbo");
                }
                yield return interval;
            }
		
        }
    }
}