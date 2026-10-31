# Example: capybara sitting and happily eating a watermelon (flat 213×231 PNG, solid cream background)

**Request**: "capy ngồi ăn dưa hấu vui vẻ sinh động", art = `CapyArt/capy.png` (one flat image, no layers, no alpha). Result: `Samples/CapyAnimTest.unity`, generator `Editor/CapySampleGenerator.cs`.

## Decomposition
| What | Primitive | Parameters |
|---|---|---|
| chewing | mõm (snout) `SineWave` | pos y 0.012 (1.2 px), rot 0.8° @ 2.4 Hz; Happy 0.016, 1.2° @ 3.6 Hz |
| biting | watermelon (+hands as child) `Keyframes` loop 3 s | lift 1.4–1.8 px, rot 1.1–2°, four nibbles; Happy loop 1.8 s |
| life of the whole body | `Breathe` 1.2% @ 0.45 Hz, `Swing` ±0.8°, `SineWave` bob 0.6 px @ 2.4 Hz (Happy: 2%, ±2°, 1.6 px @ 3.6 Hz) | no seams — the main liveliness |
| happy feet | foot L/R `Swing` | ±3° @ 1 Hz (Happy ±6° @ 1.6 Hz, phase 0.28) |
| heart bubble | `Hover` + `Pulse` | 0.05/0.07 @ 0.6/1.4 Hz (Happy 0.08/0.14 @ 1/2.2 Hz) |
| shadow | `Pulse` | 1.2% @ 0.45 Hz (Happy 3% @ 2.2 Hz) |
Two states in an `AnimStateSet`: **Eat** (start) and **Happy**, both loop.

## What was cut (and what was not)
Layers: base · snout (soft matte) · watermelon · hands (child of watermelon) · foot L · foot R · heart bubble · shadow. Everything except the snout is a hard mask grown by 1px.
**Not cut**: the right ear — same flat brown as the head, no outline separating it; the first attempt (cut line across the ear, extension under the head) always smeared brown. It stays static.

## Problems found and how they were fixed (these are now rules in `splitting-flat-art.md`)
1. Hole fill by 3×3 averaging → diagonal chevrons; by row only → horizontal streaks → **row + column average from strict fur colour**.
2. Fill sources included leftover watermelon rind → green/red patches → **strict fur-colour family only**.
3. Chest outline missing where hands/melon hid it → concave bite in the silhouette → **fill-region ellipse for the chest + synthesized 2px outline**.
4. Foot/hand holes outside the body left transparent → crack between foot and thigh when rotating → **BFS edge extension into outside holes, 2px ring at the background kept clear**.
5. Pale seam lines at layer borders (bilinear) and cracks at attachments → **overlap ring: 3px of original pixels around hands, melon, feet** (growing 1px with edge colours was not enough).
6. Amplitudes that were fine on the whole sprite opened gaps at joints (feet ±12°, melon 3–4 px) → **capped at ≈1.5–2 px**; liveliness moved to whole-body bob/squash.
7. Skin-coloured strip between snout and watermelon when the melon lifted → **snout extended 10px under the melon**.
8. Thin background-coloured line under the belly when the body bounced → **shadow made a complete ellipse** (only on body pixels). The first gap check ignored the shadow layer and said PASS; the user still saw the gap. Including the shadow layer in the check exposed it (567 / 1287 px), after the fix 34 / 115 px.

9. Heart bubble carried a dark triangle (corner of the head outline) and faint haze → cut by shape, reject dark/brown, `KeepConnected`, alpha ≥ 6%, `Despeckle`.
10. Snout: head-outline stroke flew with it (soft matte also accepted colours darker than the snout), then the eyes' rings were missing (too strict), then a peach fleck flew with it (lighter pixels accepted). Final rule: matte only for `0.04 < t < 1.2`; inside the nose area accept pixels **darker** than the head colour. Found only by looking at rest | peak at 16×, base-only and the layer on magenta — the numeric checks passed throughout.
11. Hole fill leaked a grey block (pale rind used as a colour source) → sources limited to fur/outline.

## Verified
Static recomposite: mean abs diff 0.6, per-layer registration identical (all layers share the same sub-texel offset ⇒ no relative misalignment).
`AnimGapCheck` (600 px height): body only Eat 31 px / Happy 61 px; **including the shadow layer** Eat 34 px (0.023%) / Happy 115 px (0.077%) — PASS. Earlier versions: fill without the outside-body extension 214 / 623 px with a visible crack at the left foot.
Rest-vs-worst sheet on magenta and 6× crops of snout/melon, left hand and feet at the Happy peak pose: no gaps. Live Play screenshots zoomed on hands/melon and feet, two frames each: hands stay attached.

## Limits to report
Source is 213 px wide and pixel-styled → jagged when scaled. Right ear static. A layered export (ear, arms, head, eyes) would allow ear flicks and a real head-bob.
