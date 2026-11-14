using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using Il2CppEHG.Multiplayer;
using Il2CppLE.Data;
using Il2CppLE.Services.Bazaar;
using Il2CppLE.UI.Bazaar;
using Il2CppLE.UI.PanelSystem;
using Il2CppOperationResult;
using Newtonsoft.Json.Linq;

namespace LastEpoch_Hud.Scripts.Mods.Economy;

/// <summary>What a Bazaar query asked for, read from the request's own serialisation.</summary>
internal sealed class BazaarQuery
{
    public int Stall = -1;
    public int Page = 1;
    public int PageSize = 12;
    public int[] ItemTypes = Array.Empty<int>();
    public int[] SubTypes = Array.Empty<int>();
    public int? Rarity;
    public int Sorting = 1;
    public long? MinGold, MaxGold;
    public int? MinFp, MaxFp, MinLevel, MaxLevel;
    public int? MinWw, MaxWw, MinLp, MaxLp;
    public int[] UniqueIds = Array.Empty<int>();
    public bool? Primordial;
    public int Corrupted; // 0 any, 1 only corrupted, 2 only not corrupted (the dropdown: ANY / YES / NO)
    public int Tier6, Tier7, NonSealedTier6, NonSealedTier7;

    // "At least RequiredCount of these affixes, each rolled at least MinRoll."
    public List<(int Required, List<(int Id, int MinRoll)> Affixes)> AffixGroups = new();

    public bool Matches(OfflineListing l)
    {
        if (ItemTypes.Length > 0 && !ItemTypes.Contains(l.ItemType)) return false;
        if (SubTypes.Length > 0 && !SubTypes.Contains(l.SubType)) return false;
        if (Rarity.HasValue && l.BazaarRarity != Rarity.Value) return false;
        if (MinGold.HasValue && l.Price < MinGold.Value) return false;
        if (MaxGold.HasValue && l.Price > MaxGold.Value) return false;
        if (MinFp.HasValue && l.ForgingPotential < MinFp.Value) return false;
        if (MaxFp.HasValue && l.ForgingPotential > MaxFp.Value) return false;
        if (MinLevel.HasValue && l.LevelReq < MinLevel.Value) return false;
        if (MaxLevel.HasValue && l.LevelReq > MaxLevel.Value) return false;
        if (MinWw.HasValue && l.WeaversWill < MinWw.Value) return false;
        if (MaxWw.HasValue && l.WeaversWill > MaxWw.Value) return false;
        if (MinLp.HasValue && l.LegendaryPotential < MinLp.Value) return false;
        if (MaxLp.HasValue && l.LegendaryPotential > MaxLp.Value) return false;
        if (Primordial.HasValue && l.PrimordialSealed != Primordial.Value) return false;
        if (Corrupted == 1 && !l.Corrupted) return false;
        if (Corrupted == 2 && l.Corrupted) return false;
        if (UniqueIds.Length > 0 && !UniqueIds.Contains(l.UniqueId)) return false;
        foreach (var group in AffixGroups)
        {
            int have = group.Affixes.Count(a => l.Affixes.Any(x => x.Id == a.Id && x.Roll >= a.MinRoll));
            if (have < Math.Min(group.Required, group.Affixes.Count)) return false;
        }
        // Displayed tier is the internal tier + 1.
        if (Tier6 > 0 && l.Affixes.Count(a => a.Tier + 1 == 6) < Tier6) return false;
        if (Tier7 > 0 && l.Affixes.Count(a => a.Tier + 1 == 7) < Tier7) return false;
        if (NonSealedTier6 > 0 && l.Affixes.Count(a => !a.Sealed && a.Tier + 1 == 6) < NonSealedTier6) return false;
        if (NonSealedTier7 > 0 && l.Affixes.Count(a => !a.Sealed && a.Tier + 1 == 7) < NonSealedTier7) return false;
        return true;
    }

    public IEnumerable<OfflineListing> Sort(IEnumerable<OfflineListing> items) =>
        Sorting switch
        {
            0 => items.OrderByDescending(l => l.ListedUnix),
            2 => items.OrderByDescending(l => l.Price),
            _ => items.OrderBy(l => l.Price),
        };

