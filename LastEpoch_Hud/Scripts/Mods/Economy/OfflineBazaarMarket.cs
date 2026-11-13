using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Mods.Items.AutoLoot;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

internal sealed class AffixInfo
{
    public int Id;
    public int Roll;
    public int Tier; // internal tier; the displayed tier is Tier + 1
    public bool Sealed;
}

/// <summary>One listing in the local Bazaar. Item data is the game's own serialisation (ItemData.id).</summary>
internal sealed class OfflineListing
{
    public const int CurrentMeta = 17;

    public Guid Id = Guid.NewGuid();
    public Guid Version = Guid.NewGuid();
    public string ItemBase64;
    public long Price;
    public long FairValue;
    public int Stall = -1;
    public long ListedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public bool Mine;

    // Your listings: sold by a simulated buyer at SellAtUnix, then collected for gold (Redeemed).
    public long SellAtUnix;
    public bool Sold;
    public bool Redeemed;
    public long SoldUnix;

    // Cached item facts, used for filtering without rebuilding the item.
    public int Meta;
    public string Kind;
    public string Note;
    public bool Corrupted;
    public int ItemType;
    public int SubType;
    public int Rarity;
    public int BazaarRarity; // the stall filter's rarity: 0 normal 1 magic 2 rare 3 exalted 4 set 5 unique 6 legendary
    public int ForgingPotential;
    public int LevelReq;
    public int ClassReq;
    public int UniqueId;
    public int WeaversWill;
    public int LegendaryPotential;
    public bool PrimordialSealed;
    public List<AffixInfo> Affixes = new();

    /// <summary>The sale tax on this listing's price (zero while TaxRate is zero).</summary>
    [JsonIgnore]
    public long Tax => OfflineBazaarMarket.TaxRate <= 0 ? 0 : Math.Max(1, (long)Math.Ceiling(Price * OfflineBazaarMarket.TaxRate));

    [JsonIgnore]
    public byte[] ItemBytes => _bytes ??= Convert.FromBase64String(ItemBase64);

    [JsonIgnore]
    private byte[] _bytes;
}

internal sealed class BazaarSave
{
    public List<OfflineListing> Listings = new();
    public Dictionary<int, long> StallSimUnix = new();
}

/// <summary>
/// The offline Merchant's Guild economy. Each stall holds a limited shelf of NPC listings that turns over
/// like a real market: simulated buyers take listings (underpriced ones go first), and simulated sellers
/// list new items at prices scattered around what they are worth, including the odd lowball you can snipe.
/// Your own listings sell the same way.
/// </summary>
internal static partial class OfflineBazaarMarket
{
    // ---- Tuning. The shape matters more than the numbers; adjust in play.
    //
    // Population: the market behaves as if 400,000 players are active in it. Roughly 4% of them list something
    // each hour, spread over about 30 stalls, so each stall sees ~530 new listings an hour. Fairly priced,
    // affordable items are gone within ~8 minutes (so ~70 of them are on the shelf at any moment), while
    // the expensive ones sit for hours; together that fills a shelf of a couple of hundred listings.
    public const int ActivePlayers = 2_000_000;
    // Shelf and turnover were tuned for 400,000 players. More players means more sellers and more buyers, so
    // listings come and go faster and each shelf holds more of them.
    public const double PopulationScale = ActivePlayers / 400_000.0;
    public const int StockPerStall = 1200;
    public const double TaxRate = 0.0; // sales tax; set above zero to take a cut of what you collect
    private const double NpcTurnoverMinutes = 8 / PopulationScale; // a fairly priced, affordable NPC listing lasts about this long
    private const double YourListingBaseMinutes = 12; // fairly priced listings of yours sell about this fast
    private const double MaxYourListingMinutes = 180;
    private const double NeverSellRatio = 4.0;
    private const double GarbageListChance = 0.3; // sellers rarely bother listing junk
    private const long MaxCatchUpSeconds = 24 * 3600;
    private const int ItemLevel = 80;
    private const int FillBudgetMs = 150;
    private const int FirstVisitListings = 100; // shown at once on a first visit; the rest arrive in the background
    private const double BackgroundFillInterval = 0.2; // seconds between background batches: relaxed so play stays smooth
    private const int BackgroundBatch = 1; // listings tried per batch
    private const int BackgroundBudgetMs = 4; // time a batch may take
    private static double _nextBackgroundFillAt;
    private const int MaxGenerateAttempts = 4000;

