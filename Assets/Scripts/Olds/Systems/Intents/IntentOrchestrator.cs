using UnityEngine;
namespace Intents
{
    public class IntentOrchestrator : MonoBehaviour
    {
        void OnEnable()
        {
            //IntentRouter route = new();
            Debug.Log("I live!");
            Ticker.Instance.OnTick += TickHandler;
        }

        void TickHandler(ushort tick)
        {
            Debug.Log($"On Tick {tick}");
            IntentCollector.Instance.TickHandler(tick);
            IntentRouter.Instance.TickHandler(tick);
        }
    }
}