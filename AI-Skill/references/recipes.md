# Recipes — start from the closest one, then tune

Values marked ✔ were measured in the Unity editor with these exact settings. `t` = Transform. Assume pivots are at joints (feet for jumpers, hinge for lids, shoulders for wings/arms, root of chain for tails). Add every animator with `AddComponent`/`AnimKit`; they stack.

## Creatures

### Cat — walk ✔
```csharp
var legs = gameObject.AddComponent<WalkCycleAnimator>();
legs.Legs = WalkCycleAnimator.MakeLegs(new[]{ legFL, legFR, legBL, legBR }); // 4 legs → diagonal gait
legs.Body = body;
legs.Configure(1.5f, 26f, 0.04f);            // steps/s, swing deg, body bob  (✔ leg swing 26°)
var tail = gameObject.AddComponent<TailAnimator>();
tail.Chains = new[]{ new WaveChain{ segments = new[]{ tail1, tail2, tail3 } } };
tail.Configure(8f, 1.0f, 0.12f);             // per-segment degrees
AnimKit.Swing(earL, 6f, 0.4f);               // ears
```
Run = same with `Configure(2.8f, 40f, 0.08f)`. Idle = `AnimKit.Breathe(body, 0.025f, 0.22f)` + tail 10°@0.5 Hz + `BlinkAnimator`.

### Cat — jump ✔
```csharp
var j = root.gameObject.AddComponent<JumpAnimator>();  // pivot at feet
j.Configure(1.1f, 0.5f, true);               // ✔ peak 1.10, scaleY 0.78..1.15 (squash/stretch)
// manual: Configure(h, air, false); then j.Jump(); j.Landed += () => ...;
// secondary: tail 14°@1.4 Hz (TailAnimator), ears Swing 10°@0.63 Hz (jump period ≈ 1.6 s)
```
### Person — jump
`JumpAnimator` height 1.4, air 0.6; `HairAnimator` 7°@1.4 Hz on hair chains; arms via keyframes timed to the jump period (anticipation 0.18 s, air 0.6 s):
```csharp
AnimKit.Keyframes(armL, AnimLoopMode.Loop, AnimKit.Key(0,Vector3.zero), AnimKit.Key(0.18f,Vector3.zero,10), AnimKit.Key(0.5f,Vector3.zero,-70,1,EaseType.CubicOut), AnimKit.Key(1.08f,Vector3.zero,-10), AnimKit.Key(1.58f,Vector3.zero));
```
### Bird — flap and fly ✔
```csharp
var f = gameObject.AddComponent<FlapAnimator>();
f.Wings = new[]{ new FlapWing{ wing = wingL, side = -1 }, new FlapWing{ wing = wingR, side = 1 } };
f.Body = body; f.Configure(3f, 80f, 0.12f);  // flaps/s, total range deg, body bob (✔ ±40° mirrored)
AnimKit.Orbit(root, 2f, 0.7f, 0.18f);        // ✔ flight path x −2..2; banking follows velocity
tailAnimator.Configure(6f, 3f, 0.1f);        // optional tail flutter
```
Small birds: flaps 6–10/s, range 60°. Gliding: `weight` of FlapAnimator → 0.2 with `FadeTo`.
### Bird — two animations in one object: Idle + Fly in place ✔ (`Samples/BirdAnimTest.unity`)
Hierarchy: `Bird` (pivot at the feet) → LegBack/LegFront, Tail1→Tail2, WingFar, Body, WingNear, Head→Eye. Wing sprites: pivot at the shoulder, drawn pointing backwards (folded = rotation 0).
```
Idle (perched, loops): Breathe(body, 0.03, 0.45) · head Keyframes loop 7 s (tilt +9°, −7°, two pecks −16°) ·
  wingNear Keyframes loop 7 s (double flutter −16°/−11° at 3 s) + Swing 2° · BlinkAnimator (eye open/closed sprites, 0.22 s, every 2.2–5 s) · TailAnimator 5°/seg @0.35 Hz
Fly (hovers in place, loops): FlapAnimator near wing 4.5/s, SetAngles(−82, +36), body bob 0.16, Body = Bird ·
  FlapAnimator far wing SetAngles(−74, +30), TimeOffset 0.035 (depth) · MoveBy(bird, (0,0.45,0), 0.6 s) lift-off, held ·
  legs Keyframes Once (rotate −78°, scaleY 0.7 → tucked) · head SineWave ±0.04 @4.5 Hz · TailAnimator 9°/seg @2.25 Hz
AnimStateSet on Bird: states Idle (loop), Fly (loop), non-start animators disabled, crossFade 0.35. Switch from code: `bird.GetComponent<AnimStateSet>().Play("Fly")`.
```
Measured ✔: fly wing −74°…44° (rest 8°), bird y 0…0.5; idle head −16°…9°, body scaleY 1.00…1.03; after Play("Fly") all 6 Idle animators are off and 8 Fly animators on.

