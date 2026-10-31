# Procedural animation recipes

Every procedural animator computes an **offset from the rest pose** as a function of time, then `OffsetChannel` applies only the *difference* from the previous frame. Consequences:
- Layers stack: Breathing + Sine + Swing on the same Transform just add.
- `ClearOffset()` restores the exact rest pose (verified error ≈ 1e-6).
- Don't let other code assign the same Transform's `localPosition/Rotation/Scale` every frame — animate a child.

## Recipes
**Idle character**: `BreathingAnimator` on body (0.02–0.03 @ 0.2 Hz) + `BlinkAnimator` + `HairAnimator` (4°) with `randomizeStart`.
**Tail wag** (chain of N segments): `TailAnimator` with `Chains[0].segments = [root..tip]`, amplitude 14–20°, 0.6–0.8 Hz, phaseDelay 0.12, optional `Fin`. Excited: amplitude ×1.6, frequency ×2, or call `Wag(2f)` for a burst that eases in/out.
**Wave hand**: `SwingAnimator` angle 28, speed 1.6 Hz, damping 0.6, repeatInterval 3 → waves, settles, waves again.
**Float**: `FloatingAnimator` vertical 0.08 @ 0.3 Hz on the root visual.
**Pop-in**: `TweenAnimator` always goes from the rest pose (k=0) to the offset (k=1), so it cannot scale up from 0. Place the object at its *start* scale (e.g. 0.8) and set `scaleMultiplier` = target/start (1.25) with `EaseType.BackOut`, `loopMode = Once`, `holdEndPose = true`. For fade-in use `animateAlpha` (alphaFrom 0 → alphaTo 1).
**Follow-through on dragged object**: `SecondaryMotionAnimator` on child; source = parent that the player moves.
**Looking around**: `SineWaveAnimator` on pupils, amplitude (0.03, 0.01, 0), 0.15–0.2 Hz.

## Chains
`ChainWaveAnimator` gain runs from `rootGain` (small) to `tipGain` (large); wave lag `phaseDelayPerSegment`. Segments must be nested (child of previous) so rotations accumulate like a real tail. More segments (4–6) = smoother curve.

## Stateful vs pure
Pure (bake-friendly, loop-perfect): Sine, Swing (with interval), Breathing, Floating, Tween, Tail, Hair.
Stateful: `SecondaryMotionAnimator` (spring, needs warm-up; baker warms up 2 s). Random: `BlinkAnimator` (not baked).
