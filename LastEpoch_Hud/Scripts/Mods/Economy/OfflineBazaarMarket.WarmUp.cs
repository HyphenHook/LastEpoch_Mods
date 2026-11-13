using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>
/// Warm-up. Once a character has loaded and the game's item data is ready, the game is held (time stopped, a
/// progress panel shown) while every stall gets a first shelf of listings, with much of each frame spent on it.
/// After that the game runs normally and the market keeps itself topped up in the background, one stall at a time.
/// </summary>
internal static partial class OfflineBazaarMarket
{
    private const int StallCount = 30; // 0..22 gear, 23 generic idols, 24..28 class idols, 29 altars
    // Passes over all the stalls, so a time limit still leaves every stall stocked rather than a few full ones.
    // The last pass takes each stall to half of its capacity.
    private static readonly int[] WarmPassFloors = { 150, 300, StockPerStall / 2 };
    private const int WarmBudgetMs = 1000; // of each frame: the game is held, so nearly all of it goes to stocking
    private const double WarmMaxSeconds = 180; // never hold the game longer than this
    private const double WarmStallSeconds = 4; // give up on one stall that will not fill
    private const double WarmReadySeconds = 4; // the game must have been ready this long

    private static bool _warmDone;
    private static bool _warming;
    private static double _warmReadySince = -1;
    private static double _warmStartedAt;
    private static double _warmStallStartedAt;
    private static int _warmStall;
    private static int _warmPass;
    private static bool _warmStallPrepared;
    private static float _warmSavedScale = 1f;
    private static int _warmListings;

    // Background rotation after the warm-up: one stall at a time, so every shelf keeps moving.
    private static int _bgStall;
    private static bool _bgPrepared;
    private const double ActiveStallSeconds = 60; // a stall you looked at this recently has the market's attention
    private static double _activeAt;
    private const int BackgroundOtherBudgetMs = 3;

    // While the Bazaar screen is open the rest of the game is held and the market gets most of each frame.
    private const bool PauseGameWhileBazaarOpen = false; // off: a stopped clock stops tooltips and hover
    private const int BazaarFocusBatch = 40;
    private const int BazaarFocusBudgetMs = 60;
    private const double BazaarHoldDelaySeconds = 1.5; // let the panel finish opening before the world stops
    private const double BazaarHoldMaxSeconds = 900; // safety: never hold the game longer than this
    private static bool _bazaarOpen;
    private static bool _bazaarHeld;
    private static double _bazaarOpenedAt;
    private static float _bazaarSavedScale = 1f;

    /// <summary>The Bazaar screen opened.</summary>
    public static void BazaarOpened()
    {
        _bazaarOpen = true;
        _bazaarOpenedAt = UnityEngine.Time.realtimeSinceStartupAsDouble;
        Crumb("bazaar screen: open");
    }

    /// <summary>The Bazaar screen closed: release everything.</summary>
    public static void BazaarClosed()
    {
        if (_bazaarOpen)
        {
            Crumb("bazaar screen: closed");
        }
        _bazaarOpen = false;
        ReleaseBazaarHold();
    }

    private static void ReleaseBazaarHold()
    {
        if (_bazaarHeld)
        {
            UnityEngine.Time.timeScale = _bazaarSavedScale <= 0f ? 1f : _bazaarSavedScale;
            _bazaarHeld = false;
        }
    }

    private static void BazaarFocus(double now)
    {
        if (!_bazaarOpen)
        {
            ReleaseBazaarHold();
            return;
        }
        bool gone = !Scenes.IsGameScene();
        try
        {
            gone |= BazaarPanelOpenOfflinePatch.Panel.IsNullOrDestroyed();
        }
        catch (Exception)
        {
            gone = true;
        }
        if (gone || (_bazaarHeld && now - _bazaarOpenedAt > BazaarHoldMaxSeconds))
        {
            Crumb("bazaar screen: hold released (" + (gone ? "panel gone" : "time limit") + ")");
            _bazaarOpen = false;
            ReleaseBazaarHold();
            return;
        }
        if (PauseGameWhileBazaarOpen && !_bazaarHeld && !_warming && now - _bazaarOpenedAt >= BazaarHoldDelaySeconds)
        {
            _bazaarSavedScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 0f;
            _bazaarHeld = true;
        }
    }

