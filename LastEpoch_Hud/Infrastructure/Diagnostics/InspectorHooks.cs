using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace LastEpoch_Hud.Scripts.Dev;

/// <summary>
/// Passive call tracing for the dev inspector. Hooks only record; they never change a result.
/// Add targets to <see cref="Targets" /> to trace more of the game.
/// </summary>
internal static class InspectorHooks
{
    private const int MaxTrace = 600;

    // "Full.Type.Name::Method" (all overloads). Skill effect cosmetics are the current focus.
    private static readonly string[] Targets =
    {
        "Il2CppLE.Networking.Cosmetics.ClientCosmeticsNetworkManager::OfflineLocalPlayerInitHandler",
        "Il2CppLE.Networking.Cosmetics.ClientCosmeticsNetworkManager::ApplyManualCache",
        "Il2CppLE.Networking.Cosmetics.ClientCosmeticsNetworkManager::ManuallyCachePlayerEquippedCosmetics",
        "Il2CppLE.Networking.Cosmetics.ClientCosmeticsNetworkManager::AddToAbilityEquips",
        "Il2CppLE.Networking.Cosmetics.ClientCosmeticsNetworkManager::AddAdditionalAbilities",
        "Il2CppLE.Networking.Cosmetics.ClientCosmeticsNetworkManager::RemoveFromAbilityEquips",
        "Il2CppLE.Networking.Cosmetics.ClientCosmeticsNetworkManager::SaveLocalEquips",
        "Il2CppLE.Networking.Cosmetics.CharacterEquippedCosmetics::FilterItemsNotOwned",
        "Il2CppLE.Networking.Cosmetics.CharacterEquippedCosmetics::TryEquipItem",
        "Il2CppLE.Networking.Cosmetics.CharacterEquippedCosmetics::GetEquipMtxAbilities",
        "Il2CppLE.Networking.Cosmetics.CharacterEquippedCosmetics::AddToAbilityEquips",
        "Il2CppLE.Services.Cosmetics.CosmeticResources::GetAbilityCosmetic",
        // Merchant's Guild stalls (offline gate lives somewhere under NpcStallTooltip.Interaction)
        "Il2Cpp.NpcStallTooltip::Interaction",
        "Il2CppLE.UI.PanelSystem.BazaarPanel::OnOpenWithArgs",
        "Il2CppLE.UI.Bazaar.BazaarUI::SwitchUIState",
        "Il2CppLE.UI.Bazaar.BazaarUI::OnPanelClose",
        "Il2Cpp.ErrorInfoHandler::ShowError",
        "Il2Cpp.OverChatNotification::ShowNotification",
        "Il2Cpp.ControllerNotification::ShowNotification",
        // NOT hooked by default: TranslateAbilityKey / GetAbilityVisual / GetAbilityActorVisual.
        // Hooking them crashed the game (access violation in coreclr) during player spawn.
    };

    private static readonly Queue<string> _trace = new();
    private static readonly ConcurrentDictionary<string, object> _instances = new();
    private static readonly ConcurrentDictionary<string, int> _counts = new();

    public static string Report { get; private set; } = "not installed";

    private static HarmonyLib.Harmony _harmony;

    /// <summary>Hooks "Full.Type.Name::Method" at runtime (record-only).</summary>
    public static string AddTarget(string target)
    {
        if (_harmony == null)
        {
            return "hooks not installed";
        }
        string before = Report;
        Install(_harmony, new[] { target });
        string result = Report;
        Report = before + result;
        return result;
    }

