using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    [Serializable]
    public class FlapWing
    {
        [Tooltip("Wing Transform. Pivot must be at the shoulder joint, wing drawn pointing outward.")]
        public Transform wing;
        [Tooltip("+1 for a wing on the +X side, -1 for the mirrored wing on the -X side.")]
        public float side = 1f;
    }

    /// <summary>
    /// Vỗ cánh: hạ cánh nhanh (lực đẩy), nâng cánh chậm hơn và gập cánh lại; thân nhấp nhô theo nhịp vỗ.
    /// Dùng cho chim, bướm, dơi, thiên thần. Kết hợp OrbitAnimator để bay lượn.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Flap Animator")]
    public class FlapAnimator : ToolkitAnimator
    {
        [SerializeField] FlapWing[] wings;
        [Tooltip("Body that bobs with the flaps (optional).")]
        [SerializeField] Transform body;

        [Header("Flap")]
        [Tooltip("Flaps (down+up) per second.")]
        [SerializeField, Min(0f)] float flapsPerSecond = 3f;
        [Tooltip("Wing angle at the top of the stroke, degrees.")]
        [SerializeField] float upAngle = 40f;
        [Tooltip("Wing angle at the bottom of the stroke, degrees.")]
        [SerializeField] float downAngle = -40f;
        [Tooltip("Fraction of the cycle spent on the downstroke. Below 0.5 = fast power stroke, slow recovery.")]
        [SerializeField, Range(0.2f, 0.8f)] float downstrokeRatio = 0.4f;
        [Tooltip("Wing length scale during the upstroke (1 = no folding).")]
        [SerializeField, Range(0.3f, 1f)] float foldScale = 0.75f;

        [Header("Body")]
        [Tooltip("Vertical bob of the body (local units).")]
        [SerializeField] float bodyBob = 0.08f;
        [Tooltip("Body tilts back on the downstroke (degrees).")]
        [SerializeField] float bodyTilt = 3f;

        OffsetChannel[] _wingCh;
        OffsetChannel _bodyCh;

        public override float CycleDuration { get { return flapsPerSecond > 0.0001f ? 1f / flapsPerSecond : 0f; } }
        public override string Describe() { return "Flap · " + (wings != null ? wings.Length : 0) + " wings · " + flapsPerSecond.ToString("0.##") + "/s · " + (upAngle - downAngle).ToString("0") + "° range"; }

        public FlapWing[] Wings { get { return wings; } set { ClearOffset(); wings = value; _wingCh = null; } }
        public Transform Body { get { return body; } set { body = value; _bodyCh = null; } }

        public void Configure(float flaps, float range, float bob)
        {
            flapsPerSecond = flaps; upAngle = range * 0.5f; downAngle = -range * 0.5f; bodyBob = bob;
        }

        /// <summary>Đặt góc đỉnh/đáy nhịp vỗ riêng (không đối xứng quanh pose gốc). Góc là offset từ pose gốc, nhân với side của từng cánh.</summary>
        public void SetAngles(float topOfStroke, float bottomOfStroke)
        {
            upAngle = topOfStroke; downAngle = bottomOfStroke;
        }

        void Ensure()
        {
            if (_wingCh != null) return;
            int n = wings != null ? wings.Length : 0;
            _wingCh = new OffsetChannel[n];
            for (int i = 0; i < n; i++) { _wingCh[i] = new OffsetChannel(); _wingCh[i].SetTarget(wings[i].wing); }
            _bodyCh = new OffsetChannel(); _bodyCh.SetTarget(body);
        }

        protected override void Evaluate(float t, float dt, float w)
        {
            Ensure();
            float p = t * flapsPerSecond;
            p -= Mathf.Floor(p);

            float angle, fold = 1f, stroke;
            if (p < downstrokeRatio)
            {
                float u = p / downstrokeRatio;
                stroke = AnimEasing.Evaluate(EaseType.SineInOut, u);
                angle = Mathf.Lerp(upAngle, downAngle, stroke);
            }
            else
            {
                float u = (p - downstrokeRatio) / (1f - downstrokeRatio);
                stroke = 1f - AnimEasing.Evaluate(EaseType.SineInOut, u);
                angle = Mathf.Lerp(upAngle, downAngle, stroke);
                fold = 1f - (1f - foldScale) * Mathf.Sin(Mathf.PI * u); // gập nhất giữa nhịp nâng cánh
            }

            for (int i = 0; i < _wingCh.Length; i++)
            {
                AnimPose pose = AnimPose.Identity;
                pose.rotation = Quaternion.Euler(0f, 0f, angle * wings[i].side);
                pose.scale = new Vector3(fold, 1f, 1f);
                _wingCh[i].Apply(pose, w);
            }

            // Thân nâng lên khi cánh đang ở dưới (đà), nghiêng nhẹ theo nhịp
            AnimPose b = AnimPose.Identity;
            float lift = stroke; // 0 ở đỉnh nhịp, 1 ở đáy nhịp
            b.position = new Vector3(0f, bodyBob * (lift - 0.5f), 0f);
            b.rotation = Quaternion.Euler(0f, 0f, bodyTilt * (lift - 0.5f));
            _bodyCh.Apply(b, w);
        }

        public override void ClearOffset()
        {
            if (_wingCh == null) return;
            for (int i = 0; i < _wingCh.Length; i++) _wingCh[i].Clear();
            _bodyCh.Clear();
        }

        public override void GetAffectedTransforms(List<Transform> results)
        {
            if (wings != null)
                for (int i = 0; i < wings.Length; i++)
                    if (wings[i].wing != null) results.Add(wings[i].wing);
            if (body != null) results.Add(body);
        }

        public override void SnapToLoop(float duration) { flapsPerSecond = SnapFrequency(flapsPerSecond, duration); }
    }
}
