using global::NoWebGame.Combat.Definitions;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Entities.Relations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Editor;



using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExitLab
{
    public static class TargetMaskMigrationChecks
    {
        private static readonly List<string> Results = new();
        private const string Scratch = "Assets/ExitLab/TargetMigrationCheckTemp";
        private static void Check(bool ok, string name)
        { if (!ok) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static void ExpectBlocked(Action action, string name)
        {
            try { action(); }
            catch (InvalidOperationException) { Check(true, name); return; }
            throw new InvalidOperationException("Expected rejection: " + name);
        }
        private static void RequireTestCopy()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Run only in an explicitly marked batch validation copy.");
        }
        private static List<NewMechanism> Nodes(SkillData asset)
        {
            var result = new List<NewMechanism>();
            using var serialized = new SerializedObject(asset);
            var it = serialized.GetIterator(); var seen = new HashSet<long>();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ManagedReference && seen.Add(it.managedReferenceId) && it.managedReferenceValue is NewMechanism node)
                    result.Add(node);
            return result;
        }
        private static string Ids(SkillData asset)
        {
            var values = new HashSet<string>();
            using var serialized = new SerializedObject(asset);
            var it = serialized.GetIterator();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ManagedReference)
                    values.Add(it.managedReferenceId + ":" + it.managedReferenceFullTypename);
            return string.Join("|", values.OrderBy(v => v, StringComparer.Ordinal));
        }
        private static void Policies(SkillData asset, string label)
        {
            foreach (var node in Nodes(asset))
                Check(node.Targeting.Version == 1 && node.Targeting.IsValid &&
                    node.Targeting.Relations == (node.Mask.value == 128 ? TargetRelationMask.Enemy : TargetRelationMask.None),
                    label + " " + node.GetType().Name + " relationship persisted");
        }

        /// <summary>첫 적용 전 사본에서만 실행합니다. 원본 콘텐츠에 쓸 최소 변경 결과를 검증합니다.</summary>
        public static void ApplyAndVerify()
        {
            RequireTestCopy(); Results.Clear();
            if (AssetDatabase.IsValidFolder(Scratch)) throw new InvalidOperationException("Scratch folder already exists; no cleanup of pre-existing data.");
            var createdScratch = false;
            try
            {
                var plan = TargetMaskMigration.Scan();
                TargetMaskMigration.WriteReport(plan, Path.GetFullPath(Application.dataPath + "/../../target-stage3-dry-run.txt"));
                Check(!plan.HasReview && plan.Files.Count > 0, "dry-run has only approved conversions");
                var emptyPath = "Assets/ObjectInstances/SkillData/SkillData 3.asset";
                var emptyBefore = File.ReadAllBytes(emptyPath);
                var identities = plan.Files.ToDictionary(f => f.Path, f => Ids(AssetDatabase.LoadAssetAtPath<SkillData>(f.Path)));
                foreach (var file in plan.Files)
                {
                    var removed = Regex.Replace(file.After, @"(?m)^        (targetMaskVersion|targetRelations): \d+\r?\n", "");
                    Check(removed == file.Before, "only version/relation fields added: " + file.Path);
                    var again = TargetMaskMigration.AnalyzeText(file.Path, file.After, new List<TargetMaskMigration.Row>());
                    Check(again.After == file.After, "text transform idempotent: " + file.Path);
                }
                var sample = plan.Files.First(f => f.Path.EndsWith("/SkillData.asset")).Before;
                void Review(string text, string name)
                {
                    var rows = new List<TargetMaskMigration.Row>(); TargetMaskMigration.AnalyzeText("synthetic.asset", text, rows);
                    Check(rows.Any(r => r.Status == "REVIEW"), name);
                }
                Review(sample.Replace("m_Bits: 128", "m_Bits: 64"), "You is not automatically converted");
                Review(sample.Replace("m_Bits: 128", "m_Bits: 136"), "mixed wall mask requires review");
                Review(sample.Replace("m_Bits: 128", "m_Bits: 4294967295"), "Everything requires review");
                Review(sample.Replace("angleDeg: 120", "targetMaskVersion: 99\n        angleDeg: 120"), "unknown version rejected");
                Review(sample.Replace("angleDeg: 120", "targetMaskVersion: 1\n        targetRelations: 128\n        angleDeg: 120"), "invalid relative bits rejected");
                Review(Regex.Replace(sample, @"(enemyMask:\r?\n          serializedVersion: 2\r?\n          m_Bits: )128", "${1}0"), "Melee double-mask disagreement rejected");
                var changedHash = plan.Files[0].Hash; plan.Files[0].Hash = "changed";
                ExpectBlocked(() => TargetMaskMigration.Apply(plan, "unused"), "stale dry-run rejected before writing");
                plan.Files[0].Hash = changedHash;
                plan.Rows.Add(new TargetMaskMigration.Row { Status = "REVIEW" });
                ExpectBlocked(() => TargetMaskMigration.Apply(plan, "unused"), "review blocks entire plan"); plan.Rows.RemoveAt(plan.Rows.Count - 1);
                var dirty = AssetDatabase.LoadAssetAtPath<SkillData>(plan.Files[0].Path); EditorUtility.SetDirty(dirty);
                ExpectBlocked(() => TargetMaskMigration.Apply(plan, "unused"), "unsaved asset edits protected"); EditorUtility.ClearDirty(dirty);
                foreach (var file in plan.Files) Check(File.ReadAllText(file.Path) == file.Before, "rejected operations preserve file: " + file.Path);
                var backup = Path.GetFullPath(Application.dataPath + "/../../target-stage3-validation-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
                TargetMaskMigration.Apply(plan, backup);
                foreach (var file in plan.Files)
                {
                    Check(File.ReadAllText(file.Path) == file.After, "applied exact reviewed bytes: " + file.Path);
                    var asset = AssetDatabase.LoadAssetAtPath<SkillData>(file.Path);
                    Check(Ids(asset) == identities[file.Path], "managed reference IDs/types preserved: " + file.Path);
                    Policies(asset, file.Path);
                    Resources.UnloadAsset(asset);
                    Policies(AssetDatabase.LoadAssetAtPath<SkillData>(file.Path), "unload/reload " + file.Path);
                }
                Check(File.ReadAllBytes(emptyPath).SequenceEqual(emptyBefore), "empty SkillData unchanged");
                var migrated = TargetMaskMigration.Scan();
                Check(!migrated.HasReview && migrated.Files.Count == 0, "second full scan produces zero changes");
                TargetMaskMigration.Apply(migrated, backup); // 빈 재실행에서는 백업을 덮어쓰지 않습니다.
                Check(!TargetMaskMigration.Scan().HasReview, "repeat apply safe");

                // 원본을 재저장하지 않고 복제본만 Unity에서 저장·재로드합니다.
                AssetDatabase.CreateFolder("Assets/ExitLab", "TargetMigrationCheckTemp"); createdScratch = true;
                foreach (var file in plan.Files)
                {
                    var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>(file.Path));
                    var path = AssetDatabase.GenerateUniqueAssetPath(Scratch + "/RoundTrip.asset");
                    AssetDatabase.CreateAsset(copy, path); AssetDatabase.SaveAssetIfDirty(copy);
                    var identity = Ids(copy); var json = EditorJsonUtility.ToJson(copy);
                    Resources.UnloadAsset(copy);
                    var loaded = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                    Check(Ids(loaded) == identity && EditorJsonUtility.ToJson(loaded) == json, "Unity save/reload preserves complete graph: " + file.Path);
                    Policies(loaded, "saved copy");
                }
                Check(TargetMaskDrawersCheck(), "Inspector masks and new-object defaults");
                Debug.Log($"TargetMaskMigrationChecks PASS {Results.Count}");
            }
            catch (Exception error) { Results.Add("FAIL " + error); throw; }
            finally
            {
                if (createdScratch) AssetDatabase.DeleteAsset(Scratch);
                File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../target-stage3-migration-tests.txt"), Results);
            }
        }
        private static bool TargetMaskDrawersCheck()
        {
            if (TargetRelationMaskDrawer.FromSelection(-1) != 31 || TargetRelationMaskDrawer.FromSelection(0) != 0) return false;
            var node = (NewMechanism)TargetMaskEditorDefaults.Create(typeof(DamageMechanism));
            if (node.Targeting.Version != 1 || node.Targeting.Relations != TargetRelationMask.None) return false;
            var asset = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                asset.mechanismData = new MechanismData { mechanism = node };
                using var so = new SerializedObject(asset);
                var field = so.FindProperty("mechanismData").FindPropertyRelative("mechanism");
                var mask = field.FindPropertyRelative("targetRelations"); var version = field.FindPropertyRelative("targetMaskVersion");
                if (mask == null || version.intValue != 1 || mask.intValue != 0) return false;
                var drawer = new TargetRelationMaskDrawer();
                var normal = drawer.GetPropertyHeight(mask, GUIContent.none);
                mask.intValue = 128;
                return drawer.GetPropertyHeight(mask, GUIContent.none) > normal;
            }
            finally { Object.DestroyImmediate(asset); }
        }
        /// <summary>적용된 콘텐츠를 재시작 후 확인합니다. 에셋은 변경하지 않습니다.</summary>
        public static void VerifyMigrated()
        {
            Results.Clear();
            var plan = TargetMaskMigration.Scan();
            Check(!plan.HasReview && plan.Files.Count == 0, "all saved Mechanisms migrated / idempotent");
            foreach (var path in plan.Rows.Select(r => r.Path).Distinct()) Policies(AssetDatabase.LoadAssetAtPath<SkillData>(path), path);
            Check(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/ObjectInstances/SkillData/SkillData 3.asset").mechanismData == null, "null managed reference remains null");
            File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../target-stage3-reload-tests.txt"), Results);
            Debug.Log($"TargetMaskMigrationReload PASS {Results.Count}");
        }
    }
}
