using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DependencyGraphExporter
{
    [MenuItem("Tools/Export/Asset Dependency Graph (DOT)")]
    public static void Export()
    {
        var allAssetPaths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !p.EndsWith(".meta")).ToArray();

        var sw = new StringWriter();
        sw.WriteLine("digraph G { rankdir=LR; node [shape=box];");

        foreach (var path in allAssetPaths)
        {
            var deps = AssetDatabase.GetDependencies(path, true)
                .Where(d => d != path && d.StartsWith("Assets/"));
            foreach (var d in deps)
                sw.WriteLine($"\"{Escape(path)}\" -> \"{Escape(d)}\";");
        }

        sw.WriteLine("}");
        var outPath = "Assets/DependencyGraph.dot";
        File.WriteAllText(outPath, sw.ToString());
        Debug.Log($"Exported: {outPath}");
        AssetDatabase.Refresh();
    }

    static string Escape(string s) => s.Replace("\\", "/").Replace("\"", "\\\"");
}