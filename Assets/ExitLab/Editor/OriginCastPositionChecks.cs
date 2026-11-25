using global::NoWebGame.Combat.Effects.Runtime;
using global::NoWebGame.Combat.Mechanisms;
using global::NoWebGame.Moves;
using global::NoWebGame.Moves.ObjectEntity;
using global::NoWebGame.Combat.Casting;
using global::NoWebGame.Combat.Targeting;
using global::NoWebGame.Movement.Travel;
using global::NoWebGame.Systems.Data;
using global::NoWebGame.Systems.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;






using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using GameTime = global::NoWebGame.Systems.Time.Time;

namespace ExitLab
{
    /// <summary>시전 원점 계약을 실제 Unity 객체와 CommandCollector로 검사합니다.</summary>
    public static class OriginCastPositionChecks
    {
        private sealed class Capture : INewMechanism
        {
            public short CooldownTicks => 0;
            public byte DelayTicks => 2;
            public float MinRange => 0;
            public float MaxRange => 100;
            public LayerMask Mask => 0;
            public CastContext Last;
            public int Count;
            public void Execute(CastContext ctx) { Last = ctx; Count++; }
        }

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var checks = new List<string>();
            var caster = new GameObject("origin caster");
            var services = new GameObject("origin services");
            services.SetActive(false);
            // 런타임 어셈블리의 internal setter는 검증에서만 reflection으로 접근합니다.
            typeof(GameTime).GetProperty("Ticker").SetValue(null, new Ticker());
            typeof(GameTime).GetProperty("DelayScheduler").SetValue(null, new DelayScheduler());
            var resolver = services.AddComponent<TargetResolver>();
            var collector = services.AddComponent<CommandCollector>();
            services.SetActive(true);
            // EditMode에서는 일반 MonoBehaviour의 실행 수명주기를 명시적으로 시작합니다.
            typeof(CommandCollector).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(collector, null);
            var capture = new Capture();
            var target = SkillTarget.At(new Vector2(8, 2));
            var runner = new SkillRunner(resolver);
            try
            {
                caster.transform.position = new Vector2(1, 1);
                var root = new SkillCommand(caster.transform, TargetMode.TowardsCoordinate,
                    new FixedVector2(caster.transform.position), capture, default, target);
                collector.EnqueueCommand(root);
                GameTime.Ticker.Step();
                caster.transform.position = new Vector2(3, 2);
                GameTime.Ticker.Step();
                Check(capture.Count == 0, "root waits for delay", checks);
                GameTime.Ticker.Step();
                var original = capture.Last;
                Check(original.OriginCastPosition == new Vector2(3, 2) && root.OriginCastPosition == null,
                    "snapshot at execution, not enqueue", checks);
                var exit = new TravelResult(ExitKind.Interrupted, ExitCause.WallBlocked, new Vector2(6, 2));
                var ended = original.WithExit(exit);
                Check(ended.OriginCastPosition == original.OriginCastPosition && ended.Target.Equals(target) &&
                    ended.Exit.Value.Position == exit.Position, "WithExit preserves origin and original Target", checks);
                var queued = new List<SkillCommand>();
                collector.CommandEnqueued += queued.Add;
                SkillUtils.ActivateChain(new[] {
                    new MechanismData { mechanism = capture },
                    new MechanismData { mechanism = capture, targetSource = ChainTargetSource.ExitPosition },
                    new MechanismData { mechanism = capture, requireRetarget = true,
                        mode = TargetMode.TowardsSelf, targetSource = ChainTargetSource.ExitPosition }
                }, ended);
                Check(queued.Count == 3 && queued.TrueForAll(c => c.OriginCastPosition == original.OriginCastPosition),
                    "all child Commands preserve origin", checks);
                Check(queued[0].Target.Equals(target) && queued[1].Target.TryGetPosition(out var p) && p == exit.Position &&
                    !queued[2].Target.IsResolved, "inherit / ExitPosition / retarget priority unchanged", checks);
                caster.transform.position = new Vector2(20, 9);
                for (var i = 0; i < 3; i++) GameTime.Ticker.Step();
                Check(capture.Last.OriginCastPosition == original.OriginCastPosition &&
                    capture.Last.Target.TryGetPosition(out p) && p == new Vector2(20, 9),
                    "retarget executes at new location but retains origin", checks);
                caster.transform.position = Vector2.zero;
                runner.Activate(root);
                Check(capture.Last.OriginCastPosition.HasValue && capture.Last.OriginCastPosition.Value == Vector2.zero,
                    "independent cast at world zero is valid", checks);
                caster.transform.position = new Vector2(10, 10);
                runner.Activate(new SkillCommand(caster.transform, TargetMode.TowardsCoordinate, default,
                    capture, default, target, originCastPosition: Vector2.zero));
                Check(capture.Last.OriginCastPosition == Vector2.zero, "inherited world zero is not reinitialized", checks);
                var unset = new CastContext(capture, caster.transform, target, default);
                Check(unset.OriginCastPosition == null && unset.WithExit(exit).OriginCastPosition == null,
                    "constructors and WithExit retain unset state", checks);
                var alternate = new CastContext(capture, caster.transform, target, default, TargetMode.TowardsCoordinate,
                    originCastPosition: Vector2.zero);
                Check(alternate.WithExit(exit).OriginCastPosition == Vector2.zero, "alternate constructor retains origin", checks);

                // 빈 실패 분기가 아니라 실제 자식 Command를 관측합니다.
                var detect = new DetectMechanism { requiredMode = TargetMode.TowardsCoordinate,
                    onNotFound = new[] { new MechanismData { mechanism = capture } } };
                queued.Clear();
                runner.Activate(new SkillCommand(caster.transform, TargetMode.TowardsCoordinate, default, detect, default));
                Check(queued.Count == 1 && queued[0].OriginCastPosition == new Vector2(10, 10) && !queued[0].Target.IsResolved,
                    "Detect failure starts and propagates snapshot", checks);
                collector.CeaseCommand();

                AreaCheck(caster.transform, new BoxArea { Width = 1000, Height = 2000 }, Vector2.zero, checks);
                AreaCheck(caster.transform, new BoxArea { Width = 1000, Height = 2000 }, null, checks);
                AreaCheck(caster.transform, new BoxArea { Width = 1000, Height = 2000 }, new Vector2(4, 0), checks);
                AreaCheck(caster.transform, new LaserArea { Width = 1000 }, Vector2.zero, checks);
                AreaCheck(caster.transform, new CircleArea { Radius = 1000 }, Vector2.zero, checks);
                Debug.Log($"OriginCastPositionChecks: PASS ({checks.Count})");
            }
            catch (Exception e) { checks.Add("FAIL: " + e); throw; }
            finally
            {
                Directory.CreateDirectory("../audit-results/origin-cast-position");
                File.WriteAllLines("../audit-results/origin-cast-position/checks.txt", checks);
                typeof(CommandCollector).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(collector, null);
                Object.DestroyImmediate(services);
                Object.DestroyImmediate(caster);
            }
        }