    /// <summary>
    /// The interop Nullable wrappers misread; the request's own serialisation (PackData) is reliable.
    /// Unknown or absent fields keep their defaults.
    /// </summary>
    public static BazaarQuery Read(QueryItemsRequestData r)
    {
        var q = new BazaarQuery();
        try
        {
            var packed = r.PackData();
            var bytes = new byte[packed.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = packed[i];
            }
            string json = System.Text.Encoding.UTF8.GetString(bytes);
            var obj = JObject.Parse(json);
            q.Stall = Int(obj, "stalltype") ?? -1;
            q.Page = Math.Max(1, Int(obj, "page") ?? 1);
            q.PageSize = Math.Max(1, Int(obj, "pagesize") ?? 12);
            q.ItemTypes = Ints(obj, "itemtypes");
            q.Rarity = Int(obj, "rarity");
            q.Sorting = Int(obj, "sorting") ?? 1;
            q.MinGold = Long(obj, "mingoldvalue");
            q.MaxGold = Long(obj, "maxgoldvalue");
            q.MinFp = Int(obj, "minforgingpotential");
            q.MaxFp = Int(obj, "maxforgingpotential");
            q.MinLevel = Int(obj, "minrequiredlevel");
            q.MaxLevel = Int(obj, "maxrequiredlevel");
            q.MinWw = Int(obj, "minweaverswill");
            q.MaxWw = Int(obj, "maxweaverswill");
            q.MinLp = Int(obj, "minlegendarypotential");
            q.MaxLp = Int(obj, "maxlegendarypotential");
            q.Tier6 = Int(obj, "tier6affixcount") ?? 0;
            q.Tier7 = Int(obj, "tier7affixcount") ?? 0;
            q.NonSealedTier6 = Int(obj, "nonsealedtier6affixcount") ?? 0;
            q.NonSealedTier7 = Int(obj, "nonsealedtier7affixcount") ?? 0;
            q.UniqueIds = Ints(obj, "uniquesfilters");
            JToken corrupted = Find(obj, "iscorrupted");
            q.Corrupted = corrupted is JValue cv && cv.Value is bool cb && cb ? 1 : 0;
            // the request cannot tell ANY from NO (both arrive as false), so ask the filter itself
            try
            {
                var filter = UnityEngine.Object.FindObjectOfType<Il2CppLE.UI.Bazaar.FilterUI>();
                if (filter != null && filter.corruptedSelection != null)
                {
                    var flag = filter.corruptedSelection.SelectedFlag;
                    q.Corrupted = flag == Il2CppLE.UI.Bazaar.BazaarItemFlag.YES ? 1 : flag == Il2CppLE.UI.Bazaar.BazaarItemFlag.NO ? 2 : 0;
                }
            }
            catch (Exception)
            {
            }
            JToken primordial = Find(obj, "isprimordial");
            if (primordial != null)
            {
                q.Primordial = (bool)((JValue)primordial).Value;
            }
            if (Find(obj, "affixfilters") is JArray groups)
            {
                foreach (JObject g in groups.OfType<JObject>())
                {
                    var affixes = new List<(int, int)>();
                    if (Find(g, "affixes") is JArray list)
                    {
                        foreach (JObject a in list.OfType<JObject>())
                        {
                            affixes.Add((Int(a, "id") ?? -1, Int(a, "minroll") ?? 0));
                        }
                    }
                    q.AffixGroups.Add((Int(g, "requiredcount") ?? 1, affixes));
                }
            }
            if (Find(obj, "subtypes") is JArray subs)
            {
                q.SubTypes = subs.Select(s => s is JObject o ? Int(o, "subtype") : (int?)s).Where(v => v.HasValue).Select(v => v.Value).ToArray();
            }
            OfflineBazaarMarket.Crumb("request: " + (json.Length > 300 ? json.Substring(0, 300) : json));
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("query parse failed: " + ex.Message);
        }
        return q;
    }

