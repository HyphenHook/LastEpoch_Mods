using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using LastEpoch_Hud.Scripts.Core.QualityOfLife;
using LastEpoch_Hud.Scripts.ModUI;
using LastEpoch_Hud.Scripts.ModUI.Pages;
using LastEpoch_Hud.Scripts.ModUI.Settings;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.UI;

internal static class SessionGainCounters
{
    public static SessionGains Session { get; } = new();

    [ThreadStatic]
    static int xpDepth;

    [ThreadStatic]
    static int favourDepth;

    [ThreadStatic]
    static int amberDepth;

    [ThreadStatic]
    static int manualGrants;
    static string display = "";
    static float nextDisplay;
    static GUIStyle style;
    static readonly GainBalances balances = new(Session);
    static long factionOwner;
    static bool positionLoaded;
    static bool dragging;
    static Vector2 position;
    static Vector2 dragOffset;
    const string PositionX = "LastEpoch_Hud.SessionCounter.X";
    const string PositionY = "LastEpoch_Hud.SessionCounter.Y";
    static readonly FactionID[] trackedFactions =
    {
        FactionID.CircleOfFortune,
        FactionID.MerchantsGuild,
        FactionID.TheWeaver,
    };

    public static bool Active =>
        !SaveManager.instance.IsNullOrDestroyed()
        && SaveManager.instance.initialized
        && Scenes.IsGameScene()
        && !Refs_Manager.player_actor.IsNullOrDestroyed()
        && Refs_Manager.player_actor.gameObject.activeInHierarchy
        && !Session.Paused
        && Time.timeScale > 0;

