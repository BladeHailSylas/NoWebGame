using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExitLab
{
    [InitializeOnLoad]
    public static class RelativeTargetPlayModeLauncher
    {
        static RelativeTargetPlayModeLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("RelativeTarget.Run", false)) return;
                SessionState.SetBool("RelativeTarget.Run", false);
                new GameObject("Relative target runner").AddComponent<RelativeTargetPlayModeRunner>();
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("분리된 batch 검증 사본에서만 실행하세요.");
            SessionState.SetBool("RelativeTarget.Run", true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }
}
