# Splitting flat art into animatable layers (quality rules + gates)

Use when the user gives ONE flat image / sprite sheet and wants per-part motion. Working reference implementations in the package:
`Editor/CapySampleGenerator.cs` (Texture2D pipeline: bg flood fill, colour-family masks, soft matte, hole fill, grown layers, pivots, recomposite, scene + animation) and `Editor/AnimGapCheck.cs` (the gap QA). The Mermaid fin/face-patch cut is in `examples/mermaid-from-spritesheet.md`.

## 0. Decide before you cut
- List exactly which parts must move. **Cut only parts that have a real boundary** — an outline or a clear colour change.
- A part with no boundary (an ear the same flat brown as the head) cannot be cut cleanly: every cut edge shows as a smear. **Leave it static** and get liveliness elsewhere. (Capy: right ear removed from the plan after it smeared.)
- Whole-object motion (squash, bounce, sway, breathing) has **no seams at all** and is free. Put most of the liveliness there; use cut parts for small, high-value motion (chewing, bite, wing, tail, blink).

## 1. Pipeline (all in code, `Texture2D.LoadImage` → `GetPixels32`; rows are bottom-up in Unity, use top-down arrays internally)
1. **Background**: flood fill from the borders (tolerance ~22). Shadow/ground = flood fill from a seed over its own colour, kept as a separate layer.
2. **Masks per part** from HSV colour families inside hand-picked rectangles (read coordinates from a 4× grid image with labelled ticks — draw digits yourself). Never rely on one threshold: fur family `IsBelly` (h .035–.115, s .35–.85, v .58–.92) vs everything else inside a box is more robust than "dark brown".
3. **Soft matte** for soft boundaries (snout vs head): alpha = projection of the pixel onto the line between the two colours, un-blend the colour, base keeps the plain colour → static composite is exact.
4. **Review the masks**: tint each mask over the image and LOOK at it. Then export every layer on a magenta background in one sheet and LOOK again (holes, junk fragments, streaks, wrong outline pieces).
5. **Base**: original minus all cut parts, holes filled (see §2), soft edges against background (un-blend: `a = clamp(dist(c,bg)/dist(outline,bg))`).
6. **Layers**: crop to bbox (+1px), **pivot at the joint** (pivot is in pixel-EDGE coordinates; Unity pivot y counts from the bottom). Position = `(px - frameCentreX)/100, (frameH - py)/100` relative to the base pivot.
7. Import: Sprite, PPU 100, bilinear, `alphaIsTransparency`, custom pivot, no mipmaps. Sorting: parts behind a neighbour (fin, ear flap) get **extension under the neighbour**; parts in front (hands, feet, melon) rely on the filled base.
8. Hierarchy: what is held moves with the holder (hands = child of the watermelon), so they cannot slide apart.

## 2. Filling the holes the cut leaves in the base (this is where gaps come from)
- Sources = **strict fur colour only** (never leftover rind/outline pixels — they contaminated the fill with green/red).
- Inside the body: interpolate along the **row AND the column** between the nearest fur pixels and average them. Row-only gave horizontal streaks, 3×3 averaging gave diagonal chevrons.
- A fill region (ellipses/polygon covering head + body + the chest area the arms hide) tells inside-body from outside. Where hidden body outline should exist, **synthesize a 2px outline** on filled pixels that touch transparency.
- Holes OUTSIDE the body (foot, hand poking out): do NOT leave them transparent — a rotating foot exposes a crack between foot and leg. Extend the nearest solid pixel's colour into the hole (multi-source BFS, ≤14px) but **keep a 2px ring next to the background transparent** so soft layer edges don't turn into a dirty fringe at rest.

