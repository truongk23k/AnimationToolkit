using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Tween offset vị trí / xoay / scale / alpha với easing và loop mode (Once, Loop, PingPong).
    /// Dùng cho chuyển động một lần (pop-in, nhảy, fade) hoặc lặp đơn giản.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Tween Animator")]
    public class TweenAnimator : OffsetAnimator
    {
        [Header("Motion (offset at the end of the tween)")]
        [SerializeField] Vector3 positionOffset;
        [Tooltip("Euler degrees added at the end of the tween.")]
        [SerializeField] Vector3 rotationOffset;
        [Tooltip("Scale multiplier reached at the end of the tween. (1,1,1) = no change.")]
        [SerializeField] Vector3 scaleMultiplier = Vector3.one;

        [Header("Alpha (SpriteRenderer on target)")]
        [SerializeField] bool animateAlpha;
        [SerializeField, Range(0f, 1f)] float alphaFrom = 1f;
        [SerializeField, Range(0f, 1f)] float alphaTo = 0f;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] float duration = 0.5f;
        [SerializeField, Min(0f)] float delay;
        [SerializeField] EaseType ease = EaseType.SineInOut;
        [SerializeField] AnimLoopMode loopMode = AnimLoopMode.PingPong;

        [Tooltip("Once: keep the end pose after finishing. If false, return to start.")]
        [SerializeField] bool holdEndPose = true;

        SpriteRenderer _sprite;
        float _baseAlpha = 1f;
        bool _completedRaised;

        public event Action Completed;

        public bool IsFinished { get { return loopMode == AnimLoopMode.Once && _completedRaised; } }

        public void Configure(Vector3 pos, Vector3 rot, Vector3 scaleMul, float dur, EaseType easing, AnimLoopMode mode)
        {
            positionOffset = pos; rotationOffset = rot; scaleMultiplier = scaleMul;
            duration = Mathf.Max(0.01f, dur); ease = easing; loopMode = mode;
        }

        /// <summary>Chạy lại từ đầu (dùng cho one-shot).</summary>
        public void Play()
        {
            time = 0f;
            _completedRaised = false;
            enabled = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _sprite = Target.GetComponent<SpriteRenderer>();
            if (_sprite != null) _baseAlpha = _sprite.color.a;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            RestoreAlpha();
        }

        public override void ResetState()
        {
            base.ResetState();
            _completedRaised = false;
            RestoreAlpha();
        }

        public override void ClearOffset()
        {
            base.ClearOffset();
            RestoreAlpha();
        }

        void RestoreAlpha()
        {
            if (_sprite == null || !animateAlpha) return;
            Color c = _sprite.color; c.a = _baseAlpha; _sprite.color = c;
        }

        public override bool IsLooping { get { return loopMode != AnimLoopMode.Once; } }
        public override float CycleDuration { get { return loopMode == AnimLoopMode.PingPong ? duration * 2f : duration; } }
        public override string Describe()
        {
            return "Tween · " + loopMode + " · " + duration.ToString("0.##") + "s · " + ease + (animateAlpha ? " · alpha" : "");
        }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            bool finished;
            float raw = AnimEasing.Progress(t - delay, duration, loopMode, out finished);
            float k = AnimEasing.Evaluate(ease, raw);
            if (finished && !holdEndPose) k = 0f;

            pose.position = positionOffset * k;
            pose.rotation = Quaternion.Euler(rotationOffset * k);
            pose.scale = Vector3.Lerp(Vector3.one, scaleMultiplier, k);

            if (animateAlpha && _sprite != null)
            {
                Color c = _sprite.color;
                c.a = Mathf.Lerp(_baseAlpha, Mathf.Lerp(alphaFrom, alphaTo, k), Weight);
                _sprite.color = c;
            }

            if (finished && !_completedRaised && t - delay > 0f)
            {
                _completedRaised = true;
                if (Completed != null) Completed();
            }
        }
    }
}
