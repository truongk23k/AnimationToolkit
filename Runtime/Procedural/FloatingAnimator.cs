using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>Lơ lửng tự nhiên: dọc là sóng sin + hài bậc 2 nhẹ, ngang elip lệch pha, nghiêng nhẹ theo chuyển động. Loop khép kín.</summary>
    [AddComponentMenu("Animation Toolkit/Floating Animator")]
    public class FloatingAnimator : OffsetAnimator
    {
        [SerializeField, Min(0f)] float frequency = 0.3f;
        [Tooltip("Vertical bob distance.")]
        [SerializeField] float verticalAmplitude = 0.08f;
        [Tooltip("Horizontal drift distance (0 = pure vertical hover).")]
        [SerializeField] float horizontalAmplitude = 0.03f;
        [Tooltip("Tilt in degrees following the horizontal drift.")]
        [SerializeField] float tiltDegrees = 2f;
        [SerializeField] float phase;

        public void Configure(float vertical, float freq)
        {
            verticalAmplitude = vertical; frequency = freq; horizontalAmplitude = vertical * 0.4f;
        }

        public override float CycleDuration { get { return frequency > 0.0001f ? 1f / frequency : 0f; } }
        public override string Describe() { return "Floating · ±" + verticalAmplitude.ToString("0.##") + " @ " + frequency.ToString("0.##") + " Hz"; }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            // Hài bậc 2 làm nhịp lên/xuống không đều như máy
            float y = Cycle(t, frequency, phase) + 0.25f * Cycle(t, frequency * 2f, phase + 0.1f);
            float x = Cycle(t, frequency, phase + 0.25f);
            pose.position = new Vector3(x * horizontalAmplitude, y * verticalAmplitude / 1.15f, 0f);
            pose.rotation = Quaternion.Euler(0f, 0f, -x * tiltDegrees);
        }

        public override void SnapToLoop(float duration) { frequency = SnapFrequency(frequency, duration); }
    }
}
