using UnityEngine;
using System;
using System.Collections;

[DisallowMultipleComponent]
/// <summary>
/// Internal runner that bridges Unity's Update loop to the deterministic ticker.
/// </summary>
public class TickerRunner : MonoBehaviour
{
    private Ticker _ticker;
    private void OnEnable()
    {
        _ticker = new Ticker();
        Debug.Log("Ticker Awake, time is ticking...");
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
            catch (TickCountOverflowException ex)
            {
				Debug.LogError("Die yobbo");
            }
            yield return interval;
        }
		
    }
}