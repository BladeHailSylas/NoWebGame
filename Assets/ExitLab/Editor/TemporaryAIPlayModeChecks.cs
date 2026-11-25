using global::NoWebGame.Entities.Enemy;
using global::NoWebGame.Systems.Time;
using System;
using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using GameTime = global::NoWebGame.Systems.Time.Time;

namespace ExitLab
{
    /// <summary>별도 batch 검증 사본에서 실제 Start/OnEnable, 틱과 물리 이동까지 확인합니다.</summary>
    [InitializeOnLoad]
    public static class TemporaryAIPlayModeChecks
    {
        private const string Key = "ExitLab.TemporaryAIPlayModeChecks";
        static TemporaryAIPlayModeChecks()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Observe;
        }

        // 이 진입점은 batch 전용이며 완료 시 프로세스를 종료합니다. 대화형 메뉴에는 노출하지 않습니다.
        public static void Begin()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 batch 검증 사본에서만 실행하세요.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("AI Play Mode fixture").AddComponent<TemporaryAIPlayground>();
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".stage", 0);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 90);
            EditorApplication.update -= Observe;
            EditorApplication.update += Observe;
            EditorApplication.isPlaying = true;
        }

        private static void Observe()
        {
            if (!SessionState.GetBool(Key, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
                    throw new Exception("Play Mode verification timed out.");
                if (!EditorApplication.isPlaying || GameTime.Ticker == null) return;
                var enemy = Object.FindFirstObjectByType<EnemyEntity>();
                if (!enemy) return;
                var tick = GameTime.Ticker.TickCount;
                var stage = SessionState.GetInt(Key + ".stage", 0);
                if (stage == 0 && tick >= 75)
                {
                    var target = GameObject.Find("You target").GetComponent<TemporaryAITarget>();
                    Require(enemy.AIDecisionCount == 1 && target.HitCount == 1, "60-tick decision reaches actual damage pipeline");
                    foreach (var item in Object.FindObjectsByType<TemporaryAITarget>(FindObjectsSortMode.None)) item.targetable = false;
                    SessionState.SetVector3(Key + ".position", enemy.transform.position);
                    SessionState.SetInt(Key + ".stage", 1);
                }
                else if (stage == 1 && tick >= 210)
                {
                    Require(Vector3.Distance(enemy.transform.position, SessionState.GetVector3(Key + ".position", default)) > .1f,
                        "no-target AI actually moves Rigidbody2D");
                    Require(enemy.AIMovement.sqrMagnitude > .99f, "walking input remains normalized");
                    enemy.TemporaryAIEnabled = false;
                    SessionState.SetVector3(Key + ".position", enemy.transform.position);
                    SessionState.SetInt(Key + ".stage", 2);
                }
                else if (stage == 2 && tick >= 240)
                {
                    Require(enemy.AIMovement == Vector2.zero, "AI disable clears movement input");
                    Require(Vector3.Distance(enemy.transform.position, SessionState.GetVector3(Key + ".position", default)) < .15f,
                        "AI remains stopped after disable");
                    Finish(0, "PASS: actual Play Mode startup, Attack, random Rigidbody2D movement and disable (5 assertions).");
                }
            }
            catch (Exception error) { Finish(1, "FAIL: " + error); }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int exitCode, string message)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Observe;
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../temporary-ai-playmode-tests.txt"));
            File.WriteAllText(output, message);
            Debug.Log(message);
            EditorApplication.Exit(exitCode);
        }
    }
}
