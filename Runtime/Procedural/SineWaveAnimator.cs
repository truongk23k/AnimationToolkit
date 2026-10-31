using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>Chuyển động tuần hoàn mượt bằng sóng sin: vị trí, xoay, scale (mỗi trục độc lập).</summary>
    [AddComponentMenu("Animation Toolkit/Sine Wave Animator")]
    public class SineWaveAnimator : OffsetAnimator
    {
        [Header("Wave")]
        [Tooltip("Cycles per second.")]
        [SerializeField, Min(0f)] float frequency = 0.5f;
        [Tooltip("Phase offset in cycles (0..1). Use on sibling parts to desync them.")]
        [SerializeField] float phase;

        [Header("Amplitude")]
        [Tooltip("Local position amplitude per axis.")]
        [SerializeField] Vector3 positionAmplitude = new Vector3(0f, 0.05f, 0f);
        [Tooltip("Rotation amplitude in degrees per axis (Z for 2D).")]
        [SerializeField] Vector3 rotationAmplitude;
        [Tooltip("Scale amplitude per axis, added to 1.")]
        [SerializeField] Vector3 scaleAmplitude;

        public Vector3 ScaleAmplitude { get { return scaleAmplitude; } set { scaleAmplitude = value; } }

        public void Configure(Vector3 posAmp, float rotZ, float freq, float phaseCycles)
        {
            positionAmplitude = posAmp; rotationAmplitude = new Vector3(0, 0, rotZ);
            frequency = freq; phase = phaseCycles;
        }

        public override float CycleDuration { get { return frequency > 0.0001f ? 1f / frequency : 0f; } }
        public override string Describe()
        {
            string what = (positionAmplitude != Vector3.zero ? "pos " : "") + (rotationAmplitude != Vector3.zero ? "rot " : "") + (scaleAmplitude != Vector3.zero ? "scale " : "");
            return "Sine wave · " + what + "· " + frequency.ToString("0.##") + " Hz";
        }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            float s = Cycle(t, frequency, phase);
            pose.position = positionAmplitude * s;
            pose.rotation = Quaternion.Euler(rotationAmplitude * s);
            pose.scale = Vector3.one + scaleAmplitude * s;
        }

        public override void SnapToLoop(float duration) { frequency = SnapFrequency(frequency, duration); }
    }
}