    private static readonly List<OfflineListing> _all = new();
    private static readonly Random _rng = new();
    private static readonly Dictionary<int, int[]> _itemTypesOfStall = new();
    private static readonly Dictionary<int, double> _pendingArrivals = new();
    private static Dictionary<int, long> _stallSimUnix = new();
    private static bool _loaded;
    private static bool _dirty;
    private static double _nextTickAt;
    private static double _nextSaveAt;
    private static int _activeStall = -1; // the stall you last looked at; the market keeps it moving
    private static double _nextActiveCatchUp;

    [ThreadStatic]
    private static bool _rolling;

    private static double _debugCheckedAt = -100;
    private static bool _debugOn;

    /// <summary>Debug logging is off unless a file named debug.enabled sits in the mod's folder.</summary>
    private static bool DebugLogging()
    {
        double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
        if (now - _debugCheckedAt > 10)
        {
            _debugCheckedAt = now;
            try
            {
                _debugOn = File.Exists(Path.Combine(Folder, "debug.enabled"));
            }
            catch (Exception)
            {
                _debugOn = false;
            }
        }
        return _debugOn;
    }

    /// <summary>Crash-proof breadcrumb (debug only): appended and flushed immediately, so it survives a native crash.</summary>
    public static void Crumb(string message)
    {
        if (!DebugLogging())
        {
            return;
        }
        try
        {
            File.AppendAllText(
                Path.Combine(Folder, "bazaar_debug.log"),
                DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine
            );
        }
        catch (Exception) { }
    }

    private static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Mods", Main.mod_name);

    private static string SavePath => Path.Combine(Folder, "OfflineBazaar.json");

