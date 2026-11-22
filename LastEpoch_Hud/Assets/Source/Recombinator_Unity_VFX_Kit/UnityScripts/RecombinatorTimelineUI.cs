using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Reference animated UI overlays; each is a child Image layered over a dormant base Image.
// This is a visual timeline only; hook crafting/gameplay validation and item creation separately.
public class RecombinatorTimelineUI : MonoBehaviour
{
    public SpriteSequenceUI leftSlot, rightSlot;
    public SpriteSequenceUI[] orangePipes, greenPipes;
    public SpriteSequenceUI poolOrange, poolGreen, pourOrange, pourGreen, flashOrange, flashGreen;
    public Image finalItemImage; // should already have its final item icon assigned by your gameplay logic
    public float slotPowerSeconds = 1.0f;
    public float pipeTravelSeconds = 1.5f;
    public float poolingSeconds = 0.8f;
    public float pouringSeconds = 0.9f;
    public float flashSeconds = 0.5f;
    public float fadeSeconds = 0.65f;
    Coroutine sequence;
    void Awake() { if (finalItemImage) finalItemImage.enabled = false; }
    public void PlayRecombination() { if (sequence != null) StopCoroutine(sequence); StopAllVfx(); sequence = StartCoroutine(Sequence()); }
    IEnumerator Sequence()
    {
        // 0: input 1 powers up, then input 2; swap ordering here for simultaneous activation.
        if (leftSlot) leftSlot.Play(); yield return new WaitForSeconds(slotPowerSeconds);
        if (rightSlot) rightSlot.Play(); yield return new WaitForSeconds(slotPowerSeconds);
        foreach (var p in orangePipes) if (p) { p.loop = true; p.Play(); }
        foreach (var p in greenPipes) if (p) { p.loop = true; p.Play(); }
        yield return new WaitForSeconds(pipeTravelSeconds);
        if (poolOrange) poolOrange.Play(); if (poolGreen) poolGreen.Play();
        yield return new WaitForSeconds(poolingSeconds);
        if (pourOrange) pourOrange.Play(); if (pourGreen) pourGreen.Play();
        yield return new WaitForSeconds(pouringSeconds);
        if (finalItemImage) finalItemImage.enabled = true;
        if (flashOrange) flashOrange.Play(); if (flashGreen) flashGreen.Play();
        yield return new WaitForSeconds(flashSeconds);
        // Stop all conduits after flashes. Their flow layers fade before they are disabled.
        float elapsed = 0f;
        while (elapsed < fadeSeconds) {
            float a = 1f - elapsed / Mathf.Max(0.01f, fadeSeconds);
            foreach (var p in orangePipes) if (p) p.SetAlpha(a);
            foreach (var p in greenPipes) if (p) p.SetAlpha(a);
            elapsed += Time.deltaTime; yield return null;
        }
        StopAllVfx(); sequence = null;
    }
    void StopAllVfx()
    {
        if (leftSlot) leftSlot.Stop(); if (rightSlot) rightSlot.Stop();
        foreach (var p in orangePipes) if (p) p.Stop();
        foreach (var p in greenPipes) if (p) p.Stop();
        if (poolOrange) poolOrange.Stop(); if (poolGreen) poolGreen.Stop();
        if (pourOrange) pourOrange.Stop(); if (pourGreen) pourGreen.Stop();
        if (flashOrange) flashOrange.Stop(); if (flashGreen) flashGreen.Stop();
    }
}