    private static JToken Find(JObject o, string lowerName)
    {
        foreach (JProperty p in o.Properties())
        {
            if (p.Name.ToLowerInvariant() == lowerName && p.Value.Type != JTokenType.Null)
            {
                return p.Value;
            }
        }
        return null;
    }

    private static int? Int(JObject o, string name)
    {
        JToken t = Find(o, name);
        return t == null ? null : (int?)Convert.ToInt32(((JValue)t).Value);
    }

    private static long? Long(JObject o, string name)
    {
        JToken t = Find(o, name);
        return t == null ? null : (long?)Convert.ToInt64(((JValue)t).Value);
    }

    private static int[] Ints(JObject o, string name) =>
        Find(o, name) is JArray a ? a.Select(v => Convert.ToInt32(((JValue)v).Value)).ToArray() : Array.Empty<int>();
}

/// <summary>Builds the objects the game's Bazaar UI expects.</summary>
internal static class BazaarReply
{
    private static ItemListingData.PriceLoweringData NoPriceLowering(long price) =>
        new ItemListingData.PriceLoweringData { LowerAmount = 0, FloorPrice = 0, OriginalGoldValue = price };

    public static ItemListingData ToData(OfflineListing l)
    {
        var bytes = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(l.ItemBytes.Length);
        for (int i = 0; i < l.ItemBytes.Length; i++)
        {
            bytes[i] = l.ItemBytes[i];
        }
        return new ItemListingData(
            new Il2CppSystem.Guid(l.Id.ToString()),
            bytes,
            l.Price,
            l.Mine ? l.Tax : 0,
            0,
            l.Redeemed ? BazaarItemState.Redeemed : l.Sold ? BazaarItemState.Sold : BazaarItemState.Unsold,
            l.Mine,
            true,
            l.ListedUnix,
            new Il2CppSystem.Guid(l.Version.ToString()),
            NoPriceLowering(l.Price)
        );
    }

    public static UniTask<Result<QueryItemsResponse, BazaarOperationError>> Page(List<OfflineListing> all, BazaarQuery q)
    {
        int totalPages = Math.Max(1, (all.Count + q.PageSize - 1) / q.PageSize);
        int page = Math.Min(q.Page, totalPages);
        var items = new Il2CppSystem.Collections.Generic.List<ItemListingData>();
        foreach (OfflineListing l in all.Skip((page - 1) * q.PageSize).Take(q.PageSize))
        {
            items.Add(ToData(l));
        }
        BazaarRefresh.LastPage = page;
        var response = new QueryItemsResponse(items, page, totalPages, all.Count);
        return new UniTask<Result<QueryItemsResponse, BazaarOperationError>>(
            new Result<QueryItemsResponse, BazaarOperationError>(response)
        );
    }

    public static UniTask<Status<BazaarOperationError>> Done(BazaarOperationError? error)
    {
        if (error.HasValue)
        {
            return new UniTask<Status<BazaarOperationError>>(new Status<BazaarOperationError>(error.Value));
        }
        BazaarRefresh.Request();
        return new UniTask<Status<BazaarOperationError>>(Status<BazaarOperationError>.SuccessStatus);
    }
}

[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.QueryItems))]
public class BazaarQueryItemsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        QueryItemsRequestData requestData,
        ref UniTask<Result<QueryItemsResponse, BazaarOperationError>> __result
    )
    {
        if (GameplayEnvironment.IsOnlinePlay || requestData == null)
        {
            return true;
        }
        try
        {
            BazaarQuery q = BazaarQuery.Read(requestData);
            if (q.Stall < 0)
            {
                q.Stall = StallNpcClickedPatch.LastStall;
            }
            OfflineBazaarMarket.LearnStall(q.Stall, q.ItemTypes);
            var stock = OfflineBazaarMarket.StockFor(q.Stall);
            var all = q.Sort(stock.Where(q.Matches)).ToList();
            OfflineBazaarMarket.Crumb("browse stall " + q.Stall + ": " + all.Count + " match, page " + q.Page);
            __result = BazaarReply.Page(all, q);
            return false;
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("QueryItems failed: " + ex);
            return true;
        }
    }
}

