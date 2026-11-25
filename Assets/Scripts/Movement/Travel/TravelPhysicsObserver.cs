using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoWebGame.Movement.Travel
{
    /// <summary>Production은 FixedUpdate 물리 이후, Script simulation은 명시적 통보 이후 관측합니다.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.SubSystems", "Assembly-CSharp", "TravelPhysicsObserver")]
[DisallowMultipleComponent]
    public sealed class TravelPhysicsObserver : MonoBehaviour
    {
        private static readonly HashSet<TravelPhysicsObserver> Active = new();
        private static bool _quitting;
        public Action AfterPhysics;
        public Action<bool> Disabled;
        public static bool IsShuttingDown => _quitting || !Application.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active.Clear(); _quitting = false; }
        private void OnEnable() { Active.Add(this); StartCoroutine(Observe()); }
        private IEnumerator Observe()
        {
            var wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                if (Physics2D.simulationMode == SimulationMode2D.FixedUpdate) AfterPhysics?.Invoke();
            }
        }
        public static void NotifyManualSimulationCompleted()
        {
            // 콜백 중 종료/비활성화로 집합이 바뀔 수 있으므로 복사본을 순회합니다.
            foreach (var observer in new List<TravelPhysicsObserver>(Active))
                if (observer && observer.isActiveAndEnabled) observer.AfterPhysics?.Invoke();
        }
        private void OnApplicationQuit() { _quitting = true; }
        private void OnDisable()
        {
            Active.Remove(this);
            StopAllCoroutines();
            Disabled?.Invoke(!IsShuttingDown && gameObject.scene.isLoaded);
        }
    }
}
