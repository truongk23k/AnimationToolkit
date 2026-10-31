# Seamless looping

A loop is seamless when pose(0) == pose(T) **and** velocity matches.

1. **Whole cycles**: every periodic animator must complete an integer number of cycles in the clip duration. `ClipBaker.Bake(..., snapToLoop: true)` calls `SnapToLoop(duration)` which rounds `frequency` (≥1 cycle). Frequency changes slightly — acceptable for ambient motion.
2. **Closed key**: baker forces last key = first key and uses wrap-around central-difference tangents, so velocity also matches.
3. **Decaying swings** (`damping > 0`) must use `repeatInterval` equal to a divisor of the duration; otherwise the kick resets mid-clip. Prefer damping 0 for baked loops.
4. **Don't bake**: Blink (random), sprite swaps, alpha. Keep them live (`BlinkAnimator` runtime) next to a baked body clip.
5. **Live (non-baked) loops** are inherently seamless because they are sine/frac based — no bake needed. Prefer live animators in playables (smaller build, no clip assets).
6. Clip settings: `loopTime = true`; legacy clips also get `WrapMode.Loop`. Legacy clips play with the `Animation` component (no Controller).
7. Mixed durations: pick duration = longest period (e.g. breath 5 s, tail 1.4 s → 5 s clip; tail snapped to 7 cycles = 1.43 s).

Check: after baking, compare `curve.keys[0].value` and `curve.keys[last].value` for every binding (must be equal).