### Butterfly / bat
`FlapAnimator` flaps 8, range 100, `foldScale` 0.9; body `FloatingAnimator` 0.15 + `OrbitAnimator` figure-8 (`yHarmonic=2`).
### Mermaid — see `examples/mermaid-from-spritesheet.md`
Tail/fin: `AnimKit.Swing(fin, 13f, 0.7f)` + `AnimKit.Wobble(fin, 3.5f, 1.9f)` (two layers) ✔; blink/smile by face patch + `BlinkAnimator`; body `AnimKit.Breathe(base, 0.006f, 0.22f)`.
### Fish
`SineWaveAnimator` rotation Z 4°@0.8 Hz on body, tail `TailAnimator` 12°@1.6 Hz, fins `Swing` 10°; swim path `OrbitAnimator`.
### Snake / worm / tentacle
Segmented chain with `TailAnimator` (amplitude 18°, phaseDelay 0.18, rootGain 0.8 tipGain 1.2).

## Objects

### Treasure chest — open + bounce ✔
```csharp
var lidAnim = lid.gameObject.AddComponent<PoseSequenceAnimator>();      // pivot at the hinge edge
lidAnim.Keys = PoseSequenceAnimator.BuildOpenKeys(105f, 0.4f, 10f, 0f);  // angle, open time, overshoot, cycle (0 = once)
lidAnim.LoopMode = AnimLoopMode.Once;                                    // ✔ peaks 115°, settles 105°, IsFinished=true
var hop = chest.gameObject.AddComponent<JumpAnimator>(); hop.Configure(0.12f, 0.22f, true);
// open/close loop: BuildOpenKeys(105, 0.4, 10, 3f) + LoopMode.Loop
// trigger from code: lidAnim.Play(); lidAnim.Completed += ShowReward;
```
### Door / flap / drawer
`BuildOpenKeys(80, 0.5, 4, 0)` with the pivot on the hinge; drawer = `AnimKit.MoveBy(t, new Vector3(0.6f,0,0), 0.4f)`.
### UI button / popup appear ✔
`AnimKit.PopIn(t, 0.4f, 0.6f)` (ends at current scale ✔), then `AnimKit.Pulse(t, 0.06f, 1.2f)` for attention.
### Coin / star / gem
`AnimKit.Spin` (or `SineWave` scale X for a flip look), `AnimKit.Hover(t, 0.08f, 0.5f)`, shine = `TweenAnimator` alpha on a child.
### Bubble / balloon / cloud
`AnimKit.Hover(t, 0.15f, 0.25f)` + `AnimKit.Pulse(t, 0.03f, 0.5f)` with different `TimeOffset` per instance.
### Plant / grass / flag / rope / antenna
`HairAnimator` over the chain: amplitude 4–8°/segment, 0.3–0.6 Hz, phaseDelay 0.15. Wind gust: call `Configure` with a larger amplitude for a moment, or add a second animator and `FadeIn`/`FadeOut` it (`Weight` is capped at 1).
### Pendulum / clock / hanging sign
`AnimKit.Swing(t, 20f, 0.5f)` with damping via `GetComponent<SwingAnimator>().Configure(20, 0.5, 0.4, 3)` for a decaying swing re-kicked every 3 s.
### Heartbeat / pulse
```csharp
AnimKit.Keyframes(t, AnimLoopMode.Loop, AnimKit.Key(0f,Vector3.zero), AnimKit.Key(0.1f,Vector3.zero,0,1.18f), AnimKit.Key(0.2f,Vector3.zero), AnimKit.Key(0.3f,Vector3.zero,0,1.1f), AnimKit.Key(0.45f,Vector3.zero), AnimKit.Key(0.9f,Vector3.zero));
```
### Hit / shake / deny
`AnimKit.Shake(t, 0.06f, 14f)` and `FadeOut(0.3f, true)` after 0.4 s.

## Combining
Order of thought for any new request: (1) the main action (Jump / Walk / Flap / Tween / Keyframes) → (2) secondary parts (tail, ears, hair via chains / Swing) → (3) body life (Breathe/Hover) → (4) face (Blink / expression) → (5) timing offsets so nothing is in sync.
