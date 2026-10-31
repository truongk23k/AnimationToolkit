using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    public enum BlinkMode
    {
        [Tooltip("Swap sprites: open / half / closed.")] SpriteSwap,
        [Tooltip("Squash the eye Transforms on Y. Works without extra artwork if eyes are separate objects.")] ScaleY
    }

    /// <summary>
    /// Chớp mắt ngẫu nhiên. Cần mắt tách riêng: SpriteRenderer có sprite mở/nhắm, hoặc Transform mắt để squash.
    /// Có xác suất chớp đôi để trông tự nhiên. Không bake được (khoảng cách ngẫu nhiên).
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Blink Animator")]
    public class BlinkAnimator : ToolkitAnimator
    {
        [SerializeField] BlinkMode mode = BlinkMode.SpriteSwap;

        [Header("Sprite Swap")]
        [SerializeField] SpriteRenderer[] eyeRenderers;
        [SerializeField] Sprite openSprite;
        [Tooltip("Optional in-between frame (smoother). Leave empty to skip.")]
        [SerializeField] Sprite halfSprite;
        [SerializeField] Sprite closedSprite;

        [Header("Scale Y")]
        [SerializeField] Transform[] eyeTransforms;
        [SerializeField, Range(0f, 1f)] float closedScaleY = 0.08f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] float minInterval = 2.5f;
        [SerializeField, Min(0.1f)] float maxInterval = 5.5f;
        [Tooltip("Duration of one blink in seconds (close + open).")]
        [SerializeField, Min(0.05f)] float blinkDuration = 0.16f;
        [SerializeField, Range(0f, 1f)] float doubleBlinkChance = 0.15f;

        OffsetChannel[] _channels;
        float _nextBlink = -1f;
        float _lastT = -1f;
        float _blinkStart = -10f;
        int _closeness = -1;

        public override bool IsBakeable { get { return false; } }
        public override string Describe() { return "Blink · " + mode + " · random every " + minInterval.ToString("0.#") + "–" + maxInterval.ToString("0.#") + "s"; }

        public void Configure(float minInt, float maxInt)
        {
            minInterval = minInt; maxInterval = Mathf.Max(minInt, maxInt);
        }

        /// <summary>FacialExpressionController gọi khi đổi biểu cảm để mắt "mở" đúng sprite mới.</summary>
        public void SetOpenSprite(Sprite s)
        {
            openSprite = s;
            if (_closeness <= 0) ApplySprite(0f);
        }

        public void SetupSprites(SpriteRenderer[] renderers, Sprite open, Sprite half, Sprite closed)
        {
            eyeRenderers = renderers; openSprite = open; halfSprite = half; closedSprite = closed;
            mode = BlinkMode.SpriteSwap;
        }

        public void SetupScale(Transform[] eyes)
        {
            eyeTransforms = eyes; mode = BlinkMode.ScaleY; _channels = null;
        }

        public override void ResetState()
        {
            base.ResetState();
            _nextBlink = -1f; _lastT = -1f; _blinkStart = -10f;
        }

        protected override void Evaluate(float t, float dt, float w)
        {
            // Thời gian bị tua lùi (reset) → lên lịch lại
            if (t < _lastT || _nextBlink < 0f)
            {
                _nextBlink = t + Random.Range(minInterval, maxInterval);
                _blinkStart = -10f;
            }
            _lastT = t;

            if (t >= _nextBlink)
            {
                _blinkStart = t;
                bool dbl = Random.value < doubleBlinkChance;
                _nextBlink = t + (dbl ? blinkDuration * 1.6f : Random.Range(minInterval, maxInterval));
            }

            float u = (t - _blinkStart) / blinkDuration;
            float c = 0f;
            if (u >= 0f && u <= 1f)
            {
                // Khép nhanh (40%), mở chậm hơn (60%) — giống mắt thật
                c = u < 0.4f
                    ? AnimEasing.Evaluate(EaseType.QuadOut, u / 0.4f)
                    : 1f - AnimEasing.Evaluate(EaseType.SineInOut, (u - 0.4f) / 0.6f);
            }
            c *= w;

            if (mode == BlinkMode.ScaleY) ApplyScale(c);
            else ApplySprite(c);
        }

        void ApplyScale(float c)
        {
            if (_channels == null)
            {
                int n = eyeTransforms != null ? eyeTransforms.Length : 0;
                _channels = new OffsetChannel[n];
                for (int i = 0; i < n; i++) { _channels[i] = new OffsetChannel(); _channels[i].SetTarget(eyeTransforms[i]); }
            }
            AnimPose p = AnimPose.Identity;
            p.scale = new Vector3(1f, Mathf.Lerp(1f, closedScaleY, c), 1f);
            for (int i = 0; i < _channels.Length; i++) _channels[i].Apply(p, 1f);
        }

        void ApplySprite(float c)
        {
            if (eyeRenderers == null) return;
            int level = c > 0.66f ? 2 : (c > 0.33f && halfSprite != null ? 1 : 0);
            if (level == _closeness) return;
            _closeness = level;
            Sprite s = level == 2 ? closedSprite : (level == 1 ? halfSprite : openSprite);
            if (s == null) return;
            for (int i = 0; i < eyeRenderers.Length; i++)
                if (eyeRenderers[i] != null) eyeRenderers[i].sprite = s;
        }

        public override void ClearOffset()
        {
            if (_channels != null)
                for (int i = 0; i < _channels.Length; i++) _channels[i].Clear();
            if (_closeness > 0 && eyeRenderers != null && openSprite != null)
                for (int i = 0; i < eyeRenderers.Length; i++)
                    if (eyeRenderers[i] != null) eyeRenderers[i].sprite = openSprite;
            _closeness = -1;
        }

        public override void GetAffectedTransforms(List<Transform> results)
        {
            if (mode != BlinkMode.ScaleY || eyeTransforms == null) return;
            for (int i = 0; i < eyeTransforms.Length; i++)
                if (eyeTransforms[i] != null) results.Add(eyeTransforms[i]);
        }
    }
}
