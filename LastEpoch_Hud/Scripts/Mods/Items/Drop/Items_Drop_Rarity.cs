using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.Core.Items;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.Drop;

public class Items_Drop_Rarity
{
    private static bool warnedUnsafeLegendary;

    public static float R = 1f;
    public static float G = 0.416666f;
    public static float B = 0f;
    public static float A = 1f;
    public static Color color;
    public static Texture2D texture;
    public static Sprite sprite;

    public static bool CanRun()
    {
        bool res = false;
        if ((Hud_Manager.IsPauseOpen()) && (ModUI.ForceDrop.ForceDropModel.Enabled))
        {
            return false;
        }
        else if ((Scenes.IsGameScene()) && (!Save_Manager.instance.IsNullOrDestroyed()))
        {
            if (!Save_Manager.instance.data.IsNullOrDestroyed())
            {
                res = true;
            }
        }

        return res;
    }

    public static void Update_ColorAndTexture()
    {
        color = new Color(R, G, B, A);
        if (texture.IsNullOrDestroyed())
        {
            texture = Create_Texture(color);
        }
        if (!texture.IsNullOrDestroyed())
        {
            if (sprite.IsNullOrDestroyed())
            {
                sprite = Create_Sprite(texture);
            }
            if (sprite.IsNullOrDestroyed())
            {
                Main.logger_instance?.Error("Sprite create Fail");
            }
        }
        else
        {
            Main.logger_instance?.Error("Can't create sprite, texture is null");
        }
    }

    public static Texture2D Create_Texture(Color color)
    {
        Texture2D texture2d = new Texture2D(2, 2);
        Color[] pixels = new Color[2 * 2];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        texture2d.SetPixels(pixels);
        texture2d.Apply();

        return texture2d;
    }

    public static Sprite Create_Sprite(Texture2D texture2d)
    {
        Sprite sprite = Sprite.Create(
            new Rect(0.0f, 0.0f, texture2d.width, texture2d.height),
            new Vector2(texture2d.width / 2, texture2d.height / 2),
            100.0f,
            texture2d
        );

        return sprite;
    }

