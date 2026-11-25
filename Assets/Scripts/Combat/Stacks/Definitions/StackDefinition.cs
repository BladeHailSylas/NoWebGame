using UnityEditor;
using UnityEngine;

namespace NoWebGame.Combat.Stacks.Definitions
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks.Definition", "Assembly-CSharp", "ReadOnlyFieldAttribute")]
    public class ReadOnlyFieldAttribute : PropertyAttribute { }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks.Definition", "Assembly-CSharp", "StackDefinition")]
    public abstract class StackDefinition : ScriptableObject
    {
        [SerializeField, ReadOnlyField] private string id; // GUID 기반
        public string ID => id; // 외부 접근용 프로퍼티
        [Header("Metadata")]
        public string displayName;
        public int maxStacks = 1000000;
        public ushort duration = 65535;
        public GameObject visualPrefab;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터에서만 실행 (빌드 시 불필요)
            if (!string.IsNullOrEmpty(id)) return;
            var path = AssetDatabase.GetAssetPath(this);
            var guid = AssetDatabase.AssetPathToGUID(path);

            // id가 비어 있을 때 GUID를 자동 할당
            id = guid;
            EditorUtility.SetDirty(this);
        }
#endif
    }

    public enum PeriodicType
    {
        Recharging,
        Accumulating,
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Systems.Stacks.Definition", "Assembly-CSharp", "StackMetadata")]
    public readonly struct StackMetadata
    {
        public readonly int Metadata;

        public StackMetadata(int metadata = 0)
        {
            Metadata = metadata;
        }
    }
}