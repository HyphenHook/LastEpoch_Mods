RECOMBINATOR — UNITY UI VFX KIT  |  VERSION 2 / PROTOTYPE
=======================================================
DESIGN
Two items are inserted: LEFT ember-red, RIGHT verdant-green. Soul essence travels through otherwise GREY/DORMANT conduits to the ornate result frame. Two essences pool, pour down into a result item, flash, and fade. The item-recombination logic is intentionally not included.

CONTENT
10 independent component folders: 3 frame folders, 7 pipe folders.
Every Base/Dormant.png is ONE standalone PNG. Each component has its own README.
Sequence frames are individual transparent PNGs, named frame_000.png ... in separate VFX subfolders.
UnityScripts/SpriteSequenceUI.cs and RecombinatorTimelineUI.cs provide simple plug-in UI animations and orchestration.

RECOMMENDED TIMELINE at 16–20 FPS
0.00–1.00 left slot powers up (16 @16 FPS)
1.00–2.00 right slot powers up (16 @16 FPS)
2.00–3.50 orange and green pipe flows loop (12 @16 FPS)
3.50–4.30 essence pools in final slot (16 @20 FPS)
4.30–5.20 essence pours downward (16 @~18 FPS)
5.20 reveal created item + orange/green flash (8 @16 FPS)
5.70–6.35 pipe energy fades to clear; gray pipe remains

HOW TO PLAY
Create uGUI Images on Canvas, Base/Dormant.png behind animated sequence Image children (matching sprite native sizes), add SpriteSequenceUI to overlays, assign frames numerically. Attach RecombinatorTimelineUI to panel root and wire all fields. For each side assign the appropriate pipe overlay array. For color-mixed connector assign one overlay to each array. Call PlayRecombination() from your craft button handler after inventory validation. The controller is cosmetic only; do not consume items from it.

IMPORT
Texture Type Sprite (2D and UI), Sprite Mode Single, Alpha Is Transparency checked, Filter Mode Bilinear, Max Size 1024, Compression None or High Quality. CanvasScaler Scale With Screen Size, reference resolution 1920x1080. Preserve aspect ratios. This uses UnityEngine.UI and standard coroutines; compatible with Built-in / URP / HDRP.

LIMITATIONS / QA
Source sprites are small concept extracted frames. Gray conduit originals have been neutralized (desaturated); faint luminance remnants can persist but NO colored flowing animation exists in Base/Dormant.png. Glow is on separate VFX overlays only. Flow particles are not topologically constrained to bends; for exact production implementation replace with UV-scrolling masked shader streams. The two result flash layers are independent; position/order them so they don't fully cancel visually. Item result artwork and crafting business rules supplied by your game.

Folder guide: Each component includes README.txt. Code uses FPS and durations exposed in Inspector; adjust to suit animation pacing.
