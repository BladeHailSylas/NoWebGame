using UnityEditor;
using UnityEngine;

public static class MissingRefScanner
{
    [MenuItem("Tools/Scan/Missing References")]
    static void Scan()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab t:Scene t:ScriptableObject");
        int missing = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var objs = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in objs)
            {
                var go = obj as GameObject; if (!go) continue;
                foreach (var comp in go.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) { Debug.LogWarning($"Missing Component in {path}", go); missing++; continue; }
                    var so = new SerializedObject(comp); var prop = so.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                            prop.objectReferenceInstanceIDValue == 0 && prop.objectReferenceValue != null)
                        { Debug.LogWarning($"Missing Reference: {path} -> {comp.GetType().Name}.{prop.displayName}", go); missing++; }
                    }
                }
            }
        }
        Debug.Log($"Scan complete. Missing refs: {missing}");
    }
}