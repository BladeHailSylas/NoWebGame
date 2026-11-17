using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExitLab
{
    [InitializeOnLoad]
    public static class DestinationPlayModeLauncher
    {
        static DestinationPlayModeLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Destination.Run", false)) return;
                SessionState.SetBool("Destination.Run", false);
                new GameObject("Destination checks").AddComponent<DestinationPlayModeRunner>();
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker")) throw new InvalidOperationException("Validation copy only.");
            SessionState.SetBool("Destination.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }
}