## 3. Overlap — the rule that actually removes gaps
A cut layer only contains the pixels that were VISIBLE in the flat image. Move it 2px and whatever it used to hide, or whatever it was attached to, opens up. So **every moving layer needs content beyond its visible mask**:
- **Overlap ring** (`LayerFromMask(mask, ringR=3, exclude)`): add a ring of ≥ (max displacement + 1) px made of the ORIGINAL pixels around the mask (inside the body only — never into the background, never pixels of a layer that moves differently). At rest the ring is identical to the base underneath, so the picture does not change and bilinear edge seams vanish; in motion the ring covers attachment points (arm→body, ankle→leg) instead of a crack. (Growing 1px with the layer's own edge colours thickened outlines and still opened gaps at joints — do not use it.)
- **Extend under occluders**: a layer partly hidden by another needs the hidden part invented and drawn *under* the occluder. Capy: the snout continues ~10px down under the watermelon (otherwise a skin-coloured strip shows between snout and melon when the melon lifts).
- **Ground / shadow must be a complete shape**: the part of the shadow under the body is covered in the flat image, so the shadow layer is missing there. When the body bounces, a thin background-coloured line opens under the belly. Fill the whole shadow ellipse (only on pixels that were body, not on background) and draw it behind everything.
- **Hand/held objects ride with the holder**: make the held part a child of the holder (hands under the watermelon) so they cannot slide apart.
- Static recomposite equals the original by construction; it proves nothing about motion. Always test the moved pose.

## 3b. What goes WITH a part (the "chipped / lost outline / junk flies along" bugs)
A moving layer must take its WHOLE look with it — every ring, outline, nose line. Whatever you leave in the base stands still and looks chipped; whatever extra you grab from a neighbour rides along as junk. Both happened on Capy:
- **Eyes lost their outline**: the ring around the two dark "eyes" on the snout was left in the base because only *dark* pixels were accepted, so the ring stayed put while the eyes moved. → inside the real nose area accept **every pixel darker than the head colour**.
- **A peach fleck flew with the snout**: a "differs from head colour" test also accepts pixels *lighter* than the head (the pale rim of the fur). → add the direction: darker only.
- **A curved outline stroke flew with the snout**: the head's dark outline lies on the *extension* of the head→snout line in colour space, so a soft-matte projection happily gave it alpha 1. → bound the projection (`t < 1.2`) and keep dark pixels only inside a hand-drawn region of the real feature.
- **A dark triangle flew with the heart bubble**: the cut rectangle was bigger than the bubble and caught a corner of the head outline. → cut by SHAPE (circle + small tail), reject dark/brown pixels, keep only the connected piece around the seed (`KeepConnected`, bridge only through alpha ≥ 60, 4-neighbour).
- **Faint specks**: tiny alpha noise from the background colour became haze/dots around layers. → alpha threshold ≥ 6%, `Despeckle` (drop pieces < 8 px), connected-piece filter.
- **Never rectangle-cut near another part's outline**; every pixel rule needs a region AND a colour test AND a direction (darker/lighter).
- **Fill sources**: only fur or dark-outline pixels; any other colour (pale rind) leaks a grey/pink block that shows when the part moves.

## 4. Motion budget for cut parts
- Displacement at a junction ≲ 1.5–2 source px (≈1.5% of the part), and the overlap ring must be at least that + 1. Capy: watermelon lift 1.2–2.3 px, snout 1.2–1.6 px, feet ±3° (Eat) / ±6° (Happy), ring 3 px. Feet at ±12° opened a visible gap before the ring existed.
- Rotate about the joint; translation of parts that are visually attached is the usual culprit.
- Whole-body bob/squash is free (no seams): keep the liveliness there.

## 5. Quality gates — all required before reporting success
1. Mask overlay reviewed. 2. Layer sheet on magenta reviewed. 3. Static recomposite mean abs diff ≲ 1 and visually identical (render at 4 px per texel into a RenderTexture and diff).
4. **`AnimGapCheck.Run(root, ignoreNames, outDir)` → every state PASS** (new cracks+holes < 0.15% of the silhouette). Run it TWICE: once on the body alone, once **including the ground/shadow layer** — ignoring the shadow hid the gap under the belly and the metric said PASS while the user still saw a gap. Only ignore things that truly float free (a speech bubble). Whole-body motion is not counted.
5. **Look at `motion_<state>_rest_vs_worst.png`** (rest | worst frame on magenta; a gap shows as magenta inside the body) and at 6×-zoomed crops of every joint at the peak pose (snout/melon, each hand, each foot). A PASS number never replaces this. If the user says "still gaps", the metric missed something — find it by eye (and add what it missed to the check), do not argue from the number.
6. **`AnimGapCheck.AuditLayers(root)`**: every layer is its expected number of connected pieces (hands = 2, everything else 1) and has no FRAGMENT/junk. A fragment is something that will fly along with the layer. (Base may keep a few tiny fur-tuft specks; they are static and harmless.)
7. **`AnimGapCheck.RenderOnly(root, new[]{"Base"}, png)`: look at the base ALONE.** After the cut it must be a clean body with flat fill where parts were removed. Any stray dark dash/outline piece in the area of a moving part is something that should have gone with that part (it stands still → "lost outline"). Do the same for each layer on magenta.
8. Live zoomed screenshots in Play mode at two different frames of each state. Report numbers and what you saw.
9. When the user points at a spot ("the eye next to the heart is chipped"), find it BY EYE first (rest | peak side by side at 12–16× around that exact spot, plus base-only), then fix the rule that caused it, then add the check that would have caught it. Their words may name the wrong object (heart vs eye): confirm with a picture of what you think they mean if the crop shows nothing wrong.

## 6. Tell the user honestly
- Source art ≤ ~256px or pixel-style shows jaggies when scaled; the cut cannot fix that.
- Parts you could not or did not cut (and why), the amplitude you capped, and what layered export would unlock (ears, hair, tail).