    public static void Tick()
    {
        try
        {
            if (
                Scenes.IsCharacterSelection()
                || Scenes.SceneName == "Login"
                || Scenes.SceneName == "ClientSplash"
            )
                Reset();
            // A long loading frame is not active play time.
            float delta = Time.unscaledDeltaTime;
            SampleFactionsSafely(Active && manualGrants == 0 && delta <= 1);
            MoveOverlay();
            Session.Advance(delta, Active && delta <= 1);
            if (Time.unscaledTime >= nextDisplay)
            {
                nextDisplay = Time.unscaledTime + .25f;
                display = Format();
                WorldMiscPage.RefreshSession();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Session gain counters");
        }
    }

    public static string Format()
    {
        string state = LocaleRegistry.Translate(Session.Paused ? "Paused" : "Session");
        long seconds = (long)Math.Min(Session.ActiveSeconds, long.MaxValue);
        return state
            + " "
            + (seconds / 3600).ToString("00")
            + ":"
            + ((seconds / 60) % 60).ToString("00")
            + ":"
            + (seconds % 60).ToString("00")
            + "\n"
            + Line("XP", GainCurrency.Experience)
            + "\n"
            + Line("Favour", GainCurrency.Favour)
            + "\n"
            + Line("Memory Amber", GainCurrency.MemoryAmber);
    }

    static string Line(string label, GainCurrency currency) =>
        LocaleRegistry.Translate(label)
        + ": "
        + Session.Total(currency).ToString("N0")
        + "  |  "
        + Session.PerHour(currency).ToString("N0")
        + "/h";

    public static void Draw()
    {
        if (
            !ModSettings.SessionStats.ShowOverlay.Value
            || !Scenes.IsGameScene()
            || Refs_Manager.player_actor.IsNullOrDestroyed()
        )
            return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.richText = false;
            style.alignment = TextAnchor.UpperRight;
        }
        LoadPosition();
        ClampPosition();
        var rect = new Rect(position.x, position.y, 350, 92);
        var previous = GUI.color;
        try
        {
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), display, style);
            GUI.color = new Color(.93f, .84f, .65f);
            GUI.Label(rect, display, style);
        }
        finally
        {
            GUI.color = previous;
        }
    }

    static void LoadPosition()
    {
        if (positionLoaded)
            return;
        positionLoaded = true;
        position = new Vector2(
            PlayerPrefs.GetFloat(PositionX, 1f) * Mathf.Max(0, Screen.width - 350),
            PlayerPrefs.GetFloat(PositionY, 90f / Mathf.Max(1, Screen.height - 92))
                * Mathf.Max(0, Screen.height - 92)
        );
        ClampPosition();
    }

    static void ClampPosition()
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
            position = new Vector2(Mathf.Max(0, Screen.width - 370), 90);
        position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, Screen.width - 350));
        position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, Screen.height - 92));
    }

    static void SavePosition()
    {
        ClampPosition();
        PlayerPrefs.SetFloat(PositionX, position.x / Mathf.Max(1, Screen.width - 350));
        PlayerPrefs.SetFloat(PositionY, position.y / Mathf.Max(1, Screen.height - 92));
        PlayerPrefs.Save();
    }

    public static void ResetPosition()
    {
        positionLoaded = true;
        dragging = false;
        position = new Vector2(Mathf.Max(0, Screen.width - 370), 90);
        SavePosition();
    }

    static void MoveOverlay()
    {
        LoadPosition();
        ClampPosition();
        bool visible =
            ModSettings.SessionStats.ShowOverlay.Value
            && Scenes.IsGameScene()
            && !Refs_Manager.player_actor.IsNullOrDestroyed()
            && UnityEngine.Application.isFocused;
        if (dragging && (!visible || !Input.GetMouseButton(0)))
        {
            dragging = false;
            SavePosition();
        }
        if (!visible)
            return;
        var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        // A modifier drag reads input without adding a canvas/raycast target over item tooltips.
        if (
            !dragging
            && Input.GetMouseButtonDown(0)
            && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            && new Rect(position.x, position.y, 350, 92).Contains(mouse)
        )
        {
            dragging = true;
            dragOffset = mouse - position;
        }
        if (dragging)
        {
            position = mouse - dragOffset;
            ClampPosition();
        }
    }

    public static void Reset()
    {
        Session.Reset();
        balances.Clear();
        factionOwner = 0;
        SampleFactionsSafely(false);
    }

    public static void SetPaused(bool paused)
    {
        SampleFactionsSafely(Active && manualGrants == 0);
        Session.Paused = paused;
        SampleFactionsSafely(false);
    }

    static FactionTracker LocalFactions()
    {
        var actor = Refs_Manager.player_actor;
        long owner = actor.IsNullOrDestroyed() ? 0 : actor.Pointer.ToInt64();
        if (owner != factionOwner)
        {
            balances.Clear();
            factionOwner = owner;
        }
        return owner == 0 ? null : actor.gameObject.GetComponent<FactionTracker>();
    }

    static void SampleFactionsSafely(bool record)
    {
        try
        {
            SampleFactions(record);
        }
        catch (Exception ex)
        {
            balances.Clear();
            ErrorLog.Report(ex, "Session faction balance observation");
        }
    }

    static void SampleFactions(bool record)
    {
        var tracker = LocalFactions();
        if (tracker.IsNullOrDestroyed() || tracker.factions == null)
        {
            balances.Clear();
            return;
        }
        foreach (var id in trackedFactions)
        {
            if (
                tracker.factions.TryGetValue(id, out Faction local)
                && !local.IsNullOrDestroyed()
                && local.IsMember
            )
                balances.Observe(
                    (int)id,
                    local.Pointer.ToInt64(),
                    id == FactionID.TheWeaver ? GainCurrency.MemoryAmber : GainCurrency.Favour,
                    local.Favor,
                    record
                );
            else
                balances.Forget((int)id);
        }
    }

    public static IDisposable SuppressManualGrants() => new ManualGrant();

    sealed class ManualGrant : IDisposable
    {
        bool disposed;

        public ManualGrant()
        {
            SampleFactionsSafely(Active && manualGrants == 0);
            manualGrants++;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            try
            {
                SampleFactionsSafely(false);
            }
            finally
            {
                manualGrants--;
            }
        }
    }

    public sealed class Observation
    {
        public GainCurrency Currency;
        public long Before;
        public bool Outer;
        public bool Complete;
        public int Slot;
        public long Owner;
    }

    static Observation BeginXp(ExperienceTracker tracker)
    {
        if (
            !Active
            || manualGrants != 0
            || tracker.IsNullOrDestroyed()
            || Refs_Manager.exp_tracker.IsNullOrDestroyed()
            || tracker.Pointer != Refs_Manager.exp_tracker.Pointer
        )
            return null;
        var state = new Observation
        {
            Currency = GainCurrency.Experience,
            Before = tracker.CurrentExperience,
            Outer = xpDepth == 0,
        };
        xpDepth++;
        return state;
    }

    static bool CurrencyOf(Faction faction, out GainCurrency currency, out int slot)
    {
        currency = GainCurrency.Favour;
        slot = 0;
        var tracker = LocalFactions();
        if (tracker.IsNullOrDestroyed() || tracker.factions == null)
            return false;
        foreach (var id in trackedFactions)
            if (
                tracker.factions.TryGetValue(id, out Faction local)
                && !local.IsNullOrDestroyed()
                && local.IsMember
                && local.Pointer == faction.Pointer
            )
            {
                slot = (int)id;
                currency =
                    id == FactionID.TheWeaver ? GainCurrency.MemoryAmber : GainCurrency.Favour;
                return true;
            }
        return false;
    }

    static Observation BeginFavour(Faction faction)
    {
        if (
            !Active
            || manualGrants != 0
            || faction.IsNullOrDestroyed()
            || !CurrencyOf(faction, out var currency, out var slot)
        )
            return null;
        bool outer = currency == GainCurrency.MemoryAmber ? amberDepth == 0 : favourDepth == 0;
        var state = new Observation
        {
            Currency = currency,
            Before = faction.Favor,
            Outer = outer,
            Slot = slot,
            Owner = faction.Pointer.ToInt64(),
        };
        if (outer)
            balances.Observe(slot, state.Owner, currency, state.Before, true);
        if (currency == GainCurrency.MemoryAmber)
            amberDepth++;
        else
            favourDepth++;
        return state;
    }

    static void End(Observation state, long after, bool record)
    {
        if (state == null || state.Complete)
            return;
        state.Complete = true;
        if (state.Currency == GainCurrency.Experience)
            xpDepth--;
        else if (state.Currency == GainCurrency.MemoryAmber)
            amberDepth--;
        else
            favourDepth--;
        if (!state.Outer)
            return;
        if (state.Currency == GainCurrency.Experience)
        {
            if (record)
                Session.RecordIncrease(state.Currency, state.Before, after);
        }
        else if (record)
            balances.Observe(
                state.Slot,
                state.Owner,
                state.Currency,
                after,
                Active && manualGrants == 0
            );
        else
            balances.Forget(state.Slot);
    }

    static void XpPrefix(ExperienceTracker tracker, out Observation state)
    {
        state = null;
        try
        {
            state = BeginXp(tracker);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Session XP observation");
        }
    }

    static void XpPostfix(ExperienceTracker tracker, Observation state)
    {
        if (state == null)
            return;
        try
        {
            End(state, tracker.CurrentExperience, true);
        }
        catch (Exception ex)
        {
            ErrorLog.Report(ex, "Session XP total");
        }
        finally
        {
            End(state, 0, false);
        }
    }

    [HarmonyPatch(typeof(ExperienceTracker), "GainExp")]
    public class GainExpPatch
    {
        [HarmonyPrefix]
        static void Prefix(ExperienceTracker __instance, out Observation __state) =>
            XpPrefix(__instance, out __state);

        [HarmonyPostfix]
        static void Postfix(ExperienceTracker __instance, Observation __state) =>
            XpPostfix(__instance, __state);

        [HarmonyFinalizer]
        static void Finalizer(Observation __state) => End(__state, 0, false);
    }

    [HarmonyPatch(typeof(ExperienceTracker), "GainExpDirect")]
    public class GainExpDirectPatch
    {
        [HarmonyPrefix]
        static void Prefix(ExperienceTracker __instance, out Observation __state) =>
            XpPrefix(__instance, out __state);

        [HarmonyPostfix]
        static void Postfix(ExperienceTracker __instance, Observation __state) =>
            XpPostfix(__instance, __state);

        [HarmonyFinalizer]
        static void Finalizer(Observation __state) => End(__state, 0, false);
    }

    [HarmonyPatch(typeof(ExperienceTracker), "GainExpFromEnemyOrMote")]
    public class GainExpFromEnemyPatch
    {
        [HarmonyPrefix]
        static void Prefix(ExperienceTracker __instance, out Observation __state) =>
            XpPrefix(__instance, out __state);

        [HarmonyPostfix]
        static void Postfix(ExperienceTracker __instance, Observation __state) =>
            XpPostfix(__instance, __state);

        [HarmonyFinalizer]
        static void Finalizer(Observation __state) => End(__state, 0, false);
    }

    [HarmonyPatch(typeof(Faction), "GainFavor")]
    public class GainFavorPatch
    {
        [HarmonyPrefix]
        static void Prefix(Faction __instance, out Observation __state)
        {
            __state = null;
            try
            {
                __state = BeginFavour(__instance);
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Session Favour observation");
            }
        }

        [HarmonyPostfix]
        static void Postfix(Faction __instance, Observation __state)
        {
            if (__state == null)
                return;
            try
            {
                End(__state, __instance.Favor, true);
            }
            catch (Exception ex)
            {
                ErrorLog.Report(ex, "Session Favour total");
            }
            finally
            {
                End(__state, 0, false);
            }
        }

        [HarmonyFinalizer]
        static void Finalizer(Observation __state) => End(__state, 0, false);
    }
}
