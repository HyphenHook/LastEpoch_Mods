using System;
using System.Collections.Generic;
using System.Linq;
using LastEpoch_Hud.Scripts.Core.Crafting;
using Il2Cpp;
using Il2CppTMPro;
using LastEpoch_Hud.Scripts.ModUI.Shell.Host;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LastEpoch_Hud.Scripts.Mods.Items.Crafting;

internal static partial class Items_AdvancedForgeWindow
{
    private static void ReturnItem()
    {
        GiveBack(item);
        item = null;
        ResetEchoView();
        ResetMemoryView();
    }

    private static void GiveBack(ItemData given)
    {
        ItemContainersManager manager = ItemContainersManager.Instance;
        if (given.IsNullOrDestroyed() || manager.IsNullOrDestroyed())
        {
            return;
        }
        if (!manager.inventory.TryAddItem(given, 1, Context.DEFAULT))
        {
            manager.cursor.TryAddItem(given, 1, Context.DEFAULT);
        }
    }

    private static void ClickSlot()
    {
        if (animating || MemoryClosing)
        {
            return;
        }
        try
        {
            ItemContainersManager manager = ItemContainersManager.Instance;
            if (manager.IsNullOrDestroyed())
            {
                return;
            }
            if (item != null)
            {
                GiveBack(item);
                item = null;
                ResetEchoView();
                ResetMemoryView();
                icon?.Dispose();
                icon = null;
            }
            else
            {
                var content = manager.cursor.GetContent();
                if (content != null && content.Count > 0)
                {
                    ItemContainerEntry entry = content[0];
                    ItemData taken = entry.data;
                    if (!taken.IsNullOrDestroyed() && manager.cursor.TryRemoveItem(entry, 1, Context.DEFAULT))
                    {
                        item = taken;
                    }
                }
            }
            status = "";
            dirty = true;
        }
        catch (Exception ex)
        {
            Main.logger_instance?.Warning("Advanced Forge slot failed: " + ex.Message);
        }
    }

    private static void Impact(float now)
    {
        if (pendingKey == AdvancedMaterials.AttunementOrb)
        {
            bool attuned = Items_Attunement.UseOn(item, out double multiplier, out status);
            statusColour = attuned ? Good : Bad;
            if (attuned) StartAttunementOutcome(now, multiplier >= 1);
            if (AdvancedMaterials.Count(AdvancedMaterials.AttunementOrb) == 0) selectedKey = null;
            dirty = false;
            return;
        }
        if (pendingKey == AdvancedMaterials.EchoOrb)
        {
            EchoClicked(atImpact: true);
            if (shownEchoes != null)
            {
                PlayEchoReveal(now);
            }
            dirty = shownEchoes != null;
            return;
        }
        if (pendingKey == AdvancedMaterials.ReverieOrb)
        {
            bool rerolled = Items_Reminiscence.RerollMemory(item, out status);
            statusColour = rerolled ? Good : Bad;
            if (rerolled) status = "";
            if (rerolled)
            {
                chosenMemory = null;
                revealRerolledMemories = unveiledItemId == item.individualID;
                StartReverieScatter();
                if (AdvancedMaterials.Count(AdvancedMaterials.ReverieOrb) == 0) selectedKey = null;
            }
            dirty = false;
            return;
        }

        bool ok = false;
        string message = "";
        if (pendingKey == AdvancedMaterials.EncapsulationOrb)
        {
            ok = Items_Encapsulation.UseOn(item, out message);
        }
        else if (pendingKey == AdvancedMaterials.ReminiscenceOrb)
        {
            ok = Items_Reminiscence.UseOn(item, out message);
        }
        else if (pendingKey == AdvancedMaterials.ChaosOrb)
        {
            ok = Items_ChaosReroll.UseOn(item, out message);
        }
        status = ok ? "" : message;
        statusColour = ok ? Good : Bad;
        if (selectedKey != null && AdvancedMaterials.Count(selectedKey) < 1)
        {
            selectedKey = null; // that was the last one
        }
        if (ok) StartOrbSeal(pendingKey);
        dirty = false; // the window is rebuilt when the effect is done, so the item is not redrawn in the middle of it
        if (pendingKey == AdvancedMaterials.EncapsulationOrb) return;
        if (ok && pendingKey == AdvancedMaterials.ReminiscenceOrb)
        {
            ShowObscuredSand(now, fadeIn: true);
            return;
        }
        Color a = OrbTone(pendingKey);
        Color b = !ok ? new Color(1f, 0.55f, 0.4f) : new Color(1f, 0.78f, 0.36f);
        Vector2 at = SlotCentre;
        for (int i = 0; i < (pendingKey == AdvancedMaterials.ChaosOrb ? 34 : 20); i++)
        {
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float speed = UnityEngine.Random.Range(140f, 520f);
            AddSpark(at, Color.Lerp(a, b, UnityEngine.Random.value), UnityEngine.Random.Range(3f, 7f),
                new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.25f) * speed, UnityEngine.Random.Range(0.35f, 0.9f));
        }
    }
    private static void ForgeClicked()
    {
        if (animating || MemoryClosing)
        {
            return;
        }
        ObscuredMemory memory = Items_Reminiscence.ObscuredOf(item);
        if (memory != null)
        {
            if (selectedKey == AdvancedMaterials.ReverieOrb)
            {
                if (Items_Reminiscence.ReverieRestriction(item).Length != 0
                    || AdvancedMaterials.Count(AdvancedMaterials.ReverieOrb) < 1)
                {
                    return;
                }
                StartForgeFx(AdvancedMaterials.ReverieOrb);
                return;
            }
            else if (unveiledItemId != item.individualID)
            {
                if (Items_Reminiscence.RevealMemory(item, out status))
                {
                    unveiledItemId = item.individualID;
                    chosenMemory = null;
                    memoryRevealAt = Time.unscaledTime;
                }
                else
                {
                    statusColour = Bad;
                }
            }
            else if (chosenMemory != null)
            {
                bool kept = Items_Reminiscence.Choose(item, memory, chosenMemory, out status);
                statusColour = kept ? Good : Bad;
                if (kept) status = "";
                if (kept)
                {
                    ResetMemoryView();
                    StartMemoryMist();
                }
            }
            dirty = true;
            return;
        }
        string why = OrbRestriction(selectedKey);
        if (why.Length > 0)
        {
            status = why;
            statusColour = Bad;
            dirty = true;
            return;
        }
        if (selectedKey == AdvancedMaterials.EchoOrb)
        {
            EchoClicked();
            return;
        }
        StartForgeFx(selectedKey);
    }
}
