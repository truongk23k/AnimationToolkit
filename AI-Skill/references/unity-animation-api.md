# Toolkit API reference (namespace `AnimationToolkit`)

All animators derive from `ToolkitAnimator : MonoBehaviour`, run in `LateUpdate`, add a **delta** on top of the rest pose, stack with each other, and restore the pose when disabled.

## ToolkitAnimator (base)
Serialized: `speed`, `weight (0..1)`, `timeOffset`, `randomizeStart`, `unscaledTime`.
Members: `Speed`, `Weight`, `TimeOffset`, `ElapsedTime`, `IsBakeable`, `IsLooping` (false = plays once / only when triggered), `CycleDuration` and `EffectiveCycle` (seconds per cycle, 0 = continuous/random), `Describe()` (one-line summary — use it in your final report to state what runs and whether it loops), `Tick(float dt)`, `SetTime(float)`, `ResetState()`, `ClearOffset()`, `FadeTo(w, dur)`, `FadeIn(dur)`, `FadeOut(dur, destroyWhenDone)`, `GetAffectedTransforms(List<Transform>)`, `SnapToLoop(duration)`.
`OffsetAnimator : ToolkitAnimator` adds serialized `target` (empty = this transform), property `Target`, and the override point `Sample(float t, float dt, ref AnimPose pose)`.

## AnimKit — one-liners (every function adds a configured animator and returns it)
`Breathe(t, amount=0.03, breathsPerSecond=0.25)` · `Hover(t, height=0.1, hz=0.35)` · `Swing(t, degrees=8, hz=0.5)` · `Wobble(t, degrees=6, hz=0.8)` · `Pulse(t, amount=0.08, hz=1.2)` · `Shake(t, distance=0.05, hz=12)` · `Spin(t, secondsPerTurn=2, clockwise=true)` · `Bounce(t, height=0.4, airTime=0.4, repeat=true)` · `Orbit(t, radiusX=1.5, radiusY=0.6, orbitsPerSecond=0.2)` · `PopIn(t, duration=0.4, fromScale=0.6)` (ends at the current scale) · `MoveBy(t, offset, duration, ease)` · `RotateBy(t, degrees, duration, ease)` · `Key(time, position, rotZ=0, scale=1, ease)` · `Keyframes(t, loopMode, params PoseKey[])` · `StopAll(root, fade=0.25)`.

## Animators
| Class | Key serialized fields | Helpers |
|---|---|---|
| `TweenAnimator` (Offset) | positionOffset, rotationOffset(euler), scaleMultiplier, animateAlpha/alphaFrom/alphaTo, duration, delay, ease, loopMode, holdEndPose | `Configure(pos, rot, scaleMul, dur, ease, loop)`, `Play()`, event `Completed`, `IsFinished` |
| `SineWaveAnimator` (Offset) | frequency, phase, positionAmplitude, rotationAmplitude, scaleAmplitude | `Configure(posAmp, rotZ, freq, phase)`, `ScaleAmplitude` |
| `SwingAnimator` (Offset) | angle, swingSpeed, axis, damping, repeatInterval, centerAngle | `Configure(angle, hz, damping, interval)` |
| `BreathingAnimator` (Offset) | breathsPerSecond, inhaleRatio, scaleAmount(V2), positionAmount, preserveVolume, phase | `Configure(scaleY, breathsPerSec)` |
| `FloatingAnimator` (Offset) | frequency, verticalAmplitude, horizontalAmplitude, tiltDegrees, phase | `Configure(vertical, freq)` |
| `JumpAnimator` (Offset) | height, airTime, travel, anticipation, landing, autoRepeat, pause, squash, stretch | `Configure(height, air, repeat)`, `Jump()`, event `Landed`, `Period` |
| `OrbitAnimator` (Offset) | radius(V2), frequency, yHarmonic(1 ellipse, 2 figure-8), phase, bank | `Configure(radius, freq)` |
| `PoseSequenceAnimator` (Offset) | keys(`PoseKey{time, position, euler, scale, ease}`), loopMode, delay | `Keys`, `LoopMode`, `Play()`, `IsFinished`, event `Completed`, static `BuildOpenKeys(angle, duration, overshoot, cycle)` |
| `SecondaryMotionAnimator` (Offset) | source, stiffness, damping(ratio), positionInfluence, rotationInfluence, maxLag, maxAngleLag | `Configure(rotInfluence, stiffness, dampingRatio)` |
| `ChainWaveAnimator` → `TailAnimator`, `HairAnimator` | chains(`WaveChain{segments, phase, amplitudeScale}`), amplitude(per segment), frequency, phaseDelayPerSegment, rootGain, tipGain, bias, rotationAxis | `Configure(ampDeg, freq, phaseDelay)`, `Chains`, `RebuildChannels()` |
| `TailAnimator` | + fin, finAmplitude, finDelay, wagMultiplier | `Fin`, `Wag(duration)` |
| `HairAnimator` | + autoSpreadPhase, spreadPerSection | – |
| `WalkCycleAnimator` | legs(`WalkLeg{leg, phase}`), body, head, stepsPerSecond, swingDegrees, liftHeight, direction, bodyBob, bodyTiltDegrees, headBob | `Configure(steps, swing, bob)`, `Legs`, `Body`, static `MakeLegs(Transform[])` (2 legs alternate, 4 legs diagonal gait) |
| `FlapAnimator` | wings(`FlapWing{wing, side(±1)}`), body, flapsPerSecond, upAngle, downAngle, downstrokeRatio, foldScale, bodyBob, bodyTilt | `Configure(flaps, range, bob)`, `Wings`, `Body` |
| `BlinkAnimator` (not bakeable) | mode(SpriteSwap/ScaleY), eyeRenderers, open/half/closedSprite, eyeTransforms, closedScaleY, min/maxInterval, blinkDuration, doubleBlinkChance | `Configure(min,max)`, `SetupSprites(renderers, open, half, closed)`, `SetupScale(Transform[])`, `SetOpenSprite(Sprite)` |

