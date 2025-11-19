using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class MonoSizeReport
    {
        // �ʿ�� ����: ��� �Ӱ�ġ
        const int Threshold = 60;

        [MenuItem("Tools/Report/Heavy MonoBehaviours (Assets only)")]
        static void Run()
        {
            // Assets ���� �Ʒ��� C# ��ũ��Ʈ�� �˻�
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            int count = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // Packages/ ���ϳ� �������� ���� (������ġ)
                if (path.StartsWith("Packages/")) continue;

                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null) continue;

                var t = ms.GetClass();
                if (t == null) continue; // ������ ����/���׸� ������ Ÿ�� ���� ������ ���
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
}