        private static void AreaCheck(Transform caster, IAreaShapes shape, Vector2? origin, List<string> checks)
        {
            caster.position = new Vector2(0, 3);
            var template = new GameObject("origin area template");
            template.SetActive(false);
            var prefab = template.AddComponent<AreaEntity>();
            prefab.areaShape = shape;
            var mech = new AreaMechanism { areaPrefab = prefab, lifeTick = 120,
                onEnter = new List<MechanismData>(), onExpire = new List<MechanismData>() };
            typeof(NewMechanism).GetField("maxRange", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mech, 100000);
            try
            {
                mech.Execute(new CastContext(mech, caster, SkillTarget.At(new Vector2(4, 0)), default,
                    originCastPosition: origin));
                AreaEntity clone = null;
                foreach (var area in Resources.FindObjectsOfTypeAll<AreaEntity>())
                    if (area != prefab && area.gameObject.name == template.name + "(Clone)") clone = area;
                Check(clone && (Vector2)clone.transform.position == new Vector2(4, 0), "Area center remains Target position", checks);
                if (clone.areaShape is BoxArea box)
                {
                    var dir = new Vector2(4, 0) - (origin ?? (Vector2)caster.position);
                    if (dir.sqrMagnitude < .000001f) dir = caster.right;
                    var expected = Quaternion.LookRotation(Vector3.forward, dir.normalized);
                    Check(Quaternion.Angle(clone.transform.rotation, expected) < .001f &&
                        Mathf.Abs(Mathf.DeltaAngle(box.RotationZ, expected.eulerAngles.z)) < .001f,
                        "Box rotation snapshot / null / zero-direction: " + origin, checks);
                }
                else if (clone.areaShape is LaserArea laser)
                    Check(laser.Start.AsVector2 == (Vector2)caster.position && laser.End.AsVector2 == new Vector2(4, 0),
                        "Laser retains live Caster start and Target end", checks);
                else
                    Check(Quaternion.Angle(clone.transform.rotation, Quaternion.LookRotation(Vector3.forward,
                        new Vector3(4, -3).normalized)) < .001f, "Circle retains live rotation", checks);
            }
            finally
            {
                foreach (var area in Resources.FindObjectsOfTypeAll<AreaEntity>())
                    if (area != prefab && area.gameObject.name == template.name + "(Clone)") Object.DestroyImmediate(area.gameObject);
                Object.DestroyImmediate(template);
            }
        }

        private static void Check(bool condition, string label, List<string> checks)
        {
            if (!condition) throw new Exception(label);
            checks.Add("PASS: " + label);
        }
    }
}