    // ---------------------------------------------------------------- persistence

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        try
        {
            if (!File.Exists(SavePath))
            {
                return;
            }
            string json = File.ReadAllText(SavePath);
            BazaarSave save = json.TrimStart().StartsWith("[")
                ? new BazaarSave { Listings = JsonConvert.DeserializeObject<List<OfflineListing>>(json) ?? new() }
                : JsonConvert.DeserializeObject<BazaarSave>(json) ?? new BazaarSave();
            _all.AddRange(save.Listings);
            _stallSimUnix = save.StallSimUnix ?? new();

            // Stock made before the pricing model was rewritten has the old prices: let the market restock.
            int dropped = _all.RemoveAll(l => !l.Mine && l.Meta < OfflineListing.CurrentMeta);
            if (dropped > 0)
            {
                _stallSimUnix.Clear();
            }
            int rebuilt = 0;
            foreach (OfflineListing l in _all.Where(l => l.Meta < OfflineListing.CurrentMeta))
            {
                try
                {
                    ItemData item = ToItem(l);
                    if (item != null)
                    {
                        Describe(l, item, l.LevelReq, BazaarItemFactory.Kind.Common, false);
                        if (l.Mine)
                        {
                            // Your own listing keeps the price you set; its value is re-judged with roll quality.
                            l.FairValue = BazaarPricing.Appraise(item, l.LevelReq, l.Corrupted).Fair;
                        }
                        rebuilt++;
                    }
                }
                catch (Exception) { }
            }
            Crumb("loaded " + _all.Count + " listings (" + _all.Count(l => l.Mine) + " yours), dropped " + dropped + " old stock, rebuilt " + rebuilt);
        }
        catch (Exception ex)
        {
            Crumb("load failed: " + ex.Message);
        }
    }

    private static void MarkDirty() => _dirty = true;

    private static void SaveNow()
    {
        try
        {
            File.WriteAllText(SavePath, JsonConvert.SerializeObject(new BazaarSave { Listings = _all, StallSimUnix = _stallSimUnix }));
            _dirty = false;
        }
        catch (Exception ex)
        {
            Crumb("save failed: " + ex.Message);
        }
    }

    // ---------------------------------------------------------------- per-frame

    /// <summary>Called every frame from the main thread: your sales and saving, every couple of seconds.</summary>
    public static void Tick()
    {
        double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
        if (WarmUp(now))
        {
            return; // the game is held while the shelves are first stocked
        }
        if (!_loaded)
        {
            return;
        }
        BazaarFocus(now);
        BackgroundWork(now);
        BackgroundRotate(now);
        if (now < _nextTickAt)
        {
            return;
        }
        _nextTickAt = now + 2;
        SimulateYourSales();
        // The shelf is large now, so write it out every half minute rather than on every change.
        if (_dirty && now >= _nextSaveAt)
        {
            _nextSaveAt = now + 180;
            SaveNow();
        }
    }

    /// <summary>Called when the game quits, so nothing since the last save is lost.</summary>
    public static void SaveOnQuit()
    {
        if (_loaded && _dirty)
        {
            SaveNow();
        }
    }

    /// <summary>
    /// While you stay at a stall the market keeps moving: every so often time passes (sales and arrivals),
    /// and the new listings are rolled one per frame so there is never a hitch.
    /// </summary>
    private static void BackgroundWork(double now)
    {
        if (_activeStall < 0 || _rolling || !Scenes.IsGameScene())
        {
            return;
        }
        if (now >= _nextActiveCatchUp)
        {
            _nextActiveCatchUp = now + 10;
            CatchUp(_activeStall, withArrivals: true, deferFill: true);
        }
        if (now >= _nextBackgroundFillAt && PendingArrivals(_activeStall) >= 1 && NpcCount(_activeStall) < StockPerStall)
        {
            // With the Bazaar screen open the rest of the game is held, so the market gets much more of each frame.
            bool focus = _bazaarOpen;
            _nextBackgroundFillAt = now + (focus ? 0.0 : BackgroundFillInterval);
            int batch = focus ? BazaarFocusBatch : BackgroundBatch;
            int added = Fill(_activeStall, (int)Math.Min(batch, PendingArrivals(_activeStall)), budgetMs: focus ? BazaarFocusBudgetMs : BackgroundBudgetMs, log: false);
            if (added > 0)
            {
                _pendingArrivals[_activeStall] = Math.Max(0, PendingArrivals(_activeStall) - added);
            }
        }
    }

    private static void SimulateYourSales()
    {
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (OfflineListing l in _all)
        {
            if (l.Mine && !l.Sold && l.SellAtUnix > 0 && nowUnix >= l.SellAtUnix)
            {
                l.Sold = true;
                l.SoldUnix = nowUnix;
                MarkDirty();
                long net = l.Price - l.Tax;
                Crumb("your listing sold for " + l.Price + " (you receive " + net + ")");
                Main.logger_instance?.Msg("Bazaar: one of your items sold for " + l.Price + " gold. Collect it from My Stall.");
            }
        }
    }

    // ---------------------------------------------------------------- queries

    private static List<OfflineListing> Loaded()
    {
        EnsureLoaded();
        return _all;
    }

    public static IEnumerable<OfflineListing> Mine() => Loaded().Where(l => l.Mine && !l.Redeemed);

    public static int RedeemableCount()
    {
        EnsureLoaded();
        SimulateYourSales();
        return Mine().Count(l => l.Sold);
    }

    public static OfflineListing Find(Guid id) => Loaded().FirstOrDefault(l => l.Id == id);

    /// <summary>Learn which item types a stall holds (the request carries them), for stalls that are not 1:1.</summary>
    public static void LearnStall(int stall, int[] itemTypes)
    {
        if (stall >= 0 && itemTypes != null && itemTypes.Length > 0)
        {
            bool changed = !_itemTypesOfStall.TryGetValue(stall, out int[] old) || !old.SequenceEqual(itemTypes);
            _itemTypesOfStall[stall] = itemTypes;
            if (changed)
            {
                SaveStallTypes();
            }
        }
    }

    // Class idol stalls (24..28) share item types; the idol's class tells them apart. Class is a bit flag.
    private static int ClassBitOfStall(int stall) => stall >= 24 && stall <= 28 ? 1 << (stall - 24) : 0;

    /// <summary>Does this item belong on this stall's shelf?</summary>
    private static bool Fits(int stall, int itemType, int classReq)
    {
        int bit = ClassBitOfStall(stall);
        if (bit != 0 && (classReq & bit) == 0)
        {
            return false;
        }
        if (_itemTypesOfStall.TryGetValue(stall, out int[] types))
        {
            return types.Contains(itemType);
        }
        return itemType == stall;
    }

    public static int StallOfItemType(int itemType)
    {
        foreach (KeyValuePair<int, int[]> kv in _itemTypesOfStall)
        {
            if (kv.Value.Contains(itemType))
            {
                return kv.Key;
            }
        }
        return itemType >= 0 && itemType <= 22 ? itemType : -1;
    }

    private static int NpcCount(int stall)
    {
        int n = 0;
        foreach (OfflineListing l in _all)
        {
            if (!l.Mine && l.Stall == stall && !l.Sold)
            {
                n++;
            }
        }
        return n;
    }

    private static List<OfflineListing> Npc(int stall) => _all.Where(l => !l.Mine && l.Stall == stall && !l.Sold).ToList();

    /// <summary>
    /// The stall's shelf, brought up to date. Time passing sells listings (underpriced ones go first) and
    /// brings new ones, so what you see keeps turning over even while you are elsewhere.
    /// </summary>
    public static List<OfflineListing> StockFor(int stall)
    {
        EnsureLoaded();
        _activeStall = stall;
        _activeAt = UnityEngine.Time.realtimeSinceStartupAsDouble;
        CatchUp(stall, withArrivals: true);
        return Npc(stall);
    }

    /// <summary>Settle the sales that happened since the stall was last looked at. Used before a purchase too.</summary>
    public static void SettleSales(int stall) => CatchUp(stall, withArrivals: false);

    private static void CatchUp(int stall, bool withArrivals, bool deferFill = false)
    {
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!_stallSimUnix.TryGetValue(stall, out long last))
        {
            // First visit: stock what fits in this frame's budget, and let the background filler complete
            // the rest of the shelf over the next few seconds.
            Fill(stall, Math.Min(FirstVisitListings, StockPerStall - Npc(stall).Count), budgetMs: 1500);
            _stallSimUnix[stall] = nowUnix;
            _pendingArrivals[stall] = Math.Max(0, StockPerStall - Npc(stall).Count);
            MarkDirty();
            return;
        }

        double minutes = Math.Min(nowUnix - last, MaxCatchUpSeconds) / 60.0;
        if (minutes < 0.05)
        {
            if (withArrivals)
            {
                Fill(stall, (int)Math.Min(PendingArrivals(stall), StockPerStall - Npc(stall).Count));
            }
            return;
        }

        // Buyers: each listing may have been taken, underpriced ones very likely.
        int bought = 0;
        int takenDown = 0;
        foreach (OfflineListing l in Npc(stall).ToList())
        {
            double mean = BazaarPricing.MeanMinutesToSell(l.Price, l.FairValue, NpcTurnoverMinutes);
            if (_rng.NextDouble() < 1 - Math.Exp(-minutes / mean))
            {
                _all.Remove(l);
                bought++;
            }
            else if (_rng.NextDouble() < 1 - Math.Exp(-minutes / BazaarPricing.MeanMinutesToTakeDown(l.Price, l.FairValue, mean)))
            {
                // The seller gives up waiting and takes it down (to relist elsewhere, or keep).
                _all.Remove(l);
                takenDown++;
            }
        }
        _stallSimUnix[stall] = nowUnix;
        MarkDirty();

        if (withArrivals)
        {
            // Sellers: new listings arrive at the rate that keeps the shelf around its usual size.
            double perMinute = StockPerStall / NpcTurnoverMinutes;
            _pendingArrivals[stall] = PendingArrivals(stall) + Poisson(perMinute * minutes);
            int room = StockPerStall - Npc(stall).Count;
            int added = 0;
            if (!deferFill)
            {
                int want = (int)Math.Min(PendingArrivals(stall), room);
                added = Fill(stall, want);
                _pendingArrivals[stall] = Math.Max(0, PendingArrivals(stall) - added);
            }
            Crumb("stall " + stall + ": " + (int)minutes + " min passed, " + bought + " bought, " + takenDown + " taken down, " + added + " new now, " + (int)PendingArrivals(stall) + " more arriving, " + Npc(stall).Count + " on shelf");
        }
    }

    private static double PendingArrivals(int stall) => _pendingArrivals.TryGetValue(stall, out double v) ? v : 0;

    private static int Poisson(double mean)
    {
        // Knuth's method; the means here are small.
        double limit = Math.Exp(-Math.Min(mean, 50));
        double product = _rng.NextDouble();
        int n = 0;
        while (product > limit && n < 200)
        {
            n++;
            product *= _rng.NextDouble();
        }
        return n;
    }

    // ---------------------------------------------------------------- generation

    private static int Fill(int stall, int count, int budgetMs = FillBudgetMs, bool log = true)
    {
        if (count <= 0 || _rolling)
        {
            return 0;
        }
        _rolling = true;
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            int attempts = 0;
            int added = 0;
            var kinds = new Dictionary<string, int>();
            while (added < count && attempts++ < MaxGenerateAttempts && clock.ElapsedMilliseconds < budgetMs)
            {
                OfflineListing l = RollListing(stall);
                if (l != null)
                {
                    _all.Add(l);
                    added++;
                    kinds[l.Kind ?? "?"] = kinds.TryGetValue(l.Kind ?? "?", out int k) ? k + 1 : 1;
                    foreach (string feature in (l.Note ?? "").Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        kinds["+" + feature] = kinds.TryGetValue("+" + feature, out int fk) ? fk + 1 : 1;
                    }
                }
            }
            if (added > 0)
            {
                MarkDirty();
            }
            if (log)
                Crumb("stall " + stall + ": rolled " + added + "/" + count + " in " + attempts + " attempts, " + clock.ElapsedMilliseconds + " ms [" + string.Join(", ", kinds.Select(kv => kv.Key + " " + kv.Value)) + "]");
            return added;
        }
        finally
        {
            _rolling = false;
        }
    }

    private static GenerateItems _generator;

    /// <summary>GenerateItems.instance is only set in gameplay zones; the component lives on the player.</summary>
    private static GenerateItems FindGenerator()
    {
        if (GenerateItems.instance != null)
        {
            return GenerateItems.instance;
        }
        if (_generator.IsNullOrDestroyed())
        {
            _generator = UnityEngine.Object.FindObjectOfType<GenerateItems>();
            Crumb("generator " + (_generator.IsNullOrDestroyed() ? "NOT FOUND" : "found on player"));
        }
        return _generator;
    }

    private static int _misfits;

    private static OfflineListing RollListing(int stall)
    {
        GenerateItems generator = FindGenerator();
        if (generator == null)
        {
            return null;
        }
        _itemTypesOfStall.TryGetValue(stall, out int[] types);
        BazaarItemFactory.Rolled rolled = BazaarItemFactory.Roll(generator, stall, types, ItemLevel, _rng);
        ItemDataUnpacked unpacked = rolled?.Item;
        if (unpacked == null)
        {
            return null;
        }
        if (!Fits(stall, unpacked.itemType, (int)unpacked.classReq))
        {
            // Wrong slot or class for this shelf. Seeing many of these for a class stall means the class
            // request is being ignored.
            if (_misfits++ < 5)
            {
                Crumb("misfit on stall " + stall + ": itemType " + unpacked.itemType + " classReq " + (int)unpacked.classReq + " kind " + rolled.Kind);
            }
            return null;
        }
        unpacked.RefreshIDAndValues();
        ItemData item = unpacked.TryCast<ItemData>();
        if (item != null && BazaarRarityOf(item) == 0)
        {
            return null; // plain gray items add nothing to the market
        }
        byte[] bytes = item == null ? null : ToBytes(item);
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }

        BazaarPricing.Appraisal value = BazaarPricing.Appraise(item, unpacked.LvlReq, rolled.Corrupted);
        // Sellers rarely list junk, and when they do it is not at a silly price.
        if (value.Garbage && _rng.NextDouble() > GarbageListChance)
        {
            return null;
        }
        var l = new OfflineListing
        {
            ItemBase64 = Convert.ToBase64String(bytes),
            Price = NpcPrice(value.Fair),
            FairValue = value.Fair,
            Stall = stall,
            Note = rolled.Note,
        };
        Describe(l, item, unpacked.LvlReq, rolled.Kind, rolled.Corrupted);
        if (rolled.Twin)
        {
            // A Rune of Creation was used: the identical copy is on sale too, from another seller.
            var twin = new OfflineListing
            {
                ItemBase64 = l.ItemBase64,
                Price = NpcPrice(value.Fair),
                FairValue = value.Fair,
                Stall = stall,
                Note = rolled.Note,
            };
            Describe(twin, item, unpacked.LvlReq, rolled.Kind, rolled.Corrupted);
            _all.Add(twin);
        }
        return l;
    }

    /// <summary>
    /// What an NPC seller asks. Mostly close to value, sometimes a lowball (an opportunity to snipe) and
    /// sometimes greedy (which sits there for a long time).
    /// </summary>
    private static long NpcPrice(long fair)
    {
        double r = _rng.NextDouble();
        double factor;
        if (r < 0.07)
        {
            factor = 0.45 + _rng.NextDouble() * 0.4; // lowball
        }
        else if (r < 0.14)
        {
            factor = 1.4 + _rng.NextDouble() * 1.0; // greedy
        }
        else
        {
            double gaussian = Math.Sqrt(-2 * Math.Log(1 - _rng.NextDouble())) * Math.Cos(2 * Math.PI * _rng.NextDouble());
            factor = Math.Clamp(Math.Exp(0.15 * gaussian), 0.7, 1.4);
        }
        return Math.Max(5, (long)(fair * factor));
    }

    /// <summary>Caches the facts the filters need so a search never has to rebuild items.</summary>
    private static OfflineListing Describe(OfflineListing l, ItemData item, int levelReq, BazaarItemFactory.Kind kind, bool corrupted)
    {
        try
        {
            ItemDataUnpacked unpacked = item.TryCast<ItemDataUnpacked>();
            if (unpacked != null)
            {
                l.ClassReq = (int)unpacked.classReq;
                if (levelReq <= 0)
                {
                    levelReq = unpacked.LvlReq;
                }
            }
        }
        catch (Exception) { }
        l.Kind = kind.ToString();
        l.Corrupted = corrupted;
        l.ItemType = item.itemType;
        l.SubType = item.subType;
        l.Rarity = item.rarity;
        l.BazaarRarity = BazaarRarityOf(item);
        l.ForgingPotential = item.forgingPotential;
        l.LevelReq = levelReq;
        l.UniqueId = item.uniqueID;
        l.WeaversWill = item.weaversWill;
        l.LegendaryPotential = item.legendaryPotential;
        l.PrimordialSealed = item.hasSealedPrimordialAffix;
        l.Affixes = new List<AffixInfo>();
        if (item.affixes != null)
        {
            foreach (ItemAffix a in item.affixes)
            {
                if (a != null)
                {
                    l.Affixes.Add(new AffixInfo { Id = a.affixId, Roll = a.affixRoll, Tier = a.affixTier, Sealed = a.IsSealed });
                }
            }
        }
        l.Note = JoinNotes(l.Note, Features(item));
        l.Meta = OfflineListing.CurrentMeta;
        return l;
    }

    private static string JoinNotes(string existing, IEnumerable<string> features)
    {
        var all = new List<string>();
        if (!string.IsNullOrEmpty(existing))
        {
            all.AddRange(existing.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries));
        }
        foreach (string f in features)
        {
            if (!all.Contains(f))
            {
                all.Add(f);
            }
        }
        return string.Join("; ", all);
    }

    /// <summary>What kind of item this is, in words: sealed, experimental, primordial, personal, weaver, Ice...</summary>
    private static List<string> Features(ItemData item)
    {
        var f = new List<string>();
        try
        {
            if (item.affixes != null)
            {
                foreach (ItemAffix a in item.affixes)
                {
                    if (a == null)
                    {
                        continue;
                    }
                    if (a.IsSealedPrimordial) f.Add("Primordial T8");
                    else if (a.IsSealedCorrupted) f.Add("Corrupted affix");
                    else if (a.IsSealedRegular) f.Add("Sealed");
                    string special = a.specialAffixType.ToString();
                    if (special != "Standard") f.Add(special);
                }
                if (item.affixes.Count >= 6) f.Add("6 affixes");
            }
            string potential = item.forgingPotentialType.ToString();
            if (potential == "Ice" || potential == "Blood") f.Add(potential + " FP");
            if (item.weaversWill > 0) f.Add("Weaver's Will");
            if (item.rarity == 8) f.Add("Set");
        }
        catch (Exception) { }
        return f;
    }

    /// <summary>The stall filter's rarity for this item, using the game's own conversion.</summary>
    private static int BazaarRarityOf(ItemData item)
    {
        try
        {
            return (int)Il2CppLE.Services.Bazaar.BazaarItemRarityHelper.GetBazaarItemRarity(item);
        }
        catch (Exception)
        {
            // Fallback: 9 legendary, 8 set, 7 unique, otherwise by the number of affixes.
            return item.rarity == 9 ? 6 : item.rarity == 8 ? 4 : item.rarity == 7 ? 5 : item.rarity == 0 ? 0 : item.rarity <= 2 ? 1 : 2;
        }
    }

    private static byte[] ToBytes(ItemData item)
    {
        var id = item.id;
        if (id == null)
        {
            return null;
        }
        var bytes = new byte[id.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = id[i];
        }
        return bytes;
    }

    /// <summary>Rebuilds the game item from a listing's stored bytes.</summary>
    public static ItemData ToItem(OfflineListing l)
    {
        var bytes = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(l.ItemBytes.Length);
        for (int i = 0; i < l.ItemBytes.Length; i++)
        {
            bytes[i] = l.ItemBytes[i];
        }
        var item = new ItemDataUnpacked();
        item.RebuildFromID(bytes);
        item.RefreshIDAndValues();
        return item.TryCast<ItemData>();
    }

    // ---------------------------------------------------------------- your listings

    public static OfflineListing AddMine(ItemData item, long price, int levelReq)
    {
        EnsureLoaded();
        byte[] bytes = ToBytes(item);
        if (bytes == null || bytes.Length == 0)
        {
            return null;
        }
        BazaarPricing.Appraisal value = BazaarPricing.Appraise(item, levelReq, corrupted: false);
        var l = Describe(
            new OfflineListing
            {
                ItemBase64 = Convert.ToBase64String(bytes),
                Price = price,
                FairValue = value.Fair,
                Stall = StallOfItemType(item.itemType),
                Mine = true,
            },
            item,
            levelReq,
            BazaarItemFactory.Kind.Common,
            false
        );
        l.SellAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + SaleDelaySeconds(price, value.Fair);
        _all.Add(l);
        MarkDirty();
        Crumb("listed yours: price " + price + " fair " + value.Fair + " (" + Math.Round(price / Math.Max(1.0, value.Fair), 2) + "x) sells in " + (l.SellAtUnix - l.ListedUnix) + " s");
        return l;
    }

    /// <summary>
    /// When a simulated buyer takes your listing. A fair price sells in about
    /// <see cref="YourListingBaseMinutes" />, a greedy one waits or never sells, and one priced well under
    /// its value is sniped within a minute or two.
    /// </summary>
    private static long SaleDelaySeconds(long price, long fair)
    {
        double ratio = price / Math.Max(1.0, fair);
        if (ratio >= NeverSellRatio)
        {
            return 365L * 24 * 3600;
        }
        if (BazaarPricing.IsSnipeBait(price, fair))
        {
            return 15 + _rng.Next(0, 75);
        }
        double mean = BazaarPricing.MeanMinutesToSell(price, fair, YourListingBaseMinutes);
        double minutes = -Math.Log(1 - _rng.NextDouble()) * mean; // exponential waiting time
        return (long)(Math.Clamp(minutes, 0.5, MaxYourListingMinutes) * 60);
    }

    public static void Remove(OfflineListing l)
    {
        _all.Remove(l);
        MarkDirty();
    }

    public static void MarkChanged() => MarkDirty();

    // ---------------------------------------------------------------- delivering items to the player

    /// <summary>Puts the item in the first free inventory space, or drops it at your feet if there is none.</summary>
    public static void DeliverToPlayer(ItemData item)
    {
        Actor player = Scripts.Refs_Manager.player_actor;
        var containers = Scripts.Refs_Manager.item_containers_manager;
        if (!player.IsNullOrDestroyed() && !containers.IsNullOrDestroyed())
        {
            try
            {
                if (containers.attemptToPickupItem(item, player.position()))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Crumb("pickup failed: " + ex.Message);
            }
        }
        if (!player.IsNullOrDestroyed() && !Scripts.Refs_Manager.ground_item_manager.IsNullOrDestroyed())
        {
            Main.logger_instance?.Msg("Bazaar: inventory full, dropped the item at your feet.");
            ModItemDrop.Drop(Scripts.Refs_Manager.ground_item_manager, player, item, player.position());
        }
    }
}