    /// <summary>Removes the recording hooks from "Full.Type.Name::Method".</summary>
    public static string RemoveTarget(string target)
    {
        if (_harmony == null)
        {
            return "hooks not installed";
        }
        string[] parts = target.Split(new[] { "::" }, StringSplitOptions.None);
        Type type = parts.Length == 2 ? FindType(parts[0]) : null;
        if (type == null)
        {
            return "unknown target";
        }
        int n = 0;
        foreach (
            MethodInfo m in type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
            ).Where(m => m.Name == parts[1])
        )
        {
            _harmony.Unpatch(m, HarmonyPatchType.All, _harmony.Id);
            n++;
        }
        return "unhooked " + n + " method(s) of " + target;
    }

    public static void Install(HarmonyLib.Harmony harmony) => Install(harmony, Targets);

    private static void Install(HarmonyLib.Harmony harmony, string[] targets)
    {
        _harmony = harmony;
        var sb = new StringBuilder();
        var pre = new HarmonyMethod(typeof(InspectorHooks), nameof(Record));
        var post = new HarmonyMethod(typeof(InspectorHooks), nameof(RecordVoid));
        var postResult = new HarmonyMethod(typeof(InspectorHooks), nameof(RecordResult));
        foreach (string target in targets)
        {
            string[] parts = target.Split(new[] { "::" }, StringSplitOptions.None);
            Type type = FindType(parts[0]);
            if (type == null)
            {
                sb.AppendLine("MISSING TYPE " + parts[0]);
                continue;
            }
            MethodInfo[] methods = type.GetMethods(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                )
                .Where(m => m.Name == parts[1])
                .ToArray();
            if (methods.Length == 0)
            {
                sb.AppendLine("MISSING METHOD " + target);
            }
            foreach (MethodInfo m in methods)
            {
                // Capturing args of ref/out methods corrupts their results (broke ability casting).
                if (m.GetParameters().Any(p => !IsSafeParam(p.ParameterType)))
                {
                    sb.AppendLine("SKIPPED (ref/out or struct params are unsafe to hook) " + Describe(m));
                    continue;
                }
                try
                {
                    harmony.Patch(m, prefix: pre, postfix: m.ReturnType == typeof(void) ? post : postResult);
                    sb.AppendLine("hooked " + Describe(m));
                }
                catch (Exception ex)
                {
                    sb.AppendLine("FAILED " + Describe(m) + ": " + ex.Message);
                }
            }
        }
        Report = sb.ToString();
    }

    // ref/out and non-primitive structs (Nullable<T>, CancellationToken, Vector3...) break the trampoline.
    private static bool IsSafeParam(Type t) =>
        !t.IsByRef && (!t.IsValueType || t.IsPrimitive || t.IsEnum);

    private static Type FindType(string fullName)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!asm.GetName().Name.StartsWith("Il2Cpp", StringComparison.Ordinal))
            {
                continue;
            }
            Type t = asm.GetType(fullName, false);
            if (t != null)
            {
                return t;
            }
        }
        return null;
    }

    private static string Describe(MethodBase m) =>
        m.DeclaringType?.Name + "." + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)) + ")";

    public static string Trace(int count)
    {
        string[] all;
        lock (_trace)
        {
            all = _trace.ToArray();
        }
        return string.Join("\n", all.Skip(Math.Max(0, all.Length - count)));
    }

    public static string Counts() =>
        string.Join("\n", _counts.OrderBy(k => k.Key).Select(k => k.Value + "  " + k.Key));

    public static bool TryGetInstance(string shortTypeName, out object instance) =>
        _instances.TryGetValue(shortTypeName, out instance);

    public static IEnumerable<string> InstanceNames => _instances.Keys;

    private static void Add(string line)
    {
        lock (_trace)
        {
            _trace.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff") + " " + line);
            while (_trace.Count > MaxTrace)
            {
                _trace.Dequeue();
            }
        }
    }

    private static string Args(object[] args)
    {
        try
        {
            return string.Join(", ", args.Select(a => a?.ToString() ?? "null"));
        }
        catch (Exception ex)
        {
            return "<args unreadable: " + ex.Message + ">";
        }
    }

    private static void Remember(object instance, MethodBase method)
    {
        string name = method.DeclaringType?.Name;
        if (instance != null && name != null)
        {
            _instances[name] = instance;
        }
        if (name != null)
        {
            lock (_counts)
            {
                string key = name + "." + method.Name;
                _counts[key] = (_counts.TryGetValue(key, out int c) ? c : 0) + 1;
            }
        }
    }

    // Harmony injects these by parameter name.
    // Per-frame methods would flood the trace: log the first 50 calls, then 1 in 500.
    private static bool Sample(MethodBase m)
    {
        int c;
        lock (_counts)
        {
            _counts.TryGetValue(m.DeclaringType?.Name + "." + m.Name, out c);
        }
        return c <= 50 || c % 500 == 0;
    }

    private static void Record(object __instance, object[] __args, MethodBase __originalMethod)
    {
        try
        {
            Remember(__instance, __originalMethod);
            if (!Sample(__originalMethod))
            {
                return;
            }
            Add("> " + Describe(__originalMethod) + " args=[" + Args(__args) + "]");
        }
        catch (Exception) { /* tracing must never break the game */ }
    }

    private static void RecordVoid(MethodBase __originalMethod)
    {
        try
        {
            if (!Sample(__originalMethod))
            {
                return;
            }
            Add("< " + Describe(__originalMethod));
        }
        catch (Exception) { }
    }

    private static void RecordResult(object[] __args, object __result, MethodBase __originalMethod)
    {
        try
        {
            if (!Sample(__originalMethod))
            {
                return;
            }
            Add("< " + Describe(__originalMethod) + " result=" + (__result?.ToString() ?? "null") + " argsAfter=[" + Args(__args) + "]");
        }
        catch (Exception) { }
    }
}
