using System.Linq;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEditor;
using UnityEngine;
public class MonoSizeReport
{
    // 필요시 조정: 경고 임계치
    const int Threshold = 60;

    [MenuItem("Tools/Report/Heavy MonoBehaviours (Assets only)")]
    static void Run()
    {
        // Assets 폴더 아래의 C# 스크립트만 검색
        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
        int count = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // Packages/ 이하나 생성물은 제외 (안전장치)
            if (path.StartsWith("Packages/")) continue;

            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (ms == null) continue;

            var t = ms.GetClass();
            if (t == null) continue; // 에디터 전용/제네릭 등으로 타입 생성 실패한 경우
            if (t.IsAbstract) continue;
            if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;

            var methods = t.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly
            ).Length;

            var fields = t.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly
            ).Length;

            var size = methods + fields;
            if (size > Threshold)
            {
                Debug.LogWarning($"{t.FullName} ({path}): methods+fields={size}");
                count++;
            }
        }

        Debug.Log($"[Assets only] Heavy MonoBehaviours warnings: {count}");
    }
}