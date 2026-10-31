# Performance rules (playable ads / WebGL / Luna)

- One `LateUpdate` per **enabled** animator. Disable (`enabled=false`) or `FadeOut(…, true)` what you don't need; disabled animators cost nothing and restore the pose.
- No allocations per frame: no LINQ, no `new` collections in Evaluate/Sample (the toolkit allocates only on first `EnsureChannels`).
- No coroutines, no reflection at runtime, no physics, no Newtonsoft. `ClipBaker` is Editor-only.
- Prefer a few chained segments (4–6) over many small independent animators.
- 30+ characters: stagger with `randomizeStart`, disable off-screen ones.
- `SecondaryMotionAnimator`: avoid on hundreds of objects; use Sine/Chain waves for ambient motion.
- Prefer live procedural animators to baked clips in playables (no asset size, no Animator/Controller overhead). Bake only for hand-off or when a clip is required.
- Avoid scaling animations on objects with many children if the children use colliders (scale changes dirty physics transforms).
- Luna/Bridge.NET: stick to plain C# in `Runtime/` (no `in`/`ref readonly`, no pattern matching, no local functions). Editor folder is excluded from export; if a Luna build fails on a compile error, fix it even in Editor code.
- To preview in edit mode, call `Tick(dt)` on the animators yourself, then `ClearOffset()` on each and restore the snapshotted local poses; never leave offsets applied to the saved scene.
