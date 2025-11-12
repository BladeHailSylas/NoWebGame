using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class ReadOnlyFieldAttribute : PropertyAttribute { }

public abstract class StackDefinition : ScriptableObject
{
    [SerializeField, ReadOnlyField] private string id; // GUID 기반
    public string ID => id; // 외부 접근용 프로퍼티
    [Header("Metadata")]
    public string displayName;
    public int maxStacks = 1000000;
    public ushort duration;
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