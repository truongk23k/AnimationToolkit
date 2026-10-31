# Animation principles → toolkit settings

| Principle | How to get it | Typical values |
|---|---|---|
| Ease in/out | Use sine-based motion (all procedural animators) or `EaseType.SineInOut/SmoothStep` in `TweenAnimator` | never `Linear` for organic motion |
| Follow-through | `SecondaryMotionAnimator` on trailing parts; tip gain > root gain in chains | stiffness 80–200, damping ratio 0.25–0.5 |
| Overlapping action | Phase delay along a chain; different `phase`/`timeOffset` per part | `phaseDelayPerSegment` 0.1–0.2 cycles |
| Secondary motion | Hair/tail/ears react after the body moves | amplitude ≤ 1/3 of main motion |
| Anticipation | `TweenAnimator` that first moves opposite (small, 0.1 s) then the main move, or `EaseType.BackOut` | 10–20% of main amplitude |
| Squash & stretch | `BreathingAnimator.preserveVolume`, `SineWaveAnimator.scaleAmplitude` with opposite X/Y signs | 3–8% for subtle, up to 20% for cartoony bounce |
| Timing & rhythm | Calm idle 0.15–0.4 Hz; excited 1–2 Hz; never exactly equal frequencies on all parts | vary by ±10–20% |
| Seamless loop | Whole cycles only; `SnapToLoop(duration)` when baking | see looping-animation.md |

## Amplitude guide (2D, 1 unit ≈ character height/3)
- Breathing scale Y 0.02–0.04; position 0.01–0.02.
- Floating vertical 0.05–0.12.
- Head tilt 3–5°; hand wave 20–30°; hair 3–8° **per segment**.
- Tail/Hair/chain amplitude is **per segment** and angles accumulate down the chain (4 segments at 8° already sweep the tip a long way). Idle tail 6–10°, wag 10–14°, excited ≤ 20° per segment. Tested visually: 14°/segment on a 4-segment tail looked too violent for "gently waving".

## Avoid
- Mechanical: all parts same frequency/phase, linear easing, constant amplitude.
- Sudden changes: starting/stopping animators without a blend (use `FadeIn(0.3f)` / `FadeOut(0.3f, true)`).
- Excess: amplitude over ~35° on chain segments, scale changes over 10% for idle.
- Loop pops: non-integer cycles in a baked clip, damped swings baked without matching interval.
- Rotating a sprite around its center when the joint is elsewhere — fix the pivot instead.
