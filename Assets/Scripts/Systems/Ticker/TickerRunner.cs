using System.Collections;
using UnityEngine;

namespace Systems.Ticker
{
    [DisallowMultipleComponent]
    public class TickerRunner : MonoBehaviour
    {
        private Ticker _ticker;
        private void OnEnable()
        {
            _ticker = new Ticker();
            Debug.Log("AYAYAY AYAYYYYY");
            //BattleCore.Initialize();
            StartCoroutine(TickLoop());
        }

        IEnumerator TickLoop()
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