#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Moves;
using Moves.Mechanisms;
using Moves.ObjectEntity;
using PlayerScripts.Skills;
using Systems.Time;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using GameTime = Systems.Time.Time;

namespace ExitLab
{
    public sealed class TimeLifecyclePlayModeRunner : MonoBehaviour
    {
        private readonly List<string> _results = new();
        private readonly List<string> _errors = new();
        private void Check(bool value, string name)
        { if (!value) throw new Exception(name); _results.Add("PASS " + name); }
        private void Capture(string message, string stack, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) _errors.Add(message); }
        private sealed class Probe : DummyMechanism
        {
            public int Count;
            protected override void ExecuteCore(CastContext ctx) { Count++; }
        }
        private static int Subscribers(Ticker ticker) =>
            ((Delegate)typeof(Ticker).GetField("OnTick", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ticker))?.GetInvocationList().Length ?? 0;

        private IEnumerator Start()
        {
            Application.logMessageReceived += Capture;
            var suite = Checks(); var success = true;
            while (true)
            {
                bool more;
                try { more = suite.MoveNext(); }
                catch (Exception error) { _results.Add("FAIL " + error); success = false; break; }
                if (!more) break;
                yield return suite.Current;
            }
            (suite as IDisposable)?.Dispose();
            if (_errors.Count != 0) { success = false; _results.Add("FAIL runtime logs: " + string.Join(" | ", _errors)); }
            Application.logMessageReceived -= Capture;
            File.WriteAllLines(Path.GetFullPath(Application.dataPath + "/../../phase1-lifecycle-tests.txt"), _results);
            Debug.Log($"TimeLifecyclePlayMode {(success ? "PASS" : "FAIL")} {_results.Count}");
            EditorApplication.Exit(success ? 0 : 1);
        }

        private IEnumerator Checks()
        {
            var clockGo = new GameObject("lifecycle clock");
            var runner = clockGo.AddComponent<TickerRunner>();
            var ticker = GameTime.Ticker; var scheduler = GameTime.DelayScheduler;
            Check(ticker == Ticker.Instance && scheduler == DelayScheduler.Instance, "global aliases share owner clock");
            runner.enabled = false;
            var stopped = ticker.TickCount;
            var delay = scheduler.Start(stopped, 4);
            yield return new WaitForSecondsRealtime(.08f);
            Check(ticker.TickCount == stopped, "disabled runner does not advance");
            var subscriptions = Subscribers(ticker);
            GameTime.Initialize(); GameTime.Initialize();
            Check(Subscribers(ticker) == subscriptions, "Initialize does not duplicate observer");

            var service = new GameObject("lifecycle collector"); service.AddComponent<TargetResolver>();
            var collector = service.AddComponent<CommandCollector>();
            var probe = new Probe(); JsonUtility.FromJsonOverwrite("{\"delayTicks\":3}", probe);
            collector.EnqueueCommand(new SkillCommand(service.transform, TargetMode.TowardsCoordinate, default, probe, default, SkillTarget.At(Vector2.right)));
            ticker.Step();
            collector.enabled = false; collector.enabled = true;
            Check(Subscribers(ticker) == subscriptions + 1, "collector re-enable has one subscription");
            // Phase 4: 서비스 자체를 끄면 기존 명령은 폐기합니다. 시간 소유자의 일시 정지와 구분합니다.
            var cancelledProbe = probe;
            probe = new Probe(); JsonUtility.FromJsonOverwrite("{\"delayTicks\":3}", probe);
            collector.EnqueueCommand(new SkillCommand(service.transform, TargetMode.TowardsCoordinate, default, probe, default, SkillTarget.At(Vector2.right)));
            clockGo.SetActive(false);
            var paused = ticker.TickCount;
            yield return new WaitForSecondsRealtime(.08f);
            Check(ticker.TickCount == paused && probe.Count == 0, "inactive owner preserves scheduled work without advancing");
            clockGo.SetActive(true); runner.enabled = true;
            Check(ReferenceEquals(ticker, GameTime.Ticker) && ReferenceEquals(scheduler, GameTime.DelayScheduler), "re-enable retains clock and scheduler identity");
            Check(ticker.TickCount == paused && scheduler.GetRemaining(delay, paused) == 3, "re-enable neither resets nor advances clock");
            // OnEnable 중복 호출에도 하나의 코루틴만 유지하는지 실제 경과 시간과 함께 확인합니다.
            var enable = typeof(TickerRunner).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
            enable.Invoke(runner, null); enable.Invoke(runner, null);
            var realStart = UnityEngine.Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(.12f);
            runner.enabled = false;
            var delta = ticker.TickCount - paused;
            var realElapsed = UnityEngine.Time.realtimeSinceStartup - realStart;
            Check(delta > 0 && delta <= Math.Ceiling(realElapsed * Ticker.TicksPerSecond) + 1, "repeated enable does not multiply tick loops");
            Check(scheduler.IsCompleted(delay, ticker.TickCount) && probe.Count == 1, "pending delay and command resume exactly once");
            Check(cancelledProbe.Count == 0, "collector restart never resumes discarded command");
            var finalPause = ticker.TickCount;
            yield return new WaitForSecondsRealtime(.08f);
            Check(ticker.TickCount == finalPause, "second disable stops resumed loop");

            var areaGo = new GameObject("area subscription"); var area = areaGo.AddComponent<AreaEntity>();
            area.areaShape = new CircleArea { Radius = 0 };
            var settings = new AreaMechanism { lifeTick = 30, onEnter = new(), onExpire = new() };
            area.Init(new CastContext(settings, service.transform, SkillTarget.At(Vector2.zero), default));
            Check(Subscribers(ticker) == subscriptions + 2, "Area subscribes to the same session ticker");
            area.enabled = false;
            Check(Subscribers(ticker) == subscriptions + 1, "disabled Area detaches its ticker");
            ticker.Step(); area.enabled = true;
            Check(Subscribers(ticker) == subscriptions + 2, "Area re-enable restores exactly one subscription");
            Object.Destroy(areaGo); yield return null;
            Check(Subscribers(ticker) == subscriptions + 1, "destroyed Area leaves no ticker subscription");

            Object.Destroy(service); yield return null;
            Check(Subscribers(ticker) == subscriptions, "collector destruction detaches original ticker");
            Object.Destroy(clockGo); yield return null;
            Check(GameTime.Ticker == null && Ticker.Instance == null && GameTime.DelayScheduler == null && DelayScheduler.Instance == null, "owner destruction clears owned aliases");
            Check(Subscribers(ticker) == 0, "owner destruction detaches time observer");
            var replacementGo = new GameObject("replacement clock"); var replacement = replacementGo.AddComponent<TickerRunner>(); replacement.enabled = false;
            Check(!ReferenceEquals(ticker, GameTime.Ticker) && GameTime.Ticker.TickCount == 0, "new owner starts distinct session at zero");
            var newDelay = GameTime.DelayScheduler.Start(0, 1); GameTime.DelayScheduler.Remove(delay);
            Check(!newDelay.Equals(delay) && GameTime.DelayScheduler.IsCompleted(newDelay, 1), "old session handle cannot remove new session delay");
            Object.Destroy(replacementGo); yield return null;
        }
    }
}
#endif