    public static bool IsNewRarity(ItemDataUnpacked item)
    {
        if (!item.IsNullOrDestroyed())
        {
            if (item.affixes.Count > 4)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    //Unlock rarity for 5 and 6 affixs
    [HarmonyPatch(typeof(GenerateItems), "RollRarity")]
    public class GenerateItems_RollRarity
    {
        [HarmonyPrefix]
        static bool Prefix(ref byte __result, int __0)
        {
            if (CanRun())
            {
                if (Save_Manager.instance.data.Items.Drop.Enable_ForceUnique)
                {
                    __result = 7;
                    return false;
                }
                else if (Save_Manager.instance.data.Items.Drop.Enable_ForceSet)
                {
                    __result = 8;
                    return false;
                }
                else if (Save_Manager.instance.data.Items.Drop.Enable_ForceLegendary)
                {
                    // Rarity 9 is not a valid legendary conversion by itself.
                    // It leaves the game's generated base with an unrelated
                    // unique ID (commonly Calamity). Never persist these drops.
                    Save_Manager.instance.data.Items.Drop.Enable_ForceLegendary = false;
                    if (!warnedUnsafeLegendary)
                    {
                        warnedUnsafeLegendary = true;
                        Main.logger_instance?.Warning(
                            "Force Legendary is temporarily disabled: the old rarity-9 override creates malformed items. Use Force Drop's explicit item construction instead."
                        );
                    }
                    return true;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        [HarmonyPostfix]
        static void Postfix(ref byte __result, int __0)
        {
            if (CanRun())
            {
                if ((__result < 7) && (Save_Manager.instance.data.Items.Drop.Enable_AffixCount))
                {
                    int min = DropAffixCount.ToWhole(
                        Save_Manager.instance.data.Items.Drop.AffixCount_Min
                    );
                    int max = DropAffixCount.ToWhole(
                        Save_Manager.instance.data.Items.Drop.AffixCount_Max
                    );
                    if ((min != 0) || (max != 4))
                    {
                        if (min < 0)
                        {
                            min = 0;
                            Save_Manager.instance.data.Items.Drop.AffixCount_Min = 0f;
                        }
                        else if (min > AffixLimits.MaxTotal)
                        {
                            min = AffixLimits.MaxTotal;
                            Save_Manager.instance.data.Items.Drop.AffixCount_Min =
                                AffixLimits.MaxTotal;
                        }
                        if (max < 0)
                        {
                            max = 0;
                            Save_Manager.instance.data.Items.Drop.AffixCount_Max = 0f;
                        }
                        else if (max > AffixLimits.MaxTotal)
                        {
                            max = AffixLimits.MaxTotal;
                            Save_Manager.instance.data.Items.Drop.AffixCount_Max =
                                AffixLimits.MaxTotal;
                        }

                        // The rarity byte holds the affix count of a normal drop, and 7 and
                        // above mean unique, so the game rolls at most 6 affixes by itself.
                        // The total limit (AffixLimits.MaxTotal) is reached through Force Drop.
                        int count =
                            min == max ? min : UnityEngine.Random.RandomRangeInt(min, (max + 1));
                        __result = AffixLimits.RarityForOrdinaryCount(count);
                    }
                }
            }
        }
    }

    //New rarity on Tooltip
    private static bool UpdateTooltip = false;
    private static bool ItemBackground_Done = false;
    private static bool Ornamentation_Done = false;

    //[HarmonyPatch(typeof(TooltipItemManager), "OpenTooltip", new System.Type[] { typeof(ItemDataUnpacked), typeof(TooltipItemManager.SlotType), typeof(Vector2), typeof(Vector3), typeof(GameObject) })]
    [HarmonyPatch(
        typeof(TooltipItemManager),
        "OpenTooltip",
        new System.Type[]
        {
            typeof(ItemDataUnpacked),
            typeof(TooltipItemManager.SlotType),
            typeof(Vector2),
            typeof(Vector3),
            typeof(GameObject),
            typeof(Vector2),
        }
    )]
    public class TooltipItemManager_OpenTooltip
    {
        [HarmonyPostfix]
        static void Postfix(ref TooltipItemManager __instance)
        {
            UpdateTooltip = false;
            if (Scenes.IsGameScene())
            {
                if (!__instance.activeParameters.IsNullOrDestroyed())
                {
                    if (IsNewRarity(__instance.activeParameters.Item))
                    {
                        Update_ColorAndTexture();
                        UpdateTooltip = true;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(TooltipItemManager), "CloseTooltip")]
    public class TooltipItemManager_CloseTooltip
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            UpdateTooltip = false;
            ItemBackground_Done = false;
            Ornamentation_Done = false;
        }
    }

    [HarmonyPatch(typeof(UITooltipItem), "OnUpdateTick")]
    public class UITooltipItem_OnUpdateTick
    {
        [HarmonyPostfix]
        static void Postfix(ref UITooltipItem __instance)
        {
            if ((Scenes.IsGameScene()) && (UpdateTooltip))
            {
                if (!__instance.itemBackground.IsNullOrDestroyed())
                {
                    if (__instance.itemBackground.color != color)
                    {
                        __instance.itemBackground.color = color;
                    }
                    if (__instance.itemBackground.color == color)
                    {
                        ItemBackground_Done = true;
                    }
                }

                if (!__instance.ornamentation.IsNullOrDestroyed())
                {
                    if (__instance.ornamentation.color != color)
                    {
                        __instance.ornamentation.color = color;
                    }
                    if (__instance.ornamentation.color == color)
                    {
                        Ornamentation_Done = true;
                    }
                }

                if ((ItemBackground_Done) && (Ornamentation_Done))
                {
                    UpdateTooltip = false;
                }
            }
        }
    }

    //New rarity on Ground
    [HarmonyPatch(
        typeof(GroundItemLabel),
        "initialise",
        new System.Type[] { typeof(GroundItemVisuals) }
    )]
    public class GroundItemLabel_initialise
    {
        [HarmonyPostfix]
        static void Postfix(ref GroundItemLabel __instance, ref GroundItemVisuals __0)
        {
            if (IsNewRarity(__0.itemData))
            {
                Update_ColorAndTexture();

                //Text Color
                TextMeshProUGUI text = __instance.itemText;
                text.faceColor = color; //Text color

                //Border
                GameObject image_obj = Functions.GetChild(__instance.gameObject, "Image");
                if (!image_obj.IsNullOrDestroyed())
                {
                    image_obj.active = true;
                }
            }
        }
    }
}
