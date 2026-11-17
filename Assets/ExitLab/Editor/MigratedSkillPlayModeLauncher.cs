using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExitLab
{
    [InitializeOnLoad]
    public static class MigratedSkillPlayModeLauncher
    {
        static MigratedSkillPlayModeLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("MigratedSkill.Run", false)) return;
                SessionState.SetBool("MigratedSkill.Run", false);
                new GameObject("Migrated content runner").AddComponent<MigratedSkillPlayModeRunner>();
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Run only in a marked validation copy.");
            SessionState.SetBool("MigratedSkill.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }
}
