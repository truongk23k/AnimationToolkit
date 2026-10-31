# Animation Toolkit — AI skill + procedural animators

A skill that lets an AI agent (Claude Code, Cursor) animate **anything** in Unity from a plain description — cat jumping, chest opening and bouncing, bird flapping and flying, a person jumping, a mermaid wagging her tail — using a small runtime library. No Spine, no DOTween, no paid APIs; Luna / WebGL-safe (no reflection, coroutines, physics).

## Layout
```
Assets/AnimationToolkit/
├─ AI-Skill/            SKILL.md + references/ + templates/ + examples/   ← the skill
├─ Runtime/             animators + AnimKit (what the skill drives)
├─ Editor/              ClipBaker, SkillInstaller, Animation Viewer, AnimStateSet inspector,
│                       AnimGapCheck (art-gap QA for cut layers), sample generators (Bird, Capy)
└─ Samples/             MermaidTest.unity (art cut from the user's sprite sheet, MermaidArt hidden)
                        BirdAnimTest.unity (generated bluebird, Idle + Fly states via AnimStateSet)
                        CapyAnimTest.unity (CapyArt/capy.png, one flat image cut into layers; Eat + Happy states)
```
Unused/older parts (editor window, rig/preset layer, procedural sample art) are parked in `_AnimationToolkit_Archive/` next to `Assets/` (outside Unity's compile).

## Use
1. **Install the skill**: button *Cài skill cho Claude Code* at the top-right of the **Animation Viewer** window (copies `AI-Skill/` to `.claude/skills/unity-animation-toolkit`). Generation itself always goes through the skill — there are no generator menu items.
2. **Look at / tune animations**: **Tools ▸ Animation Toolkit ▸ Animation Viewer**. Drag a GameObject in (or turn on *Follow selection*). An **Animation** dropdown (like Spine) picks the state (Idle, Fly…); timeline shows each animator's loop cycle, drag the playhead to scrub, Play/Pause/Stop previews on the real object (pose restored on Stop), curves plot the selected animator's motion, parameters are editable inline. `AnimStateSet` also has a Spine-style Inspector: **Animation** dropdown, **Loop** (off = play once and hold the last pose, `StateCompleted` event), **Time Scale** — in edit mode picking an animation runs it right on the object, in Play mode it switches the real animation. No on-screen buttons.
3. **What to give the skill**: art (best: one PNG per part; flat images get split by the AI), the list of animations, a reference for the feel (GIF/video/Spine clip), target scene/object. Details in `AI-Skill/SKILL.md`.
4. Ask the agent in plain words: *"make this cat jump and land softly"*, *"chest opens and bounces"*. It follows `SKILL.md`: inspect → decompose into primitives → build → measure → report.
5. Or code it yourself: `AnimKit.PopIn(t); AnimKit.Bounce(cat, 0.5f); AnimKit.Keyframes(t, AnimLoopMode.Loop, …)` — see `AI-Skill/references/unity-animation-api.md` and `recipes.md`.

## Select an object → see its animations
Click any GameObject in the scene:
- **Scene view panel** (top-left): every animator on it, with a badge — `LOOP 1.43s`, `ONCE`, `CONTINUOUS` (random / reactive), `OFF` — plus what it moves, weight and speed, and **Play preview / Stop** (restores the exact pose; stops automatically on save, Play mode and recompile).
- **Inspector header** on each animator: type, loop badge, cycle, target, weight.
- **Hierarchy label**: `loop 2 · once 1` next to objects that have animators.

## Library in one paragraph
Every animator adds an offset on top of the rest pose (delta based), so many can stack on one object; disabled = zero cost and pose restored. Primitives: oscillation (Sine, Swing, Breathing, Floating), phased cycles (WalkCycle, Flap, chain waves for tail/hair), one-shots (Tween, PoseSequence), hop (Jump with squash/stretch), path (Orbit), spring follow-through (SecondaryMotion), face (Blink, FacialExpression, ExpressionPulse). Anything irregular = keyframes. Periodic motion is exactly loopable and bakeable (`ClipBaker`).

## Test scene
`Samples/MermaidTest.unity` — the user's mermaid sprite sheet split into base / tail fin / face patches, animated with `AnimKit.Breathe`, `Swing` + `Wobble` (fin), `BlinkAnimator` (face).

## Known limits
- Per-part motion needs separate sprites/Transforms with pivots at joints (the skill explains how to split flat art and what to ask the artist for).
- Animating a Transform that other code overwrites absolutely each frame drifts — animate a child.
- Blink, sprite swaps and alpha are live-only (not baked).
- Not yet verified in an actual Luna build.
