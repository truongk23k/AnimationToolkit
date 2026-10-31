---
name: unity-animation-toolkit
description: Animate ANY Unity 2D/3D object or character for playable ads without Spine — cat jumping/walking, chest opening and bouncing, bird flapping and flying, person jumping, capybara eating, mermaid tail wag and smile, UI button pops, coins, bubbles, plants. Use whenever the user describes a motion in words ("make X do Y"), gives a still/flat image or sprite sheet to bring to life (the skill cuts it into layers without gaps), wants several named animations on one object (Idle/Fly/Eat…), or wants motion baked to an AnimationClip. Provides a procedural animator library in Assets/AnimationToolkit, art-cutting rules with QA tools, and a method to decompose any motion into primitives.
---

# Unity Animation Toolkit — AI Skill

You turn a plain-language motion description into working Unity animation using the library in `Assets/AnimationToolkit` (Runtime animators + `AnimKit` one-liners + `AnimStateSet` + `ClipBaker`). **Any motion** is built the same way: decompose it into primitives, map each to a component, tune values, verify by measuring **and by looking**. Never invent APIs — use only `references/unity-animation-api.md`.

## Inputs to ask for before starting (if the user did not give them)
1. **Art** — best: one PNG per part (body, head, wing L/R, tail segments, legs, eyes open/closed, lid…) with transparent background. Acceptable: a **flat image or sprite sheet** — you cut it (see *Flat art* below). Say honestly what could not be cut.
2. **Which animations** — a list with names, e.g. `Idle`, `Fly`, `Eat`; for each: loop or one-shot, in place or travelling.
3. **Reference for the feel** (optional but ideal) — GIF/video, a Spine/Unity clip name, or a few words ("calm breathing", "frantic flapping"). A Spine `.json` can be read for **timing and amplitude** (bone rotate/translate/scale → keyframes); meshes, skinning, deform cannot be reproduced. Atlas pages with flat colour silhouettes (dye/mask regions) are not final art — ask for the real sprites.
4. **Target** — which scene/GameObject, 2D or 3D, live animators or a baked `.anim`.

## The method (every request)
1. **Inspect**: hierarchy, SpriteRenderers, pivots, existing Animator/DOTween/Spine, Unity version, render pipeline. Check `Application.isPlaying` — the user may be running Play mode. Don't touch unrelated files.
2. **Decompose** the motion into primitives (table below). Write the list: *what moves, how, how fast, in what order*.
3. **Check parts exist.** Each moving part needs its own Transform with the pivot at its joint. If not → *Flat art* rules; never fake it silently.
4. **Map** each primitive to a component, `AnimKit.*`, or `PoseSequenceAnimator` keyframes; stack several on one object (layers add up). Put every animation in an **`AnimStateSet`** state on the root (template `templates/BuildAnimStates.cs.txt`): Inspector gets a Spine-style **Animation** dropdown, **Loop**, **Time Scale**; non-start animators disabled.
5. **Tune** from `references/animation-principles.md` and the closest recipe in `references/recipes.md`. Desync siblings with `TimeOffset`.
6. **Verify** (all of it): console 0 errors → tick animators in edit mode and measure ranges → for cut art run every gate in `references/splitting-flat-art.md` → look at Play-mode screenshots (two frames per state, zoomed on joints). Report numbers, not adjectives.
7. **Report**: what was animated, parameters, loop/once per state (`IsLooping`, `EffectiveCycle`, `Describe()`), what was not cut and why, limits (resolution, Luna untested), how to adjust. The user can select the object: Scene view panel + Inspector header show loops; **Tools ▸ Animation Toolkit ▸ Animation Viewer** previews each state with timeline and curves.

### Definition of done
- [ ] Console has 0 errors; scene saved (`EditorSceneManager.SaveScene`); Play-mode-only changes are not mistaken for saved ones.
- [ ] Each requested animation is a named `AnimStateSet` state with the right Loop; start state set; non-start animators disabled.
- [ ] Measured: ranges/periods match the intent (numbers in the report).
- [ ] Cut art: mask overlay, layer sheet, rest-vs-peak crops, **`AnimGapCheck.Run` PASS incl. the ground/shadow layer**, **`AuditLayers` clean**, base-only view clean.
- [ ] If the user wants to show it off: a demo GIF per state with `GifRecorder.Record` (check the seam message says `loops cleanly`), saved under `Documentation~/` and linked from the README.
- [ ] Temporary files removed (`Assets/Screenshots`, scratch PNGs); camera/objects touched for measuring are restored.

## Primitive vocabulary — any motion is a mix of these
| Primitive | Meaning | Use |
|---|---|---|
| **Oscillation** | smooth periodic motion | `SineWaveAnimator` (pos/rot/scale), `SwingAnimator` (pendulum, decaying wave), `BreathingAnimator`, `FloatingAnimator` |
| **Cycle with phases** | parts moving offset in time | `WalkCycleAnimator` (legs), `FlapAnimator` (wings), chain `phaseDelay` (tail, hair, tentacle) |
| **One-shot with easing** | go from A to B once | `TweenAnimator` (Once), `AnimKit.PopIn/MoveBy/RotateBy`, `PoseSequenceAnimator` (Once) |
| **Hop / ballistic** | anticipation → air → landing | `JumpAnimator` (squash/stretch, `Landed` event) |
| **Path** | travel along a curve | `OrbitAnimator` (ellipse, figure-8, banking) |
| **Chain wave** | wave travelling down connected segments | `TailAnimator`, `HairAnimator` (tail, antenna, rope, flag) |
| **Spring / follow-through** | lag behind a moving parent | `SecondaryMotionAnimator` |
| **Keyframed pose** | anything irregular | `PoseSequenceAnimator` / `AnimKit.Keyframes` — the universal fallback |
| **State swap** | change sprite (eyes, mouth, lid, frame) | `BlinkAnimator` (sprite swap), `FacialExpressionController`, `ExpressionPulse` |
| **Several animations on one object** | Idle ⇄ Fly, Eat ⇄ Happy | `AnimStateSet` (cross-fade, Loop, Time Scale, `StateCompleted`) |
| **Custom** | nothing above fits | new `OffsetAnimator` from `templates/CustomAnimator.cs.txt` |

