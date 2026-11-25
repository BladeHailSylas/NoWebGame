using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Editor;
using NoWebGame.Moves;
using NoWebGame.Combat.Mechanisms;
using NoWebGame.Moves.ObjectEntity;
using UnityEditor;
using UnityEngine;

namespace ExitLab
{
    /// <summary>이관 후에도 과거 YAML과 Inspector 타입 생성 계약을 읽을 수 있는지 검사합니다.</summary>
    public static class NamespaceMigrationChecks
    {
        private static readonly List<string> Results = new();
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            Results.Add("PASS " + label);
        }

        private static string Entry(string ns, string name = "DamageMechanism", long rid = 1) =>
            "    - rid: " + rid + "\n      type: {class: " + name + ", ns: " + ns + ", asm: Assembly-CSharp}\n" +
            "      data:\n        targetMaskVersion: 1\n        targetRelations: 4\n";

        private static List<TargetMaskMigration.Row> Analyze(string text)
        {
            var rows = new List<TargetMaskMigration.Row>();
            TargetMaskMigration.AnalyzeText("memory-only", text, rows);
            return rows;
        }

        public static void Run()
        {
            if (!Application.isBatchMode || !File.Exists("TargetMaskMigrationTestCopy.marker"))
                throw new InvalidOperationException("Run only in a marked validation copy.");
            Results.Clear();
            var assembly = typeof(NewMechanism).Assembly;
            foreach (var pair in NamespaceTypeMap.Entries)
                Check(assembly.GetType(pair.Value) != null && assembly.GetType(pair.Key) == null, "type identity " + pair.Value);

            const string legacy = "Moves.Mechanisms";
            const string current = "NoWebGame.Combat.Mechanisms";
            Check(NamespaceTypeMap.Resolve(legacy, "DamageMechanism", "Assembly-CSharp") == typeof(DamageMechanism), "legacy explicit resolver");
            Check(NamespaceTypeMap.Resolve(current, "DamageMechanism", "Assembly-CSharp") == typeof(DamageMechanism), "current explicit resolver");
            foreach (var ns in new[] { legacy, current })
            {
                var rows = Analyze(Entry(ns));
                Check(rows.Count == 1 && rows[0].Status == "UNCHANGED", ns + " valid YAML");
                var legacyMaskText = Entry(ns).Replace("        targetMaskVersion: 1\n        targetRelations: 4\n",
                    "        mask:\n          serializedVersion: 2\n          m_Bits: 128\n");
                var legacyRows = new List<TargetMaskMigration.Row>();
                var legacyPlan = TargetMaskMigration.AnalyzeText("memory-only", legacyMaskText, legacyRows);
                Check(legacyRows.Count == 1 && legacyRows[0].Status == "CONVERT" && legacyRows[0].Relations == 4, ns + " legacy mask policy retained");
                Check(legacyPlan.After.Contains("ns: " + ns + ",") && legacyPlan.After.Contains("targetRelations: 4"), ns + " policy tool preserves type identity");
                Check(Analyze(Entry(ns, "UnknownMechanism")).Any(r => r.Status == "REVIEW"), ns + " unknown type rejected");
                Check(Analyze(Entry(ns).Replace("      type:", "     type:")).Any(r => r.Status == "REVIEW"), ns + " unsupported layout rejected");
            }
            var mixed = Analyze(Entry(legacy) + Entry(current, rid: 2));
            Check(mixed.Count == 2 && mixed.All(r => r.Status == "UNCHANGED"), "mixed old and new YAML");
            Check(Analyze(Entry(current).Replace("Assembly-CSharp", "MissingAssembly")).Any(r => r.Status == "REVIEW"), "unknown assembly rejected");

            // Drawer와 같은 파생 타입 탐색/생성 경로를 GUI 이벤트 없이 확인합니다.
            var mechanisms = TypeCache.GetTypesDerivedFrom<INewMechanism>().Where(t => !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters && t.Namespace == current).ToArray();
            Check(mechanisms.Length == 13 && mechanisms.Select(t => t.FullName).Distinct().Count() == 13, "mechanism candidates unique");
            foreach (var type in mechanisms)
            {
                var instance = TargetMaskEditorDefaults.Create(type);
                Check(instance != null && instance.GetType() == type && type.IsSerializable, "drawer creation " + type.Name);
            }
            foreach (var type in new[] { typeof(CircleArea), typeof(BoxArea), typeof(LaserArea) })
                Check(type.IsSerializable && Activator.CreateInstance(type) is IAreaShapes, "serializable shape " + type.Name);
            Check(typeof(SwitchMechanism).IsDefined(typeof(SerializableAttribute), false), "Switch owns Serializable attribute");

            File.WriteAllLines("namespace-migration-results.txt", Results);
            Debug.Log("NamespaceMigrationChecks PASS " + Results.Count);
        }
    }
}
