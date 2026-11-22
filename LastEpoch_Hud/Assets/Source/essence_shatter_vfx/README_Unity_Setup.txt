ADVANCED FORGE — ESSENCE SHATTER VFX
===================================

WHAT THIS PACKAGE IS
40 transparent PNG overlays designed to match the tall, purple-gold Last Epoch-inspired forging slot.
The slot frame and item image are NOT baked into the effects. Keep your existing UI frame and item underneath this overlay.
Every PNG has exactly 512 x 768 pixels, matching the 2:3 vertical slot aspect ratio, and is centered/aligned on the same coordinates.
Do not trim each PNG independently after import or the effect will jump.

PNG / ORB DIMENSIONS (IMPORTANT)
--------------------------------
Texture size per VFX frame: 512 x 768 px.
Original vertical slot reference: 1024 x 1536 px (2:3 aspect ratio).
Orb center in every VFX PNG: (256, 335) px, measured from top-left.
That is 50% across and 43.6% down the slot.
Item essence-absorption target: (256, 505) px; 50% across, 65.8% down.
Reference intact orb diameter INSIDE the VFX: approximately 240 px on the 512 px-wide frame (about 47% of slot width).
If using the original 1024 x 1536 slot artwork, use an orb image about 480 x 480 px with its center around (512, 670) px.
If using a 512 x 768 slot, use a 240 x 240 px orb image centered (256, 335).
If the slot displays at 300 x 450 screen pixels, the intact orb should appear about 141 x 141 screen pixels.
During shatter the effect radius grows substantially beyond the intact orb; keep the overlay full-slot-size and unclipped.

UNITY UI LAYER ORDER
--------------------
1. Forging slot background (your existing vertical rectangular frame).
2. Item icon in slot, with its pivot/target aligned to the absorption position.
3. Orb icon, with its center aligned to the orb center (visible until shatter).
4. VFX overlay: a full-size RawImage/Image/animated sprite at the slot RectTransform size.
5. Optional highlight, rune ring, bloom, and sparks.

Once the shatter begins (frame 9), fade and then hide the separate static orb sprite.
Keep the item visible throughout so shards visibly travel INTO the item rather than into empty space.
A tiny white-purple flash at the item in frames 22-27 sells the merge.

FRAME TIMING
------------
Frames 01–08: CHARGE — 75 ms each = 0.600 s. Show orb holding power.
Frames 09–16: SHATTER — 60 ms each = 0.480 s. The essence bursts into fragments.
Frames 17–28: ABSORB — 70 ms each = 0.840 s. Violet trails funnel down into item.
Frames 29–40: RESIDUAL — 80 ms each = 0.960 s. Small essence wisps fly out and fade.
TOTAL = 2.880 s, played ONCE (not looped).

UNITY IMPORT SETTINGS
---------------------
Texture Type: Sprite (2D and UI).
Sprite Mode: Single for separate PNGs, or Multiple for atlas (8 columns x 5 rows).
Alpha Is Transparency: Enabled.
Filter Mode: Bilinear (Point only if pixel-art desired).
Wrap Mode: Clamp.
Compression: None or High Quality (to avoid glow banding).
Mip Maps: Off for normal UI.
Pivot: Center (0.5,0.5) on ALL frames.
Preserve Aspect: True and use a consistent 2:3 VFX RectTransform.
Do not auto-trim sprites per frame; keep full identical transparent rects.

UNITY PLAYBACK
--------------
Use Unity UI Image.sprite (swap sprite from a coroutine / timeline).
WaitForSecondsRealtime(duration_ms / 1000f) so the sequence plays at consistent speed in UI.
If using Animator, use separate Clips per stage or an AnimationEvent with adjustable speed; the provided timing is not uniform FPS.
To match a 1024x1536 forge panel, set VFX overlay RectTransform to fill the panel, with anchors center and pivot center.
Place the visible orb at normalized position (0.50, 0.436) from top-left of the overlay, and absorption target (0.50, 0.658).
Do not move the sprite container as frames advance; these positions are built into every PNG.

RECOMMENDED OPTIONAL UNITY VFX
------------------------------
1) Charge: dark purple radial light behind the orb, pulsing 0.85x–1.15x, frames 01–08.
2) Shatter: one 0.10–0.16 sec bloom pulse on frames 10–12. Add a soft metal/arcane crack SFX.
3) Shatter impact: 2–3 px UI shake for 0.10 sec; avoid moving the slot permanently.
4) Absorption: 5–10 tiny additive purple particles moving from orb to item. Fade them as they approach item, frames 17–28.
5) Item merge: brief white-purple item highlight or material emission boost, peaking frames 23–26.
6) Residual: no giant shockwave here. Use 6–10 faint particle sparks escaping in different directions (0.5–0.9 sec lifetimes), frames 29–40.
7) Optional tiny camera/GUI chromatic offset at the shatter moment, under 100 ms.

BLEND MODES
-----------
The included art uses normal RGBA alpha, so ordinary UI Image works.
If you want stronger magic, duplicate the VFX Image above itself, tint purple, and use an additive UI shader for the duplicate at 20–40% opacity.
Do not use additive blend for the ONLY layer or you may lose the dark purple core definition.

OTHER NOTES
-----------
- PNGs are custom procedural effects based on the requested appearance; they are effect-only and do not reproduce the forge UI frame.
- The orb reference diameter is the important number for matching your static orb art.
- If your item icon is placed at the CENTER of the slot rather than below the orb, shift the complete VFX overlay upward/downward OR adjust the absorption target in a shader/new variant.
- For a sharper image on a 1024px-wide slot, these 512px textures are upscaled 2x. Consider higher resolution if displayed very large.