/// <summary>"My Stall": your listed items, and your sold items waiting to be collected.</summary>
[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.QueryPlayerItems))]
public class BazaarQueryPlayerItemsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        QueryItemsRequestData requestData,
        bool soldItems,
        ref UniTask<Result<QueryItemsResponse, BazaarOperationError>> __result
    )
    {
        if (GameplayEnvironment.IsOnlinePlay || requestData == null)
        {
            return true;
        }
        try
        {
            BazaarQuery q = BazaarQuery.Read(requestData);
            OfflineBazaarMarket.RedeemableCount(); // runs due sales first
            var mine = OfflineBazaarMarket.Mine().Where(l => l.Sold == soldItems).OrderByDescending(l => l.ListedUnix).ToList();
            OfflineBazaarMarket.Crumb("my stall (" + (soldItems ? "sold" : "listed") + "): " + mine.Count);
            __result = BazaarReply.Page(mine, q);
            return false;
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("QueryPlayerItems failed: " + ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.ListItem))]
public class BazaarListItemPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        ListItemRequestRequestData requestData,
        ref UniTask<Status<BazaarOperationError>> __result
    )
    {
        if (GameplayEnvironment.IsOnlinePlay || requestData == null)
        {
            return true;
        }
        __result = BazaarReply.Done(List(requestData));
        return false;
    }

    private static BazaarOperationError? List(ListItemRequestRequestData request)
    {
        try
        {
            long price = request.listingPrice;
            if (price <= 0)
            {
                return BazaarOperationError.ITEM_PRICE_BELOW_ZERO;
            }
            BazaarPanel panel = BazaarPanelOpenOfflinePatch.Panel;
            if (panel.IsNullOrDestroyed() || panel._listItemContainer.IsNullOrDestroyed())
            {
                return BazaarOperationError.NO_ITEM_TO_LIST;
            }
            IItemContainer container = panel._listItemContainer.Container;
            var content = container?.GetContent();
            if (content == null || content.Count == 0)
            {
                return BazaarOperationError.NO_ITEM_TO_LIST;
            }
            ItemContainerEntry entry = content[0];
            ItemData item = entry?.data;
            if (item.IsNullOrDestroyed())
            {
                return BazaarOperationError.NO_ITEM_TO_LIST;
            }
            int levelReq = 0;
            try
            {
                levelReq = item.getAsUnpacked()?.LvlReq ?? 0;
            }
            catch (Exception) { }
            OfflineListing listing = OfflineBazaarMarket.AddMine(item, price, levelReq);
            if (listing == null)
            {
                return BazaarOperationError.LISTING_FAILED_GENERIC;
            }
            if (!container.TryRemoveItem(entry, 1, Context.DEFAULT))
            {
                OfflineBazaarMarket.Remove(listing);
                return BazaarOperationError.LISTING_FAILED_GENERIC;
            }
            return null;
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("ListItem failed: " + ex);
            return BazaarOperationError.LISTING_FAILED_GENERIC;
        }
    }
}

[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.BuyItem))]
public class BazaarBuyItemPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ItemVersionPair listing, ref UniTask<Status<BazaarOperationError>> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay || listing == null)
        {
            return true;
        }
        BazaarOperationError? error = Buy(listing);
        OfflineBazaarMarket.Crumb("BuyItem -> " + (error.HasValue ? error.Value.ToString() : "OK"));
        __result = BazaarReply.Done(error);
        return false;
    }

    private static BazaarOperationError? Buy(ItemVersionPair pair)
    {
        try
        {
            Guid id = Guid.Parse(pair.Id.ToString());
            OfflineListing l = OfflineBazaarMarket.Find(id);
            if (l != null && !l.Mine)
            {
                // Time has passed since you looked: someone else may have snapped it up (underpriced ones go fast).
                OfflineBazaarMarket.SettleSales(l.Stall);
                l = OfflineBazaarMarket.Find(id);
            }
            if (l == null || l.Sold || l.Mine)
            {
                return BazaarOperationError.BUY_ALREADY_SOLD;
            }
            GoldTracker gold = Scripts.Refs_Manager.player_gold_tracker;
            if (gold.IsNullOrDestroyed())
            {
                return BazaarOperationError.LISTING_FAILED_GENERIC;
            }
            if (l.Price > int.MaxValue || !gold.CanAfford((int)l.Price))
            {
                return BazaarOperationError.NOT_ENOUGH_GOLD;
            }
            ItemData item = OfflineBazaarMarket.ToItem(l);
            if (item == null)
            {
                return BazaarOperationError.LISTING_FAILED_GENERIC;
            }
            gold.modifyGold(-(int)l.Price);
            l.Sold = true;
            OfflineBazaarMarket.MarkChanged();
            OfflineBazaarMarket.DeliverToPlayer(item);
            return null;
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("BuyItem failed: " + ex);
            return BazaarOperationError.LISTING_FAILED_GENERIC;
        }
    }
}

