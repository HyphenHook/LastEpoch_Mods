using System;
using System.Collections.Generic;
using Il2Cpp;
using LastEpoch_Hud.Scripts.Core.CustomItems.Headhunter.Buffs;
using UnityEngine;

namespace LastEpoch_Hud.Scripts.Mods.Items.CustomItems.Headhunter.Buffs;

/// <summary>Captures the player renderers' look and puts it back.</summary>
internal sealed class HeadhunterRendererSnapshot
{
    private readonly List<Renderer> _renderers = new();
    private readonly List<MaterialPropertyBlock> _blocks = new();
    private readonly List<Material[]> _materials = new();
    private readonly List<Material[]> _applied = new();
    private RendererManager _manager;
    private MaterialPropertyBlock _rarityBlock;
    private MaterialPropertyBlock _tintedBlock;

    public int Count => _renderers.Count;
    public bool RarityWasNull { get; private set; }
    public bool RarityEmptyBefore { get; private set; }
    public bool RarityChangedInPlace { get; private set; }

    /// <summary>Captures the look before a tint.</summary>
    public static HeadhunterRendererSnapshot Capture(RendererManager manager)
    {
        var snapshot = new HeadhunterRendererSnapshot { _manager = manager };
        snapshot._rarityBlock = manager.monsterRarityPropertyBlock;
        snapshot.RarityWasNull = snapshot._rarityBlock == null;
        snapshot.RarityEmptyBefore = !snapshot.RarityWasNull && snapshot._rarityBlock.isEmpty;
        foreach (Renderer renderer in manager.renderers)
        {
            snapshot.Add(renderer);
        }

        return snapshot;
    }

    /// <summary>Records what the tint left behind, so Restore never overwrites later game changes.</summary>
    public void MarkApplied()
    {
        _applied.Clear();
        foreach (Renderer renderer in _renderers)
        {
            _applied.Add(renderer.IsNullOrDestroyed() ? null : renderer.sharedMaterials);
        }

        _tintedBlock = _manager.IsNullOrDestroyed() ? null : _manager.monsterRarityPropertyBlock;
        RarityChangedInPlace = HeadhunterRendererMatch.ChangedInPlace(
            IdOf(_rarityBlock),
            IdOf(_tintedBlock)
        );
    }

    public void Restore()
    {
        RestoreRarityBlock();
        for (int i = 0; i < _renderers.Count; i++)
        {
            RestoreRenderer(i);
        }

        ResetAddedRenderers();
    }

    /// <summary>Renderers the game added since the capture carry our tint; reset them to the baseline.</summary>
    private void ResetAddedRenderers()
    {
        if (_manager.IsNullOrDestroyed())
        {
            return;
        }

        IntPtr manager = _manager.Pointer;
        var known = new List<IntPtr>(_renderers.Count);
        foreach (Renderer renderer in _renderers)
        {
            known.Add(IdOf(renderer));
        }

        foreach (Renderer renderer in _manager.renderers)
        {
            if (renderer.IsNullOrDestroyed())
            {
                continue;
            }

            if (HeadhunterRendererMatch.IsFresh(manager, manager, known, renderer.Pointer))
            {
                renderer.SetPropertyBlock(_manager.monsterRarityPropertyBlock);
            }
        }
    }

    private static IntPtr IdOf(MaterialPropertyBlock block)
    {
        return block == null ? IntPtr.Zero : block.Pointer;
    }

    private static IntPtr IdOf(Renderer renderer)
    {
        return renderer.IsNullOrDestroyed() ? IntPtr.Zero : renderer.Pointer;
    }

    private static IntPtr IdOf(Material material)
    {
        return material == null ? IntPtr.Zero : material.Pointer;
    }

    private static IntPtr[] IdsOf(Material[] materials)
    {
        if (materials == null)
        {
            return null;
        }

        var ids = new IntPtr[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            ids[i] = IdOf(materials[i]);
        }

        return ids;
    }

    private void RestoreRarityBlock()
    {
        if (_manager.IsNullOrDestroyed())
        {
            return;
        }

        MaterialPropertyBlock current = _manager.monsterRarityPropertyBlock;
        HeadhunterRarityRestore step = HeadhunterRendererMatch.RarityRestore(
            IdOf(_rarityBlock),
            IdOf(_tintedBlock),
            IdOf(current)
        );
        if (step == HeadhunterRarityRestore.Clear)
        {
            current.Clear();
            return;
        }

        if (step == HeadhunterRarityRestore.PutBack)
        {
            _manager.monsterRarityPropertyBlock = _rarityBlock;
        }
    }

    private void RestoreRenderer(int index)
    {
        Renderer renderer = _renderers[index];
        if (renderer.IsNullOrDestroyed())
        {
            return;
        }

        renderer.SetPropertyBlock(_blocks[index]);
        Material[] expected = index < _applied.Count ? _applied[index] : _materials[index];
        if (HeadhunterRendererMatch.SameIds(IdsOf(renderer.sharedMaterials), IdsOf(expected)))
        {
            renderer.sharedMaterials = _materials[index];
        }
    }

    private void Add(Renderer renderer)
    {
        if (renderer.IsNullOrDestroyed())
        {
            return;
        }

        _renderers.Add(renderer);
        _blocks.Add(CopyBlock(renderer));
        _materials.Add(renderer.sharedMaterials);
    }

    private static MaterialPropertyBlock CopyBlock(Renderer renderer)
    {
        if (!renderer.HasPropertyBlock())
        {
            return null;
        }

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        return block;
    }
}
