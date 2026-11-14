using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace LastEpoch_Hud.Scripts.Dev;

/// <summary>Read-only reflection helpers for the dev inspector. Main thread only.</summary>
internal static class ObjectDumper
{
    private const int MaxItems = 25;
    private const int MaxTypes = 200;
    private const BindingFlags Instance =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public static string Dump(object root, string path, int depth)
    {
        object target = root;
        foreach (string part in (path ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            target = Member(target, part, out string error);
            if (error != null)
            {
                return error;
            }
        }
        var sb = new StringBuilder();
        Write(sb, target, depth, 0);
        return sb.ToString();
    }

    private static object Member(object target, string name, out string error)
    {
        error = null;
        if (target == null)
        {
            error = "null before '" + name + "'";
            return null;
        }
        IEnumerable seq = target as IEnumerable ?? Il2CppItems(target);
        if (int.TryParse(name, out int index) && seq != null)
        {
            int i = 0;
            foreach (object item in seq)
            {
                if (i++ == index)
                {
                    return item;
                }
            }
            error = "index out of range: " + name;
            return null;
        }
        Type staticType = target as Type;
        object self = staticType == null ? target : null;
        BindingFlags flags =
            (staticType == null ? BindingFlags.Instance : BindingFlags.Static)
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;
        for (Type t = staticType ?? target.GetType(); t != null; t = t.BaseType)
        {
            PropertyInfo p = t.GetProperty(name, flags);
            if (p != null && p.GetIndexParameters().Length == 0)
            {
                return Safe(() => p.GetValue(self), out error);
            }
            FieldInfo f = t.GetField(name, flags);
            if (f != null)
            {
                return Safe(() => f.GetValue(self), out error);
            }
        }
        error = "no member '" + name + "' on " + (target as Type ?? target.GetType()).FullName;
        return null;
    }

    /// <summary>Il2Cpp collections are not .NET IEnumerable; walk GetEnumerator/MoveNext/Current by reflection.</summary>
    private static IEnumerable Il2CppItems(object value)
    {
        if (value == null || IsSimple(value.GetType()))
        {
            return null;
        }
        MethodInfo get = value.GetType().GetMethod("GetEnumerator", Type.EmptyTypes);
        if (get == null)
        {
            return null;
        }
        var items = new List<object>();
        try
        {
            object e = get.Invoke(value, null);
            MethodInfo move = e.GetType().GetMethod("MoveNext", Type.EmptyTypes);
            PropertyInfo cur = e.GetType().GetProperty("Current");
            while (items.Count < MaxItems + 1 && (bool)move.Invoke(e, null))
            {
                items.Add(cur.GetValue(e));
            }
        }
        catch (Exception)
        {
            return null;
        }
        return items;
    }

    private static object Safe(Func<object> read, out string error)
    {
        error = null;
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            error = "read failed: " + (ex.InnerException ?? ex).Message;
            return null;
        }
    }

    private static void Write(StringBuilder sb, object value, int depth, int indent)
    {
        string pad = new string(' ', indent * 2);
        if (value == null)
        {
            sb.AppendLine(pad + "null");
            return;
        }
        Type type = value.GetType();
        if (IsSimple(type))
        {
            sb.AppendLine(pad + Format(value));
            return;
        }
        sb.AppendLine(pad + type.FullName + " " + Format(value));
        if (depth <= 0)
        {
            return;
        }
        IEnumerable seq = value as IEnumerable ?? Il2CppItems(value);
        if (seq != null)
        {
            int n = 0;
            foreach (object item in seq)
            {
                if (n++ >= MaxItems)
                {
                    sb.AppendLine(pad + "  ... (truncated)");
                    break;
                }
                sb.AppendLine(pad + "  [" + (n - 1) + "]");
                Write(sb, item, depth - 1, indent + 2);
            }
            return;
        }
        foreach (PropertyInfo p in Members(type).OfType<PropertyInfo>())
        {
            sb.Append(pad + "  " + p.Name + " = ");
            object v = Safe(() => p.GetValue(value), out string error);
            if (error != null)
            {
                sb.AppendLine("<" + error + ">");
            }
            else if (v == null || IsSimple(v.GetType()))
            {
                sb.AppendLine(Format(v));
            }
            else
            {
                sb.AppendLine();
                Write(sb, v, depth - 1, indent + 2);
            }
        }
    }

