ItemSlot_Result
===============

POOL: both VFX_Pool colors, 16 frames @ 20 fps = 0.8 s. POUR: both VFX_Pour colors, 16 frames @ ~18 fps = 0.9 s. FLASH: both VFX_Flash colors, 8 frames @ 16 fps = 0.5 s. Enable result item icon between pour and flash; keep item icon above the pool but below flash overlays.

UNITY SETUP (Unity UI / uGUI)
1. Copy the whole Recombinator_Unity_VFX_Kit folder into Assets/Recombinator (or copy subfolders).
2. Select all PNG files: Texture Type = Sprite (2D and UI); Sprite Mode = Single; Alpha Is Transparency = ON; Compression = None for best UI quality; Apply.
3. Create a Canvas (Screen Space - Overlay), then add a UI > Image for Base/Dormant.png.
4. Child UI > Image elements must be the same RectTransform width/height as the base sprite and placed above it. Disable Raycast Target on VFX children.
5. Add SpriteSequenceUI to each VFX child. Assign its Image and drag the numbered frame files in numerical order to Frames. Set FPS according to the timeline below.
6. Keep the dormant base visible even when VFX are off. Animations are overlay images, not replacements for the frames.
7. In RecombinatorTimelineUI Inspector, assign the slots, pipe overlay component arrays, result pool/pour/flash overlays, and finalItemImage. Attach it to the panel root. Call PlayRecombination() only after item validation; set the result icon before starting.
8. Base art is a SINGLE image per component and VFX frame files are SINGLE transparent images (not atlas layouts). For scalable frames use a dedicated 9-slice redraw; the supplied ornate borders are not 9-slice-safe.
NOTES: VFX here are prototype sprites, not production-quality shader simulation. Pipe overlays are screen-space streams and may not follow every bend perfectly. For precise piping use Unity UI masking / custom mesh path. This package is render-pipeline-agnostic and uses UnityEngine.UI (no VFX Graph required).
