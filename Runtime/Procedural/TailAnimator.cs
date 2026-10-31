using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Đuôi nhiều đốt với sóng lan truyền + vây đuôi (fin) trễ pha thêm một nhịp.
    /// Idle: vẫy nhẹ liên tục. Gọi Wag() để vẫy mạnh trong vài giây rồi êm về lại idle.
    /// Cần đuôi được tách thành các đốt riêng (mỗi đốt là Transform con của đốt trước).
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Tail Animator")]
    public class TailAnimator : ChainWaveAnimator
    {
        [Header("Tail Fin (optional)")]
        [Tooltip("Fin at the end of the tail. Leave empty if the artwork has no separate fin.")]
        [SerializeField] Transform fin;
        [SerializeField] float finAmplitude = 25f;
        [Tooltip("Extra phase lag of the fin behind the last segment, in cycles.")]
        [SerializeField] float finDelay = 0.15f;

        [Header("Wag Burst")]
        [Tooltip("Amplitude/frequency multiplier at the peak of Wag().")]
        [SerializeField, Min(1f)] float wagMultiplier = 2f;

        readonly OffsetChannel _finChannel = new OffsetChannel();
        float _wagTime = -1f, _wagDuration;

        public Transform Fin { get { return fin; } set { fin = value; } }

        /// <summary>Vẫy mạnh trong duration giây (envelope mượt vào/ra).</summary>
        public void Wag(float duration)
        {
            _wagTime = 0f;
            _wagDuration = Mathf.Max(0.1f, duration);
        }

        protected override float Envelope(float dt)
        {
            if (_wagTime < 0f) return 1f;
            _wagTime += Mathf.Abs(dt);
            if (_wagTime >= _wagDuration) { _wagTime = -1f; return 1f; }
            float k = Mathf.Sin(Mathf.PI * (_wagTime / _wagDuration)); // 0 → 1 → 0
            return Mathf.Lerp(1f, wagMultiplier, k);
        }

        protected override void Evaluate(float t, float dt, float w)
        {
            base.Evaluate(t, dt, w);
            if (fin == null) { _finChannel.SetTarget(null); return; }

            _finChannel.SetTarget(fin);
            int n = 0;
            if (chains != null && chains.Length > 0 && chains[0] != null && chains[0].segments != null)
                n = chains[0].segments.Length;
            float s = Cycle(t, frequency, -(n * phaseDelayPerSegment) - finDelay);
            AnimPose pose = AnimPose.Identity;
            Vector3 axis = rotationAxis.sqrMagnitude < 0.0001f ? Vector3.forward : rotationAxis.normalized;
            pose.rotation = Quaternion.AngleAxis(finAmplitude * s, axis);
            _finChannel.Apply(pose, w);
        }

        public override void ResetState()
        {
            base.ResetState();
            _wagTime = -1f;
        }

        public override void ClearOffset()
        {
            base.ClearOffset();
            _finChannel.Clear();
        }

        public override void GetAffectedTransforms(List<Transform> results)
        {
            base.GetAffectedTransforms(results);
            if (fin != null) results.Add(fin);
        }

        void Reset()
        {
            amplitude = 10f; frequency = 0.7f; phaseDelayPerSegment = 0.12f; rootGain = 0.4f; tipGain = 1.1f;
        }
    }
}