    /// <summary>Item types of the stalls whose shelf is not "item type equals stall number".</summary>
    private static void LearnDefaultStalls()
    {
        // What the game's own requests showed, then anything learned in earlier sessions.
        var known = new Dictionary<int, int[]>
        {
            [11] = new[] { 12 },
            [12] = new[] { 13 },
            [13] = new[] { 14 },
            [16] = new[] { 23 },
            [23] = new[] { 25, 26, 27, 28, 29, 30, 31, 32, 33 },
            [24] = new[] { 29, 30, 31, 32, 33 },
            [25] = new[] { 29, 30, 31, 32, 33 },
            [26] = new[] { 29, 30, 31, 32, 33 },
            [27] = new[] { 29, 30, 31, 32, 33 },
            [28] = new[] { 29, 30, 31, 32, 33 },
            [29] = new[] { 41 },
        };
        try
        {
            string path = StallTypesPath;
            if (File.Exists(path))
            {
                var saved = JsonConvert.DeserializeObject<Dictionary<int, int[]>>(File.ReadAllText(path));
                if (saved != null)
                {
                    foreach (var kv in saved)
                    {
                        known[kv.Key] = kv.Value;
                    }
                }
            }
        }
        catch (Exception) { }
        foreach (var kv in known)
        {
            if (!_itemTypesOfStall.ContainsKey(kv.Key))
            {
                _itemTypesOfStall[kv.Key] = kv.Value;
            }
        }
    }

    private static string StallTypesPath => Path.Combine(Folder, "stall_types.json");

    /// <summary>Remember which item types each stall holds, so the next start does not have to guess.</summary>
    private static void SaveStallTypes()
    {
        try
        {
            File.WriteAllText(StallTypesPath, JsonConvert.SerializeObject(_itemTypesOfStall));
        }
        catch (Exception) { }
    }

    private static bool GameIsReady()
    {
        try
        {
            return Scenes.IsGameScene()
                && !Scripts.Refs_Manager.player_actor.IsNullOrDestroyed()
                && FindGenerator() != null
                && !ItemList.get().IsNullOrDestroyed()
                && !AffixList.get().IsNullOrDestroyed();
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Returns true while the warm-up holds the game.</summary>
    private static bool WarmUp(double now)
    {
        if (_warmDone)
        {
            return false;
        }
        if (!_warming)
        {
            if (!GameIsReady())
            {
                _warmReadySince = -1;
                return false;
            }
            if (_warmReadySince < 0)
            {
                _warmReadySince = now;
            }
            if (now - _warmReadySince < WarmReadySeconds)
            {
                return false;
            }
            EnsureLoaded();
            LearnDefaultStalls();
            _warming = true;
            _warmStartedAt = now;
            _warmStall = 0;
            _warmPass = 0;
            _warmStallPrepared = false;
            _warmSavedScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 0f;
            BazaarItemFactory.AttemptScale = 0.5;
            Crumb("warm-up: started");
        }

        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < WarmBudgetMs && _warmPass < WarmPassFloors.Length)
            {
                if (_warmStall >= StallCount)
                {
                    _warmPass++;
                    _warmStall = 0;
                    _warmStallPrepared = false;
                    continue;
                }
                int floor = WarmPassFloors[_warmPass];
                int stall = _warmStall;
                if (!_warmStallPrepared)
                {
                    PrepareStall(stall);
                    _warmStallPrepared = true;
                    _warmStallStartedAt = now;
                }
                bool full = NpcCount(stall) >= floor || PendingArrivals(stall) < 1;
                bool stuck = now - _warmStallStartedAt > WarmStallSeconds;
                if (full || stuck)
                {
                    if (stuck && !full)
                    {
                        Crumb("warm-up: stall " + stall + " stopped at " + NpcCount(stall) + " listings");
                    }
                    _warmStall++;
                    _warmStallPrepared = false;
                    continue;
                }
                int remaining = (int)Math.Max(1, WarmBudgetMs - clock.ElapsedMilliseconds);
                int want = Math.Min(floor - NpcCount(stall), 600);
                int added = Fill(stall, want, budgetMs: remaining, log: false);
                _pendingArrivals[stall] = Math.Max(0, PendingArrivals(stall) - added);
                _warmListings += added;
            }
        }
        catch (Exception ex)
        {
            Crumb("warm-up failed: " + ex.Message);
            _warmPass = WarmPassFloors.Length;
        }

        if (_warmPass >= WarmPassFloors.Length || now - _warmStartedAt > WarmMaxSeconds)
        {
            UnityEngine.Time.timeScale = _warmSavedScale <= 0f ? 1f : _warmSavedScale;
            BazaarItemFactory.AttemptScale = 1.0;
            _warming = false;
            _warmDone = true;
            MarkDirty();
            SaveNow();
            Crumb("warm-up: finished in " + (int)(now - _warmStartedAt) + " s, " + _warmListings + " listings made, pass " + Math.Min(_warmPass + 1, WarmPassFloors.Length) + "/" + WarmPassFloors.Length);
        }
        return _warming;
    }

