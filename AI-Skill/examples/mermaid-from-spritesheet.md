# Example: animate the user's own flat mermaid art (tail wag + smile + blink)

**Request**: "Mermaid lying on a rock: wag the tail, smile, blink." Art = one sprite sheet of 8 near-identical frames (eyes open / eyes closed smiling), **no separate layers**.

## What was done (verified in `Samples/MermaidTest.unity`)
1. **Inspect**: sheet 1536×1024, frames 352×343, frame 0 = eyes open, frame 1 = eyes closed (smiling). No tail/eye layers exist → per-part motion needs splitting.
2. **Split the flat art with code** (one-off editor script / `execute_code`, `Texture2D.GetPixels32`):
   - **Tail fin**: hue mask (cyan, 0.47–0.63) in the top-left region, limited to x<100 so green flower leaves stay; erase it from the base at y ≥ cut line; extend the fin sprite 14 px below the cut so it is hidden under the base (no seam). Also erase low-saturation leftovers in the zone with no hair. Fin sprite pivot = where the fin meets the tail body.
   - **Eyes**: frame 1 is not pixel-aligned with frame 0 → brute-force translation search (±24 px) on forehead/cheeks (excluding eyes+mouth) gave (dx,dy)=(−2,−3). Crop a 110×80 face patch from frame 0 (open) and frame 1 shifted (closed), feather the edges 9 px → no visible seam.
   - Import each as Sprite, PPU 100, **custom pivot**. Base pivot bottom-centre so scaling keeps the rock grounded.
3. **Hierarchy**: `Base` (order 0) → children `TailFin` (order −1, **behind** base so hair still covers the fin root), `Face` (order 1).
   Child position = (pivot px − frame centre x)/100, pivot y/100.
4. **Animate with the toolkit only**:
```csharp
AnimKit.Breathe(baseT, 0.006f, 0.22f);                       // subtle breathing, rock does not drift
AnimKit.Swing(finT, 13f, 0.7f);                              // tail wag
AnimKit.Wobble(finT, 3.5f, 1.9f).TimeOffset = 0.3f;          // flutter layered on top
var blink = face.AddComponent<BlinkAnimator>();
blink.SetupSprites(new[]{ faceSr }, openPatch, null, closedPatch); blink.Configure(2.2f, 4.5f); // blinkDuration 0.5 → "happy squint" like the sheet
```
5. **Verify**: console 0 errors; Play mode screenshots at different times show the fin swinging (z = 9.1°) with hair still overlapping it; forced closed-face frame blends without a seam.

## Limits to report
- Hair strands are part of the flattened base → they cannot sway. Ask the artist to export hair locks (and arms) as separate layers, or inpaint behind them, then add `HairAnimator` chains.
- Eye patch swaps the whole face region, so the mouth changes with the blink (matches the original frames).
