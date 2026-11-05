using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Moves.ObjectEntity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ExitLab
{
    public static class ExitLabBuilder
    {
        private const string Root = "Assets/ExitLab/Generated";
        [MenuItem("Tools/Exit Lab/Create isolated scene (once)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료하세요.");
            if (File.Exists(Root + "/ProjectileDashExit.unity"))
                throw new InvalidOperationException("기존 시험 씬을 덮어쓰지 않습니다. 씬을 직접 여세요.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("ExitLab Camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 4;
            camera.transform.position = new Vector3(1, 0, -10);
            var harness = new GameObject("ExitLab - Select case here").AddComponent<ExitLabHarness>();
            var template = new GameObject("MinimalProjectile");
            var rb = template.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic; rb.gravityScale = 0; rb.interpolation = RigidbodyInterpolation2D.None;
            var col = template.AddComponent<CircleCollider2D>(); col.radius = 0.1f; col.isTrigger = true;
            var projectile = template.AddComponent<ProjectileEntity>();
            var serialized = new SerializedObject(projectile);
            serialized.FindProperty("rb").objectReferenceValue = rb;
            serialized.FindProperty("col").objectReferenceValue = col;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(template, Root + "/MinimalProjectile.prefab");
            UnityEngine.Object.DestroyImmediate(template);
            harness.projectilePrefab = prefab.GetComponent<ProjectileEntity>();
            harness.coordinateTarget = Target("TransformOnly (no Collider)", false, false);
            harness.entityTarget = Target("Entity + Collider", true, false);
            harness.childColliderTarget = Target("Entity root + child Collider", true, true);
            harness.intermediateEnemy = Target("Intermediate Foe", true, false);
            EditorSceneManager.SaveScene(scene, Root + "/ProjectileDashExit.unity");
            AssetDatabase.SaveAssets();
            Validate();
        }
        private static Transform Target(string name, bool entity, bool child)
        {
            var go = new GameObject(name); go.transform.position = new Vector3(3, 0, 0);
            if (entity)
            {
                go.layer = LayerMask.NameToLayer("Foe"); go.AddComponent<ExitLabEntity>();
                var colliderObject = go;
                if (child) { colliderObject = new GameObject("Child Collider (no Entity)"); colliderObject.transform.SetParent(go.transform, false); colliderObject.layer = go.layer; }
                var col = colliderObject.AddComponent<CircleCollider2D>(); col.radius = 0.2f; col.isTrigger = true;
            }
            go.SetActive(false);
            return go.transform;
        }
        // EditMode 구성 검증만 수행합니다. Tick/Simulate/Play/Mechanism.Execute를 호출하지 않습니다.
        [MenuItem("Tools/Exit Lab/Validate saved scene (no gameplay)")]
        public static void Validate()
        {
            var scene = SceneManager.GetSceneByPath(Root + "/ProjectileDashExit.unity");
            var alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(Root + "/ProjectileDashExit.unity", OpenSceneMode.Additive);
            try
            {
                ExitLabHarness harness = null;
                foreach (var root in scene.GetRootGameObjects()) if (root.TryGetComponent<ExitLabHarness>(out var found)) harness = found;
                if (!harness || !harness.projectilePrefab || !harness.coordinateTarget || !harness.entityTarget || !harness.childColliderTarget || !harness.intermediateEnemy)
                    throw new InvalidOperationException("fixture 참조 누락");
                var data = new SerializedObject(harness.projectilePrefab);
                if (!data.FindProperty("rb").objectReferenceValue || !data.FindProperty("col").objectReferenceValue)
                    throw new InvalidOperationException("Projectile rb/col 누락");
                if (harness.coordinateTarget.GetComponentInChildren<Collider2D>(true)) throw new InvalidOperationException("빈 목표에 Collider 존재");
                if (!harness.entityTarget.GetComponent<ExitLabEntity>() || !harness.entityTarget.GetComponent<Collider2D>()) throw new InvalidOperationException("같은 객체 목표 구성 오류");
                var child = harness.childColliderTarget.GetComponentInChildren<Collider2D>(true);
                if (!child || child.GetComponent<Systems.Data.Entity>() || !harness.childColliderTarget.GetComponent<ExitLabEntity>()) throw new InvalidOperationException("자식 Collider 목표 구성 오류");
                if (LayerMask.NameToLayer("Foe") != 7 || LayerMask.NameToLayer("Walls&Obstacles") != 3) throw new InvalidOperationException("레이어 불일치");
                var prefab = harness.projectilePrefab;
                var rb = prefab.GetComponent<Rigidbody2D>();
                var col = prefab.GetComponent<CircleCollider2D>();
                if (rb.bodyType != RigidbodyType2D.Kinematic || rb.gravityScale != 0 || rb.interpolation != RigidbodyInterpolation2D.None ||
                    !col.isTrigger || Mathf.Abs(col.radius - 0.1f) > 0.0001f || prefab.gameObject.layer != 0)
                    throw new InvalidOperationException("Projectile 물리 설정 불일치");
                foreach (var item in new[] { harness.entityTarget, harness.childColliderTarget, harness.intermediateEnemy })
                {
                    var collider = item.GetComponentInChildren<Collider2D>(true);
                    if (!collider.isTrigger || collider.gameObject.layer != 7 || item.gameObject.activeSelf)
                        throw new InvalidOperationException("Target 레이어/trigger/초기 비활성 설정 불일치");
                }
                AuditDependencies();
                Debug.Log("EXIT_LAB_EDITMODE_VALIDATION_OK: saved references, prefab rb/col, three target shapes, layer names; NO gameplay executed.");
            }
            finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AuditDependencies()
        {
            // 원본 씬은 열거나 저장하지 않습니다. GUID는 Package/내장 참조와 구별해서 조회합니다.
            var lines = File.ReadAllLines("Assets/Scenes/Battlefield.unity");
            var report = new StringBuilder("# Battlefield 의존성 읽기 전용 점검\n\nUnity 6000.3.10f1 AssetDatabase GUID 조회. 원본 씬 로드/저장 및 게임플레이 없음.\n\n");
            report.AppendLine("아래는 AssetDatabase에서 경로를 찾지 못한 외부 GUID입니다. fileID=0/null 필드는 이 목록에 포함되지 않습니다.\n");
            var seen = new HashSet<string>();
            for (var i = 0; i < lines.Length; i++)
                foreach (Match match in Regex.Matches(lines[i], @"guid: ([0-9a-f]{32})"))
                {
                    var guid = match.Groups[1].Value;
                    if (guid.StartsWith("0000000000000000") || !seen.Add(guid) || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) continue;
                    report.AppendLine($"- 줄 {i + 1}, GUID `{guid}`: `{lines[i].Trim()}`");
                }
            report.AppendLine("\n## 범위 제한\n\n별도 fixture는 이 원본 콘텐츠를 사용하지 않습니다. CharacterSpec/SkillData/Stack 원본 복구, Player/입력/Root/Anchor 통합 및 Player 빌드는 미검증입니다.");
            Directory.CreateDirectory("Documentation");
            File.WriteAllText("Documentation/ExitLabDependencyAudit.md", report.ToString());
        }
    }
}
