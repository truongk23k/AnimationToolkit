using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Lắc kiểu con lắc quanh pivot của Transform (đặt pivot ở khớp: vai, cổ, gốc đuôi...).
    /// damping = 0: lắc đều mãi. damping &gt; 0 kèm repeatInterval: lắc tắt dần rồi "kick" lại — rất hợp vẫy tay.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Swing Animator")]
    public class SwingAnimator : OffsetAnimator
    {
        [Tooltip("Max swing angle in degrees.")]
        [SerializeField] float angle = 10f;
        [Tooltip("Swings (full back-and-forth) per second.")]
        [SerializeField, Min(0f)] float swingSpeed = 0.5f;
        [Tooltip("Rotation axis. (0,0,1) for 2D.")]
        [SerializeField] Vector3 axis = new Vector3(0f, 0f, 1f);
        [Tooltip("Decay per second after each kick. 0 = no decay.")]
        [SerializeField, Min(0f)] float damping;
        [Tooltip("With damping: seconds between kicks. 0 = single swing that fades out.")]
        [SerializeField, Min(0f)] float repeatInterval;
        [Tooltip("Rest angle the swing oscillates around (degrees).")]
        [SerializeField] float centerAngle;

        public void Configure(float angleDeg, float speedHz, float dampingPerSec, float interval)
        {
            angle = angleDeg; swingSpeed = speedHz; damping = dampingPerSec; repeatInterval = interval;
        }

        bool Decaying { get { return damping > 0f; } }
        public override bool IsLooping { get { return !Decaying || repeatInterval > 0.01f; } }
        public override float CycleDuration
        {
            get { return Decaying && repeatInterval > 0.01f ? repeatInterval : (swingSpeed > 0.0001f ? 1f / swingSpeed : 0f); }
        }
        public override string Describe()
        {
            string tail = !Decaying ? "" : (repeatInterval > 0.01f ? " · decays, re-kick every " + repeatInterval.ToString("0.#") + "s" : " · single swing, fades out");
            return "Swing · ±" + angle.ToString("0.#") + "° @ " + swingSpeed.ToString("0.##") + " Hz" + tail;
        }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            float local = t;
            if (damping > 0f && repeatInterval > 0.01f) local = t - repeatInterval * Mathf.Floor(t / repeatInterval);
            float env = damping > 0f ? Mathf.Exp(-damping * local) : 1f;
            float a = centerAngle + angle * env * Mathf.Sin(2f * Mathf.PI * swingSpeed * local);
            pose.rotation = Quaternion.AngleAxis(a, axis.sqrMagnitude < 0.0001f ? Vector3.forward : axis.normalized);
        }

        public override void SnapToLoop(float duration) { swingSpeed = SnapFrequency(swingSpeed, duration); }
    }
}