[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.UnlistItem))]
public class BazaarUnlistItemPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ItemVersionPair listing, ref UniTask<Status<BazaarOperationError>> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay || listing == null)
        {
            return true;
        }
        BazaarOperationError? error = Unlist(listing);
        OfflineBazaarMarket.Crumb("UnlistItem -> " + (error.HasValue ? error.Value.ToString() : "OK"));
        __result = BazaarReply.Done(error);
        return false;
    }

    private static BazaarOperationError? Unlist(ItemVersionPair pair)
    {
        try
        {
            OfflineListing l = OfflineBazaarMarket.Find(Guid.Parse(pair.Id.ToString()));
            if (l == null || !l.Mine || l.Redeemed)
            {
                return BazaarOperationError.UNLIST_ALREADY_SOLD;
            }
            OfflineBazaarMarket.RedeemableCount(); // settle any due sale first
            if (l.Sold)
            {
                return BazaarOperationError.UNLIST_ALREADY_SOLD;
            }
            ItemData item = OfflineBazaarMarket.ToItem(l);
            if (item == null)
            {
                return BazaarOperationError.LISTING_FAILED_GENERIC;
            }
            OfflineBazaarMarket.Remove(l);
            OfflineBazaarMarket.DeliverToPlayer(item);
            return null;
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("UnlistItem failed: " + ex);
            return BazaarOperationError.LISTING_FAILED_GENERIC;
        }
    }
}

/// <summary>Collect the gold for an item a simulated buyer purchased.</summary>
[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.RedeemListing))]
public class BazaarRedeemListingPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ItemVersionPair listing, ref UniTask<Status<BazaarOperationError>> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay || listing == null)
        {
            return true;
        }
        BazaarOperationError? error = Redeem(listing);
        OfflineBazaarMarket.Crumb("RedeemListing -> " + (error.HasValue ? error.Value.ToString() : "OK"));
        __result = BazaarReply.Done(error);
        return false;
    }

    private static BazaarOperationError? Redeem(ItemVersionPair pair)
    {
        try
        {
            OfflineBazaarMarket.RedeemableCount();
            OfflineListing l = OfflineBazaarMarket.Find(Guid.Parse(pair.Id.ToString()));
            if (l == null || !l.Mine)
            {
                return BazaarOperationError.NOT_SOLD;
            }
            if (l.Redeemed)
            {
                return BazaarOperationError.ALREADY_REDEEMED;
            }
            if (!l.Sold)
            {
                return BazaarOperationError.NOT_SOLD;
            }
            GoldTracker gold = Scripts.Refs_Manager.player_gold_tracker;
            if (gold.IsNullOrDestroyed())
            {
                return BazaarOperationError.LISTING_FAILED_GENERIC;
            }
            long payout = Math.Max(0, l.Price - l.Tax);
            if ((long)gold.value + payout > int.MaxValue)
            {
                return BazaarOperationError.TOO_MUCH_GOLD_TO_REDEEM;
            }
            gold.modifyGold((int)payout);
            OfflineBazaarMarket.Crumb("collected " + payout + " (price " + l.Price + ", tax " + l.Tax + ")");
            l.Redeemed = true;
            OfflineBazaarMarket.MarkChanged();
            return null;
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("RedeemListing failed: " + ex);
            return BazaarOperationError.LISTING_FAILED_GENERIC;
        }
    }
}