    /// <summary>
    /// Settles what sold while away and sets how many listings are due. A stall seen for the first time is due a
    /// full shelf.
    /// </summary>
    private static void PrepareStall(int stall)
    {
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!_stallSimUnix.ContainsKey(stall))
        {
            _stallSimUnix[stall] = nowUnix;
            _pendingArrivals[stall] = Math.Max(0, StockPerStall - Npc(stall).Count);
            MarkDirty();
        }
        else
        {
            CatchUp(stall, withArrivals: true, deferFill: true);
        }
    }

    /// <summary>The panel shown while the game is held.</summary>
    public static void DrawWarmUp()
    {
        if (!_warming)
        {
            return;
        }
        try
        {
            int w = UnityEngine.Screen.width;
            int h = UnityEngine.Screen.height;
            var previous = UnityEngine.GUI.color;
            UnityEngine.GUI.color = new UnityEngine.Color(0f, 0f, 0f, 0.82f);
            UnityEngine.GUI.DrawTexture(new UnityEngine.Rect(0, 0, w, h), UnityEngine.Texture2D.whiteTexture);
            UnityEngine.GUI.color = new UnityEngine.Color(0.93f, 0.84f, 0.65f);
            var style = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label)
            {
                alignment = UnityEngine.TextAnchor.MiddleCenter,
                fontSize = Math.Max(16, h / 40),
            };
            string text = "Stocking the Merchant's Guild...\n"
                + "pass " + Math.Min(_warmPass + 1, WarmPassFloors.Length) + " / " + WarmPassFloors.Length + "   stall "
                + Math.Min(_warmStall + 1, StallCount) + " / " + StallCount + "   -   " + _warmListings + " new listings";
            UnityEngine.GUI.Label(new UnityEngine.Rect(0, 0, w, h), text, style);
            UnityEngine.GUI.color = previous;
        }
        catch (Exception) { }
    }

    /// <summary>
    /// After the warm-up (and while you are not at a stall) the market keeps every shelf moving, one stall at a
    /// time, with a small slice of each frame.
    /// </summary>
    private static void BackgroundRotate(double now)
    {
        bool watching = _activeStall >= 0 && now - _activeAt < ActiveStallSeconds;
        if (watching || !_warmDone || _rolling || now < _nextBackgroundFillAt)
        {
            return;
        }
        if (!_bgPrepared)
        {
            PrepareStall(_bgStall);
            _bgPrepared = true;
        }
        if (PendingArrivals(_bgStall) >= 1 && NpcCount(_bgStall) < StockPerStall)
        {
            _nextBackgroundFillAt = now + BackgroundFillInterval;
            int added = Fill(_bgStall, (int)Math.Min(BackgroundBatch, PendingArrivals(_bgStall)), budgetMs: BackgroundOtherBudgetMs, log: false);
            _pendingArrivals[_bgStall] = Math.Max(0, PendingArrivals(_bgStall) - added);
            if (added > 0)
            {
                return;
            }
        }
        _bgStall = (_bgStall + 1) % StallCount;
        _bgPrepared = false;
    }
}
