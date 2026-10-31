# Worked requests — how the method maps words to components

| User says | Decompose | Build |
|---|---|---|
| "the cat jumps and lands softly" | main: ballistic hop · secondary: tail, ears · life: breathing | `JumpAnimator` (1.1, 0.5) + tail `TailAnimator` 14° + ears `Swing` + `AnimKit.Breathe` |
| "chest opens and bounces happily" | one-shot hinge with overshoot · looped small hop | `BuildOpenKeys(105,0.4,10,0)` (Once) + `JumpAnimator.Configure(0.12,0.22,true)` |
| "bird flaps its wings and flies around" | wing cycle (asymmetric) · path with banking · tail flutter | `FlapAnimator` (3 Hz, 80°) + `OrbitAnimator` + `TailAnimator` |
| "person jumps with joy" | hop · arms raise on take-off · hair follow-through | `JumpAnimator` + arm `AnimKit.Keyframes` + `HairAnimator` |
| "sleepy character nods" | slow breathing · head nod irregular | `Breathe(0.035, 0.12)` + head `Keyframes` (down 0.8 s, up 0.4 s, `CubicOut`) |
| "button pops in then pulses" | one-shot scale with overshoot · loop pulse | `PopIn` then `Pulse` (add Pulse when `Completed`) |
| "make it feel alive" | idle: breath + sway + blink, desynced | `Breathe` + `Wobble(2°)` + `BlinkAnimator`, `randomizeStart` |
| "make this still picture of a capybara eat the watermelon happily" (flat PNG) | cut layers (snout, melon+hands, feet, heart, shadow) → chewing, bites, whole-body bob, happy feet; two states | `splitting-flat-art.md` pipeline, then `Eat` + `Happy` `AnimStateSet` states; gates incl. `AnimGapCheck` with the shadow |
| "idle and fly for this bird" | two named states, one object | `AnimStateSet`: Idle (breathe, head look, blink) / Fly (flap, lift, tuck legs) |
| "something I can't name, like a sneeze" | irregular → keyframes | `Keyframes`: anticipation back-lean (−6°, 0.2 s), snap forward (+12°, 0.08 s, `CubicOut`), recover (0.4 s) |

## Reasoning template to write before coding
```
Target: <name>, parts found: <list>, parts missing: <list → artwork limits>
Motion primitives: 1) … 2) … 3) …
Components: <class → target Transform → key values>
Verify: <what to measure / screenshot>
```