    private static IEnumerable<MemberInfo> Members(Type type)
    {
        for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            if (t.Namespace != null && t.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal))
            {
                yield break;
            }
            foreach (PropertyInfo p in t.GetProperties(Instance))
            {
                if (p.GetIndexParameters().Length == 0 && p.CanRead && !IsNoise(p.Name))
                {
                    yield return p;
                }
            }
        }
    }

    private static bool IsNoise(string name) =>
        name == "Pointer"
        || name == "ObjectClass"
        || name == "WasCollected"
        || name.StartsWith("NativeFieldInfoPtr", StringComparison.Ordinal);

    private static bool IsSimple(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);

    private static string Format(object v)
    {
        try
        {
            return v?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            return "<ToString failed: " + ex.Message + ">";
        }
    }

    /// <summary>Resolves a type by full name, Il2Cpp-prefixed name, or unique short name.</summary>
    public static Type FindInteropType(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }
        string[] candidates = { name, "Il2Cpp" + name, "Il2Cpp." + name };
        Type shortMatch = null;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            string an = asm.GetName().Name;
            if (!an.StartsWith("Il2Cpp", StringComparison.Ordinal) && !an.StartsWith("UnityEngine", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (string c in candidates)
            {
                Type t = asm.GetType(c, false);
                if (t != null)
                {
                    return t;
                }
            }
            if (shortMatch == null && !name.Contains('.'))
            {
                shortMatch = SafeTypes(asm).FirstOrDefault(t => t.Name == name);
            }
        }
        return shortMatch;
    }

    private static Type[] SafeTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).ToArray();
        }
    }

    /// <summary>Finds types and members whose names match the regex.</summary>
    public static string Search(string pattern)
    {
        var rx = new Regex(pattern, RegexOptions.IgnoreCase);
        var sb = new StringBuilder();
        int count = 0;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.GetName().Name.StartsWith("Il2Cpp", StringComparison.Ordinal))
            {
                continue;
            }
            foreach (Type t in SafeTypes(asm))
            {
                foreach (
                    MemberInfo m in t.GetMembers(
                        BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Instance
                            | BindingFlags.Static
                            | BindingFlags.DeclaredOnly
                    )
                )
                {
                    if (
                        m.MemberType == MemberTypes.NestedType
                        || m.Name.StartsWith("NativeMethodInfoPtr", StringComparison.Ordinal)
                        || m.Name.StartsWith("NativeFieldInfoPtr", StringComparison.Ordinal)
                        || !rx.IsMatch(m.Name)
                    )
                    {
                        continue;
                    }
                    sb.AppendLine(t.FullName + "::" + m.Name + " (" + m.MemberType + ")");
                    if (++count >= 300)
                    {
                        sb.AppendLine("... (capped at 300)");
                        return sb.ToString();
                    }
                }
            }
        }
        return sb.ToString();
    }

    public static string TypeName(Type t)
    {
        if (t == null)
        {
            return "?";
        }
        if (t.IsByRef)
        {
            return "ref " + TypeName(t.GetElementType());
        }
        if (t.IsArray)
        {
            return TypeName(t.GetElementType()) + "[]";
        }
        if (!t.IsGenericType)
        {
            return t.Name;
        }
        string n = t.Name;
        int tick = n.IndexOf('`');
        if (tick > 0)
        {
            n = n.Substring(0, tick);
        }
        return n + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName)) + ">";
    }

    /// <summary>Lists interop types whose full name matches the regex; optionally their members.</summary>
    public static string Types(string pattern, bool members)
    {
        var rx = new Regex(pattern, RegexOptions.IgnoreCase);
        var sb = new StringBuilder();
        int count = 0;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.GetName().Name.StartsWith("Il2Cpp", StringComparison.Ordinal))
            {
                continue;
            }
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }
            foreach (Type t in types)
            {
                if (t.FullName == null || !rx.IsMatch(t.FullName))
                {
                    continue;
                }
                if (++count > MaxTypes)
                {
                    sb.AppendLine("... (more than " + MaxTypes + " matches, narrow the pattern)");
                    return sb.ToString();
                }
                sb.AppendLine(t.FullName + "  [" + asm.GetName().Name + "]");
                if (!members)
                {
                    continue;
                }
                if (t.IsEnum)
                {
                    foreach (string en in Enum.GetNames(t))
                    {
                        sb.AppendLine("    " + en + " = " + Convert.ToInt64(Enum.Parse(t, en)));
                    }
                    continue;
                }
                foreach (
                    MethodInfo m in t.GetMethods(
                        BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Instance
                            | BindingFlags.Static
                            | BindingFlags.DeclaredOnly
                    )
                )
                {
                    if (m.IsSpecialName || m.Name.StartsWith("NativeMethodInfoPtr", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    sb.AppendLine(
                        "    "
                            + (m.IsStatic ? "static " : "")
                            + TypeName(m.ReturnType)
                            + " "
                            + m.Name
                            + "("
                            + string.Join(", ", m.GetParameters().Select(p => TypeName(p.ParameterType) + " " + p.Name))
                            + ")"
                    );
                }
                foreach (PropertyInfo p in t.GetProperties(Instance | BindingFlags.Static))
                {
                    if (!IsNoise(p.Name))
                    {
                        sb.AppendLine("    prop " + TypeName(p.PropertyType) + " " + p.Name);
                    }
                }
            }
        }
        return sb.ToString();
    }
}
