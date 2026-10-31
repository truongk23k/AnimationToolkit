using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Bay lượn theo quỹ đạo elip / số 8 (Lissajous): chim bay vòng, bướm lượn, vật thể lơ lửng quanh điểm.
    /// bank nghiêng thân theo hướng bay nên chuyển động có quán tính thay vì trượt phẳng.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Orbit Animator")]
    public class OrbitAnimator : OffsetAnimator
    {
        [Tooltip("Orbit radius on X and Y (local units).")]
        [SerializeField] Vector2 radius = new Vector2(1.5f, 0.6f);
        [Tooltip("Orbits per second.")]
        [SerializeField, Min(0f)] float frequency = 0.25f;
        [Tooltip("Y frequency multiplier. 1 = ellipse, 2 = figure eight.")]
        [SerializeField, Range(1, 4)] int yHarmonic = 1;
        [Tooltip("Phase offset in cycles.")]
        [SerializeField] float phase;
        [Tooltip("Roll in degrees following horizontal velocity (banking).")]
        [SerializeField] float bank = 8f;

        public void Configure(Vector2 r, float freq)
        {
            radius = r; frequency = freq;
        }

        public override float CycleDuration { get { return frequency > 0.0001f ? 1f / frequency : 0f; } }
        public override string Describe() { return "Orbit · " + (yHarmonic == 2 ? "figure-8" : "ellipse") + " " + radius.x.ToString("0.##") + "×" + radius.y.ToString("0.##") + " @ " + frequency.ToString("0.##") + " Hz"; }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            float a = 2f * Mathf.PI * (t * frequency + phase);
            float x = radius.x * Mathf.Sin(a);
            float y = radius.y * Mathf.Sin(a * yHarmonic + Mathf.PI * 0.5f * (yHarmonic == 1 ? 1f : 0f));
            pose.position = new Vector3(x, y, 0f);
            // Vận tốc ngang ∝ cos(a): nghiêng về phía đang bay tới
            pose.rotation = Quaternion.Euler(0f, 0f, -bank * Mathf.Cos(a));
        }

        public override void SnapToLoop(float duration) { frequency = SnapFrequency(frequency, duration); }
    }
}
