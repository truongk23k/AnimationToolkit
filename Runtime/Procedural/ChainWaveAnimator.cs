using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    [Serializable]
    public class WaveChain
    {
        [Tooltip("Segments from root to tip. Each should be a child of the previous one.")]
        public Transform[] segments;
        [Tooltip("Phase offset of this chain in cycles.")]
        public float phase;
        [Tooltip("Amplitude multiplier for this chain.")]
        public float amplitudeScale = 1f;
    }

    /// <summary>
    /// Sóng lan truyền dọc chuỗi xương (đuôi, tóc, dây...): mỗi đốt xoay sin, đốt sau trễ pha hơn đốt trước,
    /// biên độ tăng dần về phía ngọn. Hoàn toàn thủ tục nên loop khép kín và bake được thành AnimationClip.
    /// </summary>
    public abstract class ChainWaveAnimator : ToolkitAnimator
    {
        [Header("Chains")]
        [SerializeField] protected WaveChain[] chains = new WaveChain[1] { new WaveChain() };

        [Header("Wave")]
        [Tooltip("Max rotation per segment in degrees (before the root→tip gain).")]
        [SerializeField] protected float amplitude = 15f;
        [Tooltip("Wave cycles per second.")]
        [SerializeField, Min(0f)] protected float frequency = 0.6f;
        [Tooltip("Phase lag between consecutive segments, in cycles. 0.1-0.2 looks like a travelling wave.")]
        [SerializeField] protected float phaseDelayPerSegment = 0.12f;
        [Tooltip("Amplitude multiplier at the root segment.")]
        [SerializeField] protected float rootGain = 0.4f;
        [Tooltip("Amplitude multiplier at the tip segment.")]
        [SerializeField] protected float tipGain = 1.4f;
        [Tooltip("Constant curl in degrees per segment (static bend, adds to the wave).")]
        [SerializeField] protected float bias;
        [SerializeField] protected Vector3 rotationAxis = new Vector3(0f, 0f, 1f);

        OffsetChannel[][] _channels;

        public WaveChain[] Chains { get { return chains; } set { chains = value; _channels = null; } }

        public override float CycleDuration { get { return frequency > 0.0001f ? 1f / frequency : 0f; } }
        public override string Describe()
        {
            int segs = 0;
            if (chains != null) for (int i = 0; i < chains.Length; i++) if (chains[i] != null && chains[i].segments != null) segs += chains[i].segments.Length;
            return GetType().Name.Replace("Animator", "") + " wave · " + (chains != null ? chains.Length : 0) + " chain(s), " + segs + " seg · " + amplitude.ToString("0.#") + "°/seg @ " + frequency.ToString("0.##") + " Hz";
        }

        public void Configure(float amplitudeDeg, float freq, float phaseDelay)
        {
            amplitude = amplitudeDeg; frequency = freq; phaseDelayPerSegment = phaseDelay;
        }

        /// <summary>Gọi khi đổi mảng segments lúc runtime.</summary>
        public void RebuildChannels()
        {
            ClearOffset();
            _channels = null;
        }

        protected virtual float Envelope(float dt) { return 1f; }
        protected virtual float ChainPhase(int index) { return chains[index].phase; }

        void EnsureChannels()
        {
            if (_channels != null) return;
            int n = chains != null ? chains.Length : 0;
            _channels = new OffsetChannel[n][];
            for (int c = 0; c < n; c++)
            {
                Transform[] segs = chains[c] != null ? chains[c].segments : null;
                int m = segs != null ? segs.Length : 0;
                _channels[c] = new OffsetChannel[m];
                for (int i = 0; i < m; i++)
                {
                    _channels[c][i] = new OffsetChannel();
                    _channels[c][i].SetTarget(segs[i]);
                }
            }
        }

        protected override void Evaluate(float t, float dt, float w)
        {
            EnsureChannels();
            float env = Envelope(dt);
            Vector3 axis = rotationAxis.sqrMagnitude < 0.0001f ? Vector3.forward : rotationAxis.normalized;

            for (int c = 0; c < _channels.Length; c++)
            {
                OffsetChannel[] ch = _channels[c];
                int n = ch.Length;
                for (int i = 0; i < n; i++)
                {
                    float u = n > 1 ? (float)i / (n - 1) : 1f;
                    float gain = Mathf.Lerp(rootGain, tipGain, u);
                    // Sóng đi từ gốc ra ngọn: đốt sau trễ pha
                    float s = Cycle(t, frequency, ChainPhase(c) - i * phaseDelayPerSegment);
                    float a = bias + amplitude * chains[c].amplitudeScale * gain * env * s;

                    AnimPose pose = AnimPose.Identity;
                    pose.rotation = Quaternion.AngleAxis(a, axis);
                    ch[i].Apply(pose, w);
                }
            }
        }

        public override void ClearOffset()
        {
            if (_channels == null) return;
            for (int c = 0; c < _channels.Length; c++)
                for (int i = 0; i < _channels[c].Length; i++)
                    _channels[c][i].Clear();
        }

        public override void GetAffectedTransforms(List<Transform> results)
        {
            if (chains == null) return;
            for (int c = 0; c < chains.Length; c++)
            {
                if (chains[c] == null || chains[c].segments == null) continue;
                for (int i = 0; i < chains[c].segments.Length; i++)
                    if (chains[c].segments[i] != null) results.Add(chains[c].segments[i]);
            }
        }

        public override void SnapToLoop(float duration) { frequency = SnapFrequency(frequency, duration); }
    }
}
