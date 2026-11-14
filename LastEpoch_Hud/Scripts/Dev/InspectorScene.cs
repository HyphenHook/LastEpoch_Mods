using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastEpoch_Hud.Scripts.Dev;

/// <summary>Read-only scene / GameObject browsing for the dev inspector. Main thread only.</summary>
internal static class InspectorScene
{
    private const int MaxChars = 24000;

    public static string Scenes()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            sb.AppendLine(s.name + " loaded=" + s.isLoaded + " roots=" + s.rootCount);
        }
        sb.AppendLine("(plus DontDestroyOnLoad objects; see /tree)");
        return sb.ToString();
    }

    private static IEnumerable<Transform> Roots()
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!t.IsNullOrDestroyed() && t.parent == null && t.gameObject.scene.IsValid())
            {
                yield return t;
            }
        }
    }

    public static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (Transform c = t; c != null; c = c.parent)
        {
            parts.Add(c.name);
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    /// <summary>Resolves "Root/Child/Grandchild" (names, first match) to a GameObject.</summary>
    public static GameObject Resolve(string path)
    {
        string[] parts = (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }
        foreach (Transform root in Roots())
        {
            if (root.name != parts[0])
            {
                continue;
            }
            Transform cur = root;
            foreach (string part in parts.Skip(1))
            {
                cur = cur.Find(part);
                if (cur == null)
                {
                    break;
                }
            }
            if (cur != null)
            {
                return cur.gameObject;
            }
        }
        return null;
    }

    public static string Tree(string path, int depth)
    {
        var sb = new StringBuilder();
        if (string.IsNullOrEmpty(path))
        {
            foreach (Transform r in Roots().OrderBy(r => r.name))
            {
                Line(sb, r, 0, depth);
            }
            return sb.ToString();
        }
        GameObject go = Resolve(path);
        if (go.IsNullOrDestroyed())
        {
            return "not found: " + path;
        }
        Line(sb, go.transform, 0, depth);
        return sb.ToString();
    }

    private static void Line(StringBuilder sb, Transform t, int indent, int depth)
    {
        if (sb.Length > MaxChars)
        {
            return;
        }
        string comps = string.Join(
            ",",
            t.gameObject.GetComponents<Component>()
                .Where(c => !c.IsNullOrDestroyed() && !(c is Transform))
                .Select(c => c.GetIl2CppType().Name)
        );
        sb.AppendLine(
            new string(' ', indent * 2)
                + t.name
                + (t.gameObject.activeSelf ? "" : " (inactive)")
                + (comps.Length > 0 ? "  [" + comps + "]" : "")
                + (depth <= 0 && t.childCount > 0 ? "  +" + t.childCount : "")
        );
        if (depth <= 0)
        {
            return;
        }
        for (int i = 0; i < t.childCount; i++)
        {
            Line(sb, t.GetChild(i), indent + 1, depth - 1);
        }
    }

    public static string Find(string typeName, string nameRegex)
    {
        Type type = ObjectDumper.FindInteropType(typeName);
        if (type == null)
        {
            return "no interop type matching '" + typeName + "'";
        }
        var rx = new Regex(nameRegex ?? ".", RegexOptions.IgnoreCase);
        var sb = new StringBuilder();
        int n = 0;
        foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(Il2CppType.From(type)))
        {
            if (o.IsNullOrDestroyed() || !rx.IsMatch(o.name))
            {
                continue;
            }
            Component c = o.TryCast<Component>();
            GameObject go = c != null ? c.gameObject : o.TryCast<GameObject>();
            bool live = go != null && go.scene.IsValid();
            sb.AppendLine(
                (live ? PathOf(go.transform) : "<asset> " + o.name) + "  (" + o.GetIl2CppType().Name + ")"
            );
            if (++n >= 100)
            {
                sb.AppendLine("... (capped at 100)");
                break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Finds a component on the GameObject at path by (short) type name and casts to its interop type.</summary>
    public static object Component(string goPath, string componentName)
    {
        GameObject go = Resolve(goPath);
        if (go.IsNullOrDestroyed())
        {
            return null;
        }
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c.IsNullOrDestroyed())
            {
                continue;
            }
            Il2CppSystem.Type it = c.GetIl2CppType();
            if (!string.Equals(it.Name, componentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            Type real = ObjectDumper.FindInteropType(it.FullName);
            if (real == null)
            {
                return c;
            }
            MethodInfo cast = typeof(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)
                .GetMethod("TryCast")
                .MakeGenericMethod(real);
            return cast.Invoke(c, null) ?? c;
        }
        return null;
    }

    public static string LogTail(int lines)
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "MelonLoader", "Logs");
        if (!Directory.Exists(dir))
        {
            return "no log dir";
        }
        string file = new DirectoryInfo(dir)
            .GetFiles("*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => f.FullName)
            .FirstOrDefault();
        if (file == null)
        {
            return "no log file";
        }
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        var q = new Queue<string>();
        string line;
        while ((line = sr.ReadLine()) != null)
        {
            q.Enqueue(line);
            if (q.Count > lines)
            {
                q.Dequeue();
            }
        }
        return string.Join("\n", q);
    }
}