## AnimStateSet — several named animations on one object (like Spine's animation list)
Fields: `states` (`AnimState{name, loop, duration, animators[]}`), `startState`, `crossFade`, `timeScale`. No on-screen buttons — control it from code or from the Inspector.
Members: `Play(name)`, `Play(name, fade)`, `Play(index, fade)` (new state's animators restart + fade in; the old state's animators fade out and **disable themselves**; animators shared by both states are kept; playing the same state again restarts it only when it is frozen), `Restart()`, `Current`, `CurrentIndex`, `Count`, `IsFrozen`, `StateName(i)`, `StateAnimators(i)`, `StateLoop(i)`, `SetStateLoop(i, bool)` (restarts the running state), `StateLength(i)`, `StartState`, `TimeScale` (live), event `StateCompleted(name)`.
**Loop:** `loop=true` repeats forever; `loop=false` plays one pass (`duration`, or the longest animator cycle if 0), then holds the final pose (animator speeds set to 0) and raises `StateCompleted`.
Build: add each state's animators to `states[i].animators`, disable the non-start ones, add `AnimStateSet` on the root. **Inspector (Spine-style)**: *Animation* dropdown, *Loop*, *Time Scale* — in edit mode choosing an animation runs it on the object immediately (restores the pose when stopped/deselected/saved), in Play mode it switches the real animation. **Tools ▸ Animation Toolkit ▸ Animation Viewer** adds timeline, scrubbing and curves. `ToolkitAnimator.FadeOutAndDisable(duration)` is the primitive behind it. `FlapAnimator.SetAngles(top, bottom)` sets asymmetric stroke angles.

## Non-animator helpers
- `FacialExpressionController`: `eyeRenderers`, `mouthRenderers`, `pupilRenderers`, `expressions` (`FaceExpression{name, eyes, mouth, hidePupils}`), `defaultExpression`, `blink`. `Has(name)`, `SetExpression(name)`→bool, `SetDefaultExpression`, `PlayTemporary(name, seconds)`→bool, `Current`, `ExpressionNames`.
- `ExpressionPulse`: `Setup(face, expression, minInterval, maxInterval, duration)` — occasionally flash an expression.
- `AnimEasing.Evaluate(EaseType, t)`, `AnimEasing.Progress(time, duration, AnimLoopMode, out finished)`. `EaseType`: Linear, SineIn/Out/InOut, QuadIn/Out/InOut, CubicIn/Out/InOut, BackOut, ElasticOut, BounceOut, SmoothStep, SmootherStep. `AnimLoopMode`: Once, Loop, PingPong.
- `OffsetChannel` (`SetTarget`, `Apply(AnimPose, weight)`, `Clear`) and `AnimPose.Identity` — building block for custom animators.

## Editor-only (`AnimationToolkit.EditorTools`)
`ClipBaker.Bake(GameObject root, float duration, int fps, bool loop, bool legacy, bool snapToLoop)` → `Result{clip, curveCount, notes}`; save with `AssetDatabase.CreateAsset(result.clip, path)`. Skips non-bakeable animators (Blink) and records the skip in `notes`.

### Art QA (Editor-only)
`AnimGapCheck.Run(GameObject root, string[] ignoreNames, string mapDir, int frames = 240)` → `List<Result{state, maxNewGap, worstFrame, meanNewGap, maxRatio, mapPath, Pass}>`; `AnimGapCheck.Report(results)`. Runs each `AnimStateSet` state (or all animators) frame by frame at 1/30 s on a private camera, counts NEW cracks (empty pixels with solid pixels within ~4 texels on both sides) and enclosed holes versus the rest pose, and writes `gap_<state>.png` (red cracks / blue holes) plus `motion_<state>_rest_vs_worst.png` (rest | worst frame on magenta). Pass = worst frame < 0.15% of the silhouette. `ignoreNames` = GameObjects that float free (speech bubble) — do **not** ignore the ground/shadow.
`AnimGapCheck.AuditLayers(GameObject root)` → text report per SpriteRenderer: connected pieces (alpha &gt; 25, 8-neighbour), largest piece, `FRAGMENT … junk: Npx at (x,y)` for tiny loose pieces (junk that flies along with the layer).
`AnimGapCheck.RenderOnly(GameObject root, string[] names, string pngPath, int height = 900)` → renders only the named renderers at rest on magenta (base alone, or one layer) — use it to spot leftovers/junk by eye.
`CapySampleGenerator` (Editor) is the reference implementation of the whole flat-art pipeline (bg flood fill, colour-family masks, soft matte, hole fill, overlap rings, pivots, recomposite, scene + two AnimStateSet states).

## Writing a new animator
Derive from `OffsetAnimator`, override `Sample(t, dt, ref pose)` writing offsets relative to rest; override `SnapToLoop` when periodic; keep it a pure function of `t` when possible (bakeable, loops cleanly). Multi-Transform animators derive from `ToolkitAnimator` and own one `OffsetChannel` per Transform (see `WalkCycleAnimator`).
