using global::NoWebGame.Diagnostics.Runtime;
using UnityEngine;

namespace NoWebGame.Diagnostics.Runtime
{
    /// <summary>장면에 하나만 추가하세요. Caster는 Enemy/Player Entity의 루트 Transform입니다.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Moves", "Assembly-CSharp", "AttackTraceSettings")]
[DisallowMultipleComponent]
    public sealed class AttackTraceSettings : MonoBehaviour
    {
        public bool traceEnabled = true;
        public Transform caster;
        public bool detailed;
        private static AttackTraceSettings _owner;
        private void OnEnable()
        {
            if (_owner && _owner != this)
            { Debug.LogWarning("AttackTraceSettings: only one active settings component is supported.", this); return; }
            _owner = this;
            Apply();
        }
        private void Update() => Apply();
        private void Apply()
        {
            if (_owner != this) return;
            AttackLog.Enabled = traceEnabled;
            AttackLog.Detail = detailed;
            AttackLog.CasterFilter = caster ? caster.GetInstanceID() : 0;
        }
        private void OnDisable()
        {
            if (_owner != this) return;
            AttackLog.Enabled = false;
            AttackLog.CasterFilter = 0;
            _owner = null;
        }
    }
}
