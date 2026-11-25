using global::NoWebGame.Entities.Core;
using global::NoWebGame.Entities.Relations;
using System;
using System.Collections.Generic;
using System.IO;


using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ExitLab
{
    /// <summary>실제 Unity Entity/Collider로 관계 기반만 검사합니다. Scene·에셋을 저장하지 않습니다.</summary>
    public static class EntityRelationChecks
    {
        [MenuItem("Tools/ExitLab/Check Entity Relations")]
        public static void Run()
        {
            var checks = new List<string>();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var you = LayerMask.NameToLayer("You");
                var ally = LayerMask.NameToLayer("Ally");
                var foe = LayerMask.NameToLayer("Foe");
                Check(you >= 0 && ally >= 0 && foe >= 0, "required legacy layers exist", checks);
                var entities = new[]
                {
                    Create(scene, "You", you), Create(scene, "Ally", ally), Create(scene, "Foe", foe),
                    Create(scene, "Neutral", 0, EntityRelationProfile.ExplicitNeutral),
                    Create(scene, "Unrelated", 0, EntityRelationProfile.ExplicitUnrelated),
                    Create(scene, "Unknown layer", 0),
                    Create(scene, "Unknown profile", you, (EntityRelationProfile)99)
                };
                // 합의된 관계 교차표를 구현과 독립된 기대값으로 고정합니다.
                var expected = new[,]
                {
                    { EntityRelation.Self, EntityRelation.Ally, EntityRelation.Enemy, EntityRelation.Neutral, EntityRelation.Unrelated, EntityRelation.Unknown, EntityRelation.Unknown },
                    { EntityRelation.Ally, EntityRelation.Self, EntityRelation.Enemy, EntityRelation.Neutral, EntityRelation.Unrelated, EntityRelation.Unknown, EntityRelation.Unknown },
                    { EntityRelation.Enemy, EntityRelation.Enemy, EntityRelation.Self, EntityRelation.Neutral, EntityRelation.Unrelated, EntityRelation.Unknown, EntityRelation.Unknown },
                    { EntityRelation.Neutral, EntityRelation.Neutral, EntityRelation.Neutral, EntityRelation.Self, EntityRelation.Neutral, EntityRelation.Unknown, EntityRelation.Unknown },
                    { EntityRelation.Unrelated, EntityRelation.Unrelated, EntityRelation.Unrelated, EntityRelation.Neutral, EntityRelation.Self, EntityRelation.Unknown, EntityRelation.Unknown },
                    { EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Self, EntityRelation.Unknown },
                    { EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Unknown, EntityRelation.Self }
                };
                for (var i = 0; i < entities.Length; i++)
                    for (var j = 0; j < entities.Length; j++)
                        Check(EntityRelationResolver.Resolve(entities[i], entities[j]) == expected[i, j],
                            $"relation matrix [{i},{j}]", checks);

                var secondYou = Create(scene, "Another You", you);
                var secondFoe = Create(scene, "Another Foe", foe);
                Check(EntityRelationResolver.Resolve(entities[0], secondYou) == EntityRelation.Ally,
                    "same layer different Entity is Ally, not Self", checks);
                Check(EntityRelationResolver.Resolve(entities[2], secondFoe) == EntityRelation.Ally,
                    "Foe to Foe is Ally", checks);
                Check(EntityRelationResolver.Resolve(entities[3], Create(scene, "Neutral 2", 0,
                    EntityRelationProfile.ExplicitNeutral)) == EntityRelation.Neutral, "distinct neutral entities", checks);
                Check(EntityRelationResolver.Resolve(entities[4], Create(scene, "Unrelated 2", 0,
                    EntityRelationProfile.ExplicitUnrelated)) == EntityRelation.Unrelated, "distinct unrelated entities", checks);

                Check(EntityRelationResolver.Resolve(null, null) == EntityRelation.Unknown, "null pair is not Self", checks);
                Check(EntityRelationResolver.Resolve(null, entities[3]) == EntityRelation.Unknown, "missing caster before Neutral", checks);
                Check(EntityRelationResolver.Resolve(entities[0], null) == EntityRelation.Unknown, "missing target", checks);
                var destroyed = Create(scene, "destroyed", foe);
                Object.DestroyImmediate(destroyed.gameObject);
                Check(EntityRelationResolver.Resolve(destroyed, destroyed) == EntityRelation.Unknown, "destroyed pair is not Self", checks);
                Check(EntityRelationResolver.Resolve(entities[0], destroyed) == EntityRelation.Unknown, "destroyed target", checks);
                Check(EntityRelationResolver.Resolve(destroyed, entities[0]) == EntityRelation.Unknown, "destroyed caster", checks);

                var child = new GameObject("child Collider");
                SceneManager.MoveGameObjectToScene(child, scene);
                child.transform.SetParent(entities[2].transform);
                child.layer = 0; // 부모 소속과 자식의 물리 Layer를 의도적으로 다르게 지정합니다.
                var collider = child.AddComponent<CircleCollider2D>();
                Check(EntityRelationResolver.ResolveFromCollider(entities[0], collider) == EntityRelation.Enemy,
                    "child Collider uses parent Entity layer", checks);
                Check(EntityRelationResolver.ResolveFromCollider(entities[2], collider) == EntityRelation.Self,
                    "child Collider shares Entity identity", checks);
                Check(EntityRelationResolver.ResolveFromTransforms(child.transform, entities[2].transform) == EntityRelation.Self,
                    "child caster is normalized", checks);
                var nested = child.AddComponent<ExitLabEntity>();
                nested.RelationProfile = EntityRelationProfile.ExplicitNeutral;
                Check(EntityRelationResolver.ResolveFromCollider(entities[0], collider) == EntityRelation.Neutral,
                    "nested Entity is nearest owner, not transform root", checks);
                Object.DestroyImmediate(nested);
                Check(EntityRelationResolver.ResolveFromCollider(entities[0], null) == EntityRelation.Unknown,
                    "missing Collider", checks);
                var orphan = new GameObject("non Entity");
                SceneManager.MoveGameObjectToScene(orphan, scene);
                Check(EntityRelationResolver.ResolveFromCollider(entities[0], orphan.AddComponent<CircleCollider2D>()) == EntityRelation.Unknown,
                    "Collider without Entity", checks);
                Check(EntityRelationResolver.ResolveFromTransforms(orphan.transform, entities[0].transform) == EntityRelation.Unknown,
                    "Transform without caster Entity", checks);
                Check(EntityRelationResolver.ResolveFromTransforms(null, null) == EntityRelation.Unknown,
                    "missing Transform pair", checks);

                entities[0].targetable = false;
                entities[2].targetable = false;
                entities[0].enabled = false;
                entities[2].gameObject.SetActive(false);
                Check(EntityRelationResolver.Resolve(entities[0], entities[2]) == EntityRelation.Enemy,
                    "targetability and activation do not erase relation facts", checks);
                Check(EntityRelationResolver.ResolveFromCollider(entities[0], collider) == EntityRelation.Enemy,
                    "inactive child still resolves its Entity identity", checks);
                Check(EntityRelationResolver.ResolveFromTransforms(child.transform, entities[0].transform) == EntityRelation.Enemy,
                    "inactive child caster keeps relation information", checks);
                entities[0].enabled = true;
                entities[2].gameObject.SetActive(true);
                entities[0].targetable = entities[2].targetable = true;

                // Owner가 있어도 독립 Skill의 Caster 정체성을 소유자로 바꾸지 않습니다.
                secondFoe.Owner = entities[0].transform;
                Check(EntityRelationResolver.Resolve(secondFoe, secondFoe) == EntityRelation.Self,
                    "independent summon caster keeps Self", checks);
                Check(EntityRelationResolver.Resolve(secondFoe, entities[0]) == EntityRelation.Enemy,
                    "Owner does not override faction or caster", checks);
                Check(EntityRelationResolver.Resolve(secondFoe, entities[2]) == EntityRelation.Ally,
                    "summon relationship is based on its own affiliation", checks);
                secondFoe.gameObject.layer = ally;
                Check(EntityRelationResolver.Resolve(secondFoe, entities[0]) == EntityRelation.Ally,
                    "relation reflects current layer", checks);
                secondFoe.RelationProfile = EntityRelationProfile.ExplicitNeutral;
                Check(EntityRelationResolver.Resolve(secondFoe, entities[0]) == EntityRelation.Neutral,
                    "relation reflects current profile", checks);

                CheckMasks(checks);
                CheckLegacy(you, ally, foe, checks);
                Check(entities[0].RelationProfile == EntityRelationProfile.LegacyLayer,
                    "new Entity default preserves legacy layer mode", checks);
                var fresh = Create(scene, "old serialized data", you);
                EditorJsonUtility.FromJsonOverwrite("{\"targetable\":false}", fresh);
                Check(fresh.RelationProfile == EntityRelationProfile.LegacyLayer, "missing profile retains zero legacy default", checks);
                fresh.RelationProfile = EntityRelationProfile.ExplicitUnrelated;
                var copy = Create(scene, "profile copy", you);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(fresh), copy);
                Check(copy.RelationProfile == EntityRelationProfile.ExplicitUnrelated, "profile serialization roundtrip", checks);
                checks.Add($"PASS: total {checks.Count} assertions");
                Debug.Log($"EntityRelationChecks: {checks[checks.Count - 1]}");
            }
            catch (Exception error)
            {
                checks.Add("FAIL: " + error);
                throw;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../audit-results/target-relation-stage1-tests.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllLines(output, checks);
            }
        }

        private static Entity Create(Scene scene, string name, int layer,
            EntityRelationProfile profile = EntityRelationProfile.LegacyLayer)
        {
            var go = new GameObject(name) { layer = layer };
            SceneManager.MoveGameObjectToScene(go, scene);
            var entity = go.AddComponent<ExitLabEntity>();
            if (profile != EntityRelationProfile.LegacyLayer) entity.RelationProfile = profile;
            return entity;
        }

        private static void CheckMasks(List<string> checks)
        {
            var relations = new[] { EntityRelation.Self, EntityRelation.Ally, EntityRelation.Enemy,
                EntityRelation.Neutral, EntityRelation.Unrelated };
            var masks = new[] { TargetRelationMask.Self, TargetRelationMask.Ally, TargetRelationMask.Enemy,
                TargetRelationMask.Neutral, TargetRelationMask.Unrelated };
            for (var bit = 0; bit < masks.Length; bit++)
                Check((int)masks[bit] == 1 << bit, $"stable serialized mask bit {bit}", checks);
            for (var value = 0; value < 32; value++)
            {
                var mask = (TargetRelationMask)value;
                Check(TargetRelationMasks.IsValid(mask), $"valid mask {value}", checks);
                for (var bit = 0; bit < relations.Length; bit++)
                    Check(TargetRelationMasks.Allows(mask, relations[bit]) == ((value & (1 << bit)) != 0),
                        $"mask {value} relation {relations[bit]}", checks);
                Check(!TargetRelationMasks.Allows(mask, EntityRelation.Unknown), $"mask {value} rejects Unknown", checks);
                Check(!TargetRelationMasks.Allows(mask, (EntityRelation)99), $"mask {value} rejects undefined relation", checks);
            }
            foreach (var value in new[] { -1, 32, 33, 128, int.MinValue })
            {
                Check(!TargetRelationMasks.IsValid((TargetRelationMask)value), $"invalid mask {value}", checks);
                foreach (EntityRelation relation in Enum.GetValues(typeof(EntityRelation)))
                    Check(!TargetRelationMasks.Allows((TargetRelationMask)value, relation),
                        $"invalid mask {value} rejects {relation}", checks);
            }
        }

        private static void CheckLegacy(int you, int ally, int foe, List<string> checks)
        {
            var layers = new[] { you, ally, foe, 0 };
            // 기존 API가 Self를 포함하여 같은 편으로 판정하는 동작도 보존합니다.
            var allies = new[,] { { true, true, false, false }, { true, true, false, false },
                { false, false, true, false }, { false, false, false, false } };
            var enemies = new[,] { { false, false, true, false }, { false, false, true, false },
                { true, true, false, false }, { false, false, false, false } };
            for (var i = 0; i < layers.Length; i++)
                for (var j = 0; j < layers.Length; j++)
                {
                    Check(AllyEnemyChecker.IsAlly(layers[j], layers[i]) == allies[i, j], $"legacy IsAlly [{i},{j}]", checks);
                    Check(AllyEnemyChecker.IsEnemy(layers[j], layers[i]) == enemies[i, j], $"legacy IsEnemy [{i},{j}]", checks);
                }
        }

        private static void Check(bool condition, string name, List<string> checks)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add("PASS: " + name);
        }
    }
}
