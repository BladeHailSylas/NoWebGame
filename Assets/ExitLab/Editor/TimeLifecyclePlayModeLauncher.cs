using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExitLab
{
    [InitializeOnLoad]
    public static class TimeLifecyclePlayModeLauncher
    {
        static TimeLifecyclePlayModeLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("TimeLifecycle.Run", false)) return;
                SessionState.SetBool("TimeLifecycle.Run", false);
                new GameObject("Time lifecycle checks").AddComponent<TimeLifecyclePlayModeRunner>();
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Validation copy only.");
            SessionState.SetBool("TimeLifecycle.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }
}
