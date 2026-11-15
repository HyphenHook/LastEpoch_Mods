using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Add to an Image overlay with Raycast Target disabled.
// Drag the ordered sprites into Frames in the Inspector.
public class SpriteSequenceUI : MonoBehaviour
{
    public Image target;
    public Sprite[] frames;
    [Min(1)] public float fps = 16f;
    public bool loop;
    public bool playOnEnable;
    Coroutine current;
    void Awake() { if (!target) target = GetComponent<Image>(); if (target) target.enabled = false; }
    void OnEnable() { if (playOnEnable) Play(); }
    public void Play() { Stop(); if (isActiveAndEnabled) current = StartCoroutine(Run()); }
    public void Stop() { if (current != null) StopCoroutine(current); current = null; if (target) target.enabled = false; }
    public void SetAlpha(float value) { if (target) { Color c = target.color; c.a = Mathf.Clamp01(value); target.color = c; } }
    IEnumerator Run()
    {
        if (!target || frames == null || frames.Length == 0) yield break;
        target.enabled = true; SetAlpha(1f);
        do {
            for (int i = 0; i < frames.Length; i++) {
                target.sprite = frames[i];
                yield return new WaitForSeconds(1f / fps);
            }
        } while (loop);
        if (target) target.enabled = false;
        current = null;
    }
}
