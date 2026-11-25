using global::NoWebGame.Combat.Definitions;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Data;
using System;
using System.Collections.Generic;
using System.IO;


using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExitLab
{
    public static class TargetContractChecks
    {
        public static void Run()
        {
            var checks = new List<string>();
            var root = new GameObject("Target contract check");
            var child = new GameObject("child collider");
            child.transform.SetParent(root.transform);
            try
            {
                var entity = root.AddComponent<ExitLabEntity>();
                var collider = child.AddComponent<CircleCollider2D>();
                Check(EntityIdentity.FromCollider(collider) == entity, "parent Entity", checks);
                var target = SkillTarget.ForEntity(entity);
                var point = SkillTarget.At(Vector2.right);
                root.transform.position = new Vector2(4, 2);
                Check(target.TryGetPosition(out var p) && p == new Vector2(4, 2), "live Entity position", checks);
                Check(point.TryGetPosition(out p) && p == Vector2.right, "fixed Position", checks);
                Check(SkillTarget.At(Vector2.zero).IsResolved && !default(SkillTarget).IsResolved, "origin vs unresolved", checks);
                var commandA = new SkillCommand(root.transform, TargetMode.TowardsEntity, default, null, default, target, masker: 1 << 7);
                var commandB = new SkillCommand(root.transform, TargetMode.TowardsCursor, new global::NoWebGame.Systems.Data.FixedVector2(9, 0), null, default, target);
                Check(commandA.Equals(commandB) && commandA.GetHashCode() == commandB.GetHashCode(), "command equality/hash consistency", checks);
                Check(commandA.Mask.value == (1 << 7), "LayerMask is preserved, not shifted twice", checks);
                entity.targetable = false;
                Check(!target.TryGetPosition(out _) && target.IsResolved, "lost is not unresolved", checks);
                entity.targetable = true;
                root.SetActive(false);
                Check(!target.IsValid, "inactive target", checks);
                root.SetActive(true);
                Object.DestroyImmediate(entity);
                Check(!target.TryGetPosition(out _) && target.IsResolved, "destroyed target", checks);

                var gate = new TravelCompletion();
                var cleanup = 0; var publish = 0;
                var result = new TravelResult(ExitKind.Interrupted, ExitCause.TargetLost, new Vector2(5, 7));
                gate.TryFinish(result, () => { cleanup++; gate.TryFinish(result, () => cleanup++, _ => publish++); }, _ => publish++);
                gate.TryFinish(result, () => cleanup++, _ => publish++);
                Check(cleanup == 1 && publish == 1 && gate.Result.Value.Position == new Vector2(5, 7), "exactly once with reentry", checks);

                var legacy = JsonUtility.FromJson<ProjectileMechanism>("{\"lifeTick\":120,\"penetrative\":true}");
                Check(legacy.callbackMode == ExitCallbackMode.Legacy && legacy.timeoutPolicy == TimeoutPolicy.Completed,
                    "absent fields retain legacy defaults", checks);
                var data = ScriptableObject.CreateInstance<SkillData>();
                var copy = ScriptableObject.CreateInstance<SkillData>();
                try
                {
                    data.mechanismData = new MechanismData { mechanism = new DashMechanism {
                        durationTicks = 120, callbackMode = ExitCallbackMode.Classified, timeoutPolicy = TimeoutPolicy.Interrupted,
                        onInterrupted = new List<MechanismData> { new() { targetSource = ChainTargetSource.ExitPosition,
                            mechanism = new ProjectileMechanism { lifeTick = 240 } } } } };
                    EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(data), copy);
                    var dash = (DashMechanism)copy.mechanismData.mechanism;
                    Check(dash.callbackMode == ExitCallbackMode.Classified && dash.timeoutPolicy == TimeoutPolicy.Interrupted &&
                        dash.onInterrupted[0].targetSource == ChainTargetSource.ExitPosition &&
                        ((ProjectileMechanism)dash.onInterrupted[0].mechanism).lifeTick == 240, "nested SerializeReference roundtrip", checks);

                    // 신규 검증 에셋만 저장/재로드합니다. 기존 콘텐츠에는 변환을 적용하지 않습니다.
                    const string directory = "Assets/ExitLab/RegressionSamples";
                    if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets/ExitLab", "RegressionSamples");
                    var path = AssetDatabase.GenerateUniqueAssetPath(directory + "/NestedExitContract.asset");
                    var saved = Object.Instantiate(data);
                    AssetDatabase.CreateAsset(saved, path);
                    AssetDatabase.SaveAssetIfDirty(saved);
                    Resources.UnloadAsset(saved);
                    var loaded = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                    var loadedDash = (DashMechanism)loaded.mechanismData.mechanism;
                    Check(loadedDash.callbackMode == ExitCallbackMode.Classified && loadedDash.onInterrupted.Count == 1 &&
                        ((ProjectileMechanism)loadedDash.onInterrupted[0].mechanism).lifeTick == 240, "saved asset reload with nested managed references", checks);
                }
                finally { Object.DestroyImmediate(data); Object.DestroyImmediate(copy); }
                Directory.CreateDirectory("../audit-results/target-refactor");
                File.WriteAllLines("../audit-results/target-refactor/stage2-contract-checks.txt", checks);
                Debug.Log($"TargetContractChecks: PASS ({checks.Count})");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Check(bool condition, string label, List<string> checks)
        {
            if (!condition) throw new Exception("FAIL: " + label);
            checks.Add("PASS: " + label);
        }
    }
}