If a request mixes several (cat jump = Jump + tail chain + ear Swing + Breathing) add one component per primitive.

## Decision shortcuts
- Repeating idle → oscillation/cycle components (live, no clip). Different actions in sequence → `PoseSequenceAnimator` or `JumpAnimator.Jump()` from code. Needs an `.anim` → `ClipBaker.Bake`. Prefer live animators in playables.
- Asymmetric action (fast power stroke, slow recovery) → use the primitive that encodes it (`FlapAnimator.downstrokeRatio`, `BlinkAnimator`, `BuildOpenKeys`) or per-key `ease` in keyframes.
- **Whole-object motion has no seams** (squash, bounce, sway, breathing) — put most liveliness there when art is cut from a flat image; use cut parts only for small, high-value motion.

## Flat art (a single image / sprite sheet)
- A truly flat single sprite supports whole-object motion only. Per-part motion (tail, blink, wing, chewing) needs cutting.
- **Cutting is a craft with hard rules — read `references/splitting-flat-art.md` BEFORE cutting.** Summary: cut only parts with a real boundary (leave same-colour ears static); every moving layer needs an **overlap ring of original pixels**, content under whatever occludes it (snout under the melon, **ground/shadow as a full ellipse**), and must carry its WHOLE look (rings/outlines/nose lines) but nothing of its neighbours (cut by shape + colour + direction, never by rectangle); joint displacement ≲ 2 px; holes in the base filled from strict fur colour (row+column), outside-body holes extended with a 2px clear ring at the background; despeckle + keep-connected on every layer.
- **Gates are mandatory and a PASS number never overrides what the user sees**; when the user points at a spot, find it by eye first (rest | peak side by side at 12–16×, base-only), fix the rule, add the check. Worked examples: `examples/capy-eating-watermelon.md` (flat PNG), `examples/mermaid-from-spritesheet.md` (sprite sheet, fin by hue + face patch from an alternate frame).
- Pivots: a swinging part pivots at its joint (re-pivot the sprite or parent it under an empty); hinges at the hinge edge.
- Always list in the report what you could not cut and what the artist should export.

## Working with Unity through MCP — pitfalls we hit
- `execute_code` is a method **body**: no `using` lines (use fully qualified names); `Object` is ambiguous → `UnityEngine.Object`; some names clash (`RectInt`) → use ints; LINQ and local functions are available.
- After editing scripts: `refresh_unity(compile=request)` → `read_console` for errors → confirm the new types exist. Editor-script static state resets on every compile.
- `IOException 1224` when rewriting a PNG Unity just imported → `refresh_unity(scope=assets)` and retry.
- Screenshots land in `Assets/Screenshots` → delete the folder when done. `manage_camera` only captures Game/Scene view (not Inspector/EditorWindows) — measure and describe those instead of claiming you saw them.
- Don't leave `Camera.main` modified: use a private camera (`AnimGapCheck`/`RenderOnly` do) or restore it. Edit-mode previews must restore poses and `enabled`/`weight`/`speed` (the toolkit's runners do).
- Regenerating scenes (`NewScene`) while the user is in Play mode breaks their session — check `Application.isPlaying` first.
- Changes made in Play mode are discarded on exit; edit-mode scene changes persist only after `SaveScene`.
- Runtime code must stay Luna/WebGL-safe (no OnGUI, Input, LINQ in hot paths, reflection, Newtonsoft, coroutines, physics). **Not yet verified in a real Luna build** — say so.

## Rules
1. Use only toolkit APIs. If something is missing, say so or write a custom animator — don't guess names.
2. Animators add **deltas** on top of the rest pose. If other code assigns the same Transform's pose every frame, animate a child.
3. Don't make two animators fight for the same property (a Tween and a clip on one Transform).
4. Never report success without a measurement **and** a look. Don't argue from a metric against the user's eyes; extend the check instead.
5. Be honest about limits: source resolution, parts left static, untested platforms.

## Files
- `references/unity-animation-api.md` — every class, field, method (incl. AnimStateSet, AnimGapCheck).
- `references/recipes.md` — recipes: cat walk/jump, chest, bird Idle+Fly, person jump, mermaid, button pop, coin, bubble, plant, door, heartbeat…
- `references/splitting-flat-art.md` — cutting flat art: pipeline, overlap rule, what goes with a part, motion budget, quality gates.
- `references/animation-principles.md`, `procedural-animation.md`, `secondary-motion.md`, `looping-animation.md`, `performance-guidelines.md`.
- `templates/` — `AddAnimatorsDirect`, `BuildAnimStates`, `CustomAnimator`, `BakeSelection`. `examples/` — `capy-eating-watermelon`, `mermaid-from-spritesheet`, `prompt-to-animation`.
