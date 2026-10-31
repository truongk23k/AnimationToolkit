# Secondary motion & follow-through

`SecondaryMotionAnimator` simulates a damped spring that chases a source Transform (default: parent).
The lag vector is applied as an offset, so a part trails behind when the parent accelerates, overshoots, and settles.

## Tuning
| Want | stiffness | damping ratio |
|---|---|---|
| Floppy ear / antenna | 60–100 | 0.2–0.3 |
| Hair / ribbon | 100–160 | 0.3–0.4 |
| Tight accessory | 200–300 | 0.5–0.7 |
| No overshoot, just lag | any | 1.0 |
`damping = 0` never settles — avoid.

## Rules
- It only reacts to **real motion** of the source (another animator, script or player drag). On a static parent it does nothing — pair with Hair/Tail wave for ambient motion.
- `source` must not be a child of the animated object (feedback loop).
- Teleports > 5 units snap instead of stretching.
- Influence: `positionInfluence` per axis (set Z to 0 in 2D), `rotationInfluence` for Z-rotation lag; clamp with `maxLag` / `maxAngleLag`.
- Substeps are fixed (≈8 ms) for stability; cost is O(steps) per frame — fine for tens of instances.
- Order matters when baking: parents must tick before children (GetComponentsInChildren order is parent first).
