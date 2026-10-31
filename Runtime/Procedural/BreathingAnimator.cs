using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Nhịp thở tự nhiên: hít (ease in-out) rồi thở ra chậm hơn, scale nhẹ + nhấc nhẹ.
    /// preserveVolume giảm scale X khi Y phình để có cảm giác squash &amp; stretch thay vì phồng đều.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Breathing Animator")]
    public class BreathingAnimator : OffsetAnimator
    {
        [Tooltip("Full breaths (in + out) per second. 0.25 = one breath every 4 seconds.")]
        [SerializeField, Min(0f)] float breathsPerSecond = 0.25f;
        [Tooltip("Fraction of the cycle spent inhaling. Under 0.5 = quick inhale, slow exhale.")]
        [SerializeField, Range(0.1f, 0.9f)] float inhaleRatio = 0.4f;
        [Tooltip("Scale added at peak inhale (X, Y).")]
        [SerializeField] Vector2 scaleAmount = new Vector2(0.01f, 0.03f);
        [Tooltip("Local position added at peak inhale.")]
        [SerializeField] Vector3 positionAmount = new Vector3(0f, 0.01f, 0f);
        [Tooltip("Shrink X slightly as Y grows (keeps apparent volume).")]
        [SerializeField] bool preserveVolume = true;
        [Tooltip("Scale pivot is the Transform origin; put it at the chest/base of the body.")]
        [SerializeField] float phase;

        public void Configure(float scaleY, float breathsPerSec)
        {
            scaleAmount = new Vector2(scaleY * 0.35f, scaleY);
            positionAmount = new Vector3(0f, scaleY * 0.4f, 0f);
            breathsPerSecond = breathsPerSec;
        }

        public override float CycleDuration { get { return breathsPerSecond > 0.0001f ? 1f / breathsPerSecond : 0f; } }
        public override string Describe() { return "Breathing · " + (breathsPerSecond * 60f).ToString("0") + "/min · scale " + (scaleAmount.y * 100f).ToString("0.#") + "%"; }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            float p = t * breathsPerSecond + phase;
            p -= Mathf.Floor(p);
            float b;
            if (p < inhaleRatio) b = AnimEasing.Evaluate(EaseType.SineInOut, p / inhaleRatio);
            else b = AnimEasing.Evaluate(EaseType.SineInOut, 1f - (p - inhaleRatio) / (1f - inhaleRatio));

            float sx = preserveVolume ? -scaleAmount.y * 0.3f : scaleAmount.x;
            pose.scale = new Vector3(1f + sx * b, 1f + scaleAmount.y * b, 1f);
            pose.position = positionAmount * b;
        }

        public override void SnapToLoop(float duration) { breathsPerSecond = SnapFrequency(breathsPerSecond, duration); }
    }
}
