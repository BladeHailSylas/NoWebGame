using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExitLab
{
    [InitializeOnLoad]
    public static class TravelRegressionLauncher
    {
        static TravelRegressionLauncher()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("TravelRegression.Run", false)) return;
                SessionState.SetBool("TravelRegression.Run", false);
                new GameObject("Travel regression runner").AddComponent<TravelRegressionRunner>();
            };
        }
        public static void Run()
        {
            SessionState.SetBool("TravelRegression.Run", true);
            // 기존 씬을 저장하거나 덮어쓰지 않고 빈 임시 씬에서 실행합니다.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }
}
