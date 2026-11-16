using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ExitLab
{
    public static class TemporaryAIPlaygroundMenu
    {
        [MenuItem("Tools/ExitLab/Open Temporary AI Playground")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Play Mode를 종료한 뒤 열어 주세요."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Temporary AI Playground (press Play)").AddComponent<TemporaryAIPlayground>();
            // 자동 저장하지 않습니다. Play 중 설정과 SkillData는 메모리에만 존재합니다.
        }
    }
}
