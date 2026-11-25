using global::NoWebGame.Moves;
using global::NoWebGame.Entities.Relations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;


using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 버전 2 Unity YAML 관리 참조의 설정 필드만 추가합니다. 객체/관리 참조 ID를 재생성하지 않습니다.
    /// 예상 형식이 아니거나 의도가 모호하면 전체 적용을 중단합니다. dry-run 이후 파일 변경도 감지합니다.
    /// </summary>
    public static class TargetMaskMigration
    {
        public sealed class Row
        {
            public string Path, Reference, Type, Status, Reason;
            public int Legacy, Relations;
            public override string ToString() => $"{Status}\t{Path}\trid={Reference}\t{Type}\t{Legacy} -> {Relations}\t{Reason}";
        }
        public sealed class FilePlan
        {
            public string Path, Before, After, Hash;
        }
        public sealed class Plan
        {
            public readonly List<Row> Rows = new();
            public readonly List<FilePlan> Files = new();
            public int ScannedFiles;
            public bool HasReview => Rows.Any(r => r.Status == "REVIEW");
        }
        private static readonly Regex Entry = new(@"(?m)^    - rid: (?<rid>[^\r\n]+)\r?\n(?<body>(?:(?!^    - rid:|^--- !u!)[\s\S])*)");
        private static readonly Regex TypeLine = new(@"(?m)^      type: \{class: (?<class>[^,]*), ns: (?<ns>[^,]*), asm: (?<asm>[^} ]*)\}\r?$");
        private static string Absolute(string path) => System.IO.Path.GetFullPath(path);
        public static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        private static string Read(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path));
        private static int? Number(string block, string field)
        {
            var matches = Regex.Matches(block, @"(?m)^        " + field + @": (-?\d+)\r?$");
            return matches.Count == 1 && int.TryParse(matches[0].Groups[1].Value, out var n) ? n : null;
        }
        private static Match Mask(string block, string field) => Regex.Match(block,
            @"(?m)^        " + field + @":\r?\n          serializedVersion: 2\r?\n          m_Bits: (?<bits>\d+)\r?\n");

        public static FilePlan AnalyzeText(string path, string text, List<Row> rows)
        {
            var recognized = 0;
            var after = Entry.Replace(text, match =>
            {
                var block = match.Value;
                var type = TypeLine.Match(block);
                if (!type.Success) return block;
                var name = type.Groups["class"].Value;
                var ns = type.Groups["ns"].Value;
                if (string.IsNullOrEmpty(name)) return block; // 빈 관리 참조는 대체하지 않습니다.
                var resolved = NamespaceTypeMap.Resolve(ns, name, type.Groups["asm"].Value);
                var isMechanism = resolved != null && typeof(INewMechanism).IsAssignableFrom(resolved);
                if (!isMechanism && !NamespaceTypeMap.IsMechanismNamespace(ns)) return block;
                recognized++;
                var row = new Row { Path = path, Reference = match.Groups["rid"].Value, Type = name, Status = "REVIEW", Reason = "Unsupported or missing Mechanism type" };
                rows.Add(row);
                if (!isMechanism || !typeof(NewMechanism).IsAssignableFrom(resolved)) return block;
                var version = Number(block, "targetMaskVersion");
                var relations = Number(block, "targetRelations");
                if ((block.Contains("targetMaskVersion:") && !version.HasValue) || (block.Contains("targetRelations:") && !relations.HasValue))
                { row.Reason = "Malformed/duplicate version or relations"; return block; }
                if (version == 1)
                {
                    row.Relations = relations ?? -1;
                    row.Status = relations.HasValue && TargetRelationMasks.IsValid((TargetRelationMask)relations.Value) ? "UNCHANGED" : "REVIEW";
                    row.Reason = "Already relative; never reinterpret legacy fields";
                    return block;
                }
                if (version.HasValue && version != 0) { row.Reason = "Unknown schema version"; return block; }
                if (relations.HasValue && relations != 0) { row.Reason = "Unversioned relationship intent requires review"; return block; }
                var mask = Mask(block, "mask");
                if (!mask.Success || !int.TryParse(mask.Groups["bits"].Value, out var bits))
                { row.Reason = "Missing/unsupported legacy mask representation"; return block; }
                row.Legacy = bits;
                if (bits != 0 && bits != 128) { row.Reason = "Only approved 0 and Foe=128 mappings are automatic"; return block; }
                if (name == "MeleeMechanism")
                {
                    var enemy = Mask(block, "enemyMask");
                    if (!enemy.Success || enemy.Groups["bits"].Value != mask.Groups["bits"].Value)
                    { row.Reason = "Melee mask/enemyMask mismatch"; return block; }
                }
                row.Status = "CONVERT"; row.Relations = bits == 0 ? 0 : 4;
                row.Reason = bits == 0 ? "Preserve None" : "Approved relative Enemy intent";
                var newline = block.Contains("\r\n") ? "\r\n" : "\n";
                // 기존 필드 순서·줄바꿈·다른 값은 유지하고 새 필드만 추가/갱신합니다.
                var additions = "";
                if (!version.HasValue) additions += "        targetMaskVersion: 1" + newline;
                else block = Regex.Replace(block, @"(?m)^(        targetMaskVersion: )0(?=\r?$)", "${1}1");
                if (!relations.HasValue) additions += $"        targetRelations: {row.Relations}" + newline;
                else block = Regex.Replace(block, @"(?m)^(        targetRelations: )0(?=\r?$)", "${1}" + row.Relations);
                mask = Mask(block, "mask");
                return block.Insert(mask.Index + mask.Length, additions);
            });
            if (Regex.Matches(text, @"ns: (?:Moves\.Mechanisms|NoWebGame\.Combat\.Mechanisms)[,}]").Count > recognized)
                rows.Add(new Row { Path = path, Status = "REVIEW", Reason = "Unsupported managed-reference YAML layout" });
            return new FilePlan { Path = path, Before = text, After = after, Hash = Hash(text) };
        }

        public static Plan Scan()
        {
            var plan = new Plan();
            foreach (var path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                var ext = System.IO.Path.GetExtension(path);
                if (ext != ".asset" && ext != ".prefab" && ext != ".unity") continue;
                plan.ScannedFiles++;
                var text = Read(path);
                if (!text.TrimStart('\uFEFF').StartsWith("%YAML"))
                { plan.Rows.Add(new Row { Path = path, Status = "REVIEW", Reason = "Non-text Unity asset must be inspected separately" }); continue; }
                var file = AnalyzeText(path.Replace('\\', '/'), text, plan.Rows);
                if (file.Before != file.After) plan.Files.Add(file);
            }
            return plan;
        }
        public static void WriteReport(Plan plan, string path)
        {
            var lines = new List<string> { $"scanned={plan.ScannedFiles} changedFiles={plan.Files.Count} converted={plan.Rows.Count(r => r.Status == "CONVERT")} review={plan.Rows.Count(r => r.Status == "REVIEW")}" };
            lines.AddRange(plan.Rows.Select(r => r.ToString()));
            File.WriteAllLines(path, lines);
        }
        public static void Apply(Plan plan, string backupDirectory)
        {
            if (plan.HasReview) throw new InvalidOperationException("Manual review items exist. No files changed.");
            var assetsRoot = Absolute("Assets") + System.IO.Path.DirectorySeparatorChar;
            // 모든 파일을 먼저 검증합니다. 계획 이후의 사용자 수정은 덮어쓰지 않습니다.
            foreach (var file in plan.Files)
            {
                if (!Absolute(file.Path).StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase) || Hash(Read(file.Path)) != file.Hash)
                    throw new InvalidOperationException("File changed since dry-run or path outside Assets: " + file.Path);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(file.Path))
                    if (asset && EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Unsaved asset edits: " + file.Path);
            }
            foreach (var file in plan.Files)
            {
                var backup = System.IO.Path.Combine(backupDirectory, file.Path);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup));
                if (File.Exists(backup)) throw new IOException("Backup already exists; use a fresh directory: " + backup);
                File.Copy(file.Path, backup);
            }
            var written = new List<FilePlan>();
            try
            {
                foreach (var file in plan.Files)
                { File.WriteAllText(file.Path, file.After, new UTF8Encoding(false)); written.Add(file); }
            }
            catch
            {
                foreach (var file in written) File.WriteAllText(file.Path, file.Before, new UTF8Encoding(false));
                throw;
            }
            foreach (var file in plan.Files) AssetDatabase.ImportAsset(file.Path, ImportAssetOptions.ForceUpdate);
        }
        [MenuItem("Tools/Target Masks/Preview Migration")]
        public static void Preview()
        {
            var plan = Scan();
            var report = System.IO.Path.GetFullPath(Application.dataPath + "/../../target-stage3-dry-run.txt");
            WriteReport(plan, report);
            Debug.Log($"Target Mask migration preview: {plan.Files.Count} files, review={plan.HasReview}. {report}");
        }
        [MenuItem("Tools/Target Masks/Apply Reviewed Migration")]
        public static void ApplyReviewed()
        {
            var plan = Scan();
            var report = System.IO.Path.GetFullPath(Application.dataPath + "/../../target-stage3-dry-run.txt");
            WriteReport(plan, report);
            if (plan.HasReview) throw new InvalidOperationException("Review report first: " + report);
            if (plan.Files.Count == 0) { Debug.Log("No pending Target Mask migration."); return; }
            if (!EditorUtility.DisplayDialog("Target Mask Migration", $"{plan.Files.Count} saved files will change. 128→Enemy; 0→None. Read {report} before applying.", "Apply", "Cancel")) return;
            Apply(plan, System.IO.Path.GetFullPath(Application.dataPath + "/../../target-mask-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")));
        }
    }
}