/// <summary>Price suggestions come from the live market. Offline there is none, so answer "no data".</summary>
[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.GetItemPrices))]
public class BazaarItemPricesPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref UniTask<Result<Il2CppLE.Data.Responses.GetBazaarItemPricesResponse, BazaarOperationError>> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return true;
        }
        var response = new Il2CppLE.Data.Responses.GetBazaarItemPricesResponse
        {
            Prices = new Il2CppSystem.Collections.Generic.List<BazaarItemPriceDto>(),
        };
        __result = new UniTask<Result<Il2CppLE.Data.Responses.GetBazaarItemPricesResponse, BazaarOperationError>>(
            new Result<Il2CppLE.Data.Responses.GetBazaarItemPricesResponse, BazaarOperationError>(response)
        );
        return false;
    }
}

/// <summary>
/// The sell panel's suggested price. Offline there are no market samples, so it is the mod's own appraisal of the
/// item placed in the slot, and it is put into the asking price box like the game does with a real suggestion.
/// </summary>
[HarmonyPatch(typeof(PriceSuggestionUI), nameof(PriceSuggestionUI.ItemIsSupportedByPriceSuggestions))]
public class BazaarPriceSupportedPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return true;
        }
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(PriceSuggestionUI), nameof(PriceSuggestionUI.UpdatePriceSuggestionUI))]
public class BazaarPriceSuggestionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PriceSuggestionUI __instance, ItemDataUnpacked item)
    {
        if (GameplayEnvironment.IsOnlinePlay || item.IsNullOrDestroyed() || __instance.IsNullOrDestroyed())
        {
            return true;
        }
        try
        {
            ItemData data = item.TryCast<ItemData>();
            if (data == null)
            {
                return true;
            }
            long fair = BazaarPricing.Appraise(data, item.LvlReq, data.corrupted).Fair;
            if (!__instance._priceSuggestionLoader.IsNullOrDestroyed())
            {
                __instance._priceSuggestionLoader.SetActive(false);
            }
            if (!__instance._priceSuggestionValueWrapper.IsNullOrDestroyed())
            {
                __instance._priceSuggestionValueWrapper.SetActive(true);
            }
            if (!__instance._priceSuggestionValue.IsNullOrDestroyed())
            {
                __instance._priceSuggestionValue.text = fair.ToString("N0");
            }
            __instance.PriceSuggested?.Invoke(fair);
            return false;
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("price suggestion failed: " + ex.Message);
            return true;
        }
    }
}

[HarmonyPatch(typeof(BazaarClientService), nameof(BazaarClientService.GetRedeemableCount))]
public class BazaarRedeemableCountPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref UniTask<Result<int, BazaarOperationError>> __result)
    {
        if (GameplayEnvironment.IsOnlinePlay)
        {
            return true;
        }
        __result = new UniTask<Result<int, BazaarOperationError>>(
            new Result<int, BazaarOperationError>(OfflineBazaarMarket.RedeemableCount())
        );
        return false;
    }
}

/// <summary>
/// After a buy, list, unlist or collect the panel does not re-query on its own, so a sold listing would
/// stay on screen. Ask it to reload its current page on the next frame.
/// </summary>
internal static class BazaarRefresh
{
    public static int LastPage = 1;
    private static bool _pending;

    public static void Request() => _pending = true;

    public static void Tick()
    {
        if (!_pending)
        {
            return;
        }
        _pending = false;
        try
        {
            BazaarPanel panel = BazaarPanelOpenOfflinePatch.Panel;
            if (!panel.IsNullOrDestroyed() && !panel.BazaarUI.IsNullOrDestroyed())
            {
                panel.BazaarUI.RequestPage(LastPage);
            }
        }
        catch (Exception ex)
        {
            OfflineBazaarMarket.Crumb("refresh failed: " + ex.Message);
        }
    }
}
