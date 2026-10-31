using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    [Serializable]
    public class WalkLeg
    {
        [Tooltip("Leg Transform. Pivot should be at the hip/shoulder.")]
        public Transform leg;
        [Tooltip("Gait phase in cycles (0..1). Opposite legs differ by 0.5.")]
        public float phase;
    }

    /// <summary>
    /// Chu kỳ đi/chạy tại chỗ (game tự di chuyển object): mỗi chân đung đưa quanh khớp theo pha lệch,
    /// chân nhấc lên ở nửa bước tiến, thân nhấp nhô 2 lần/chu kỳ và nghiêng nhẹ. Hợp 2 chân (người) hoặc 4 chân (mèo, chó).
    /// Chạy = tăng stepsPerSecond và swingDegrees.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Walk Cycle Animator")]
    public class WalkCycleAnimator : ToolkitAnimator
    {
        [SerializeField] WalkLeg[] legs;
        [Tooltip("Optional: body bobs up/down and tilts.")]
        [SerializeField] Transform body;
        [Tooltip("Optional: head counter-bobs a little later than the body.")]
        [SerializeField] Transform head;

        [Header("Gait")]
        [Tooltip("Full step cycles per second (one cycle = every leg steps once).")]
        [SerializeField, Min(0f)] float stepsPerSecond = 1.5f;
        [Tooltip("Max leg swing in degrees.")]
        [SerializeField] float swingDegrees = 28f;
        [Tooltip("Foot lift while a leg swings forward (local units).")]
        [SerializeField] float liftHeight = 0.05f;
        [Tooltip("1 = swing forward is positive rotation (facing left in 2D), -1 flips direction.")]
        [SerializeField] float direction = 1f;

        [Header("Body")]
        [SerializeField] float bodyBob = 0.04f;
        [SerializeField] float bodyTiltDegrees = 2f;
        [SerializeField] float headBob = 0.015f;

        OffsetChannel[] _legCh;
        OffsetChannel _bodyCh, _headCh;

        public override float CycleDuration { get { return stepsPerSecond > 0.0001f ? 1f / stepsPerSecond : 0f; } }
        public override string Describe() { return "Walk cycle · " + (legs != null ? legs.Length : 0) + " legs · " + stepsPerSecond.ToString("0.##") + " steps/s · swing " + swingDegrees.ToString("0") + "°"; }

        public WalkLeg[] Legs { get { return legs; } set { ClearOffset(); legs = value; _legCh = null; } }
        public Transform Body { get { return body; } set { body = value; _bodyCh = null; } }

        public void Configure(float steps, float swing, float bob)
        {
            stepsPerSecond = steps; swingDegrees = swing; bodyBob = bob; liftHeight = bob * 1.2f;
        }

        /// <summary>Tự gán pha: 2 chân đối nhau; 4 chân kiểu đi chéo (trước-trái, trước-phải, sau-trái, sau-phải).</summary>
        public static WalkLeg[] MakeLegs(Transform[] transforms)
        {
            int n = transforms.Length;
            WalkLeg[] r = new WalkLeg[n];
            for (int i = 0; i < n; i++)
            {
                float ph = (i % 2) * 0.5f;
                if (n == 4) ph = (i == 0 || i == 3) ? 0f : 0.5f;
                r[i] = new WalkLeg { leg = transforms[i], phase = ph };
            }
            return r;
        }

        void Ensure()
        {
            if (_legCh != null) return;
            int n = legs != null ? legs.Length : 0;
            _legCh = new OffsetChannel[n];
            for (int i = 0; i < n; i++) { _legCh[i] = new OffsetChannel(); _legCh[i].SetTarget(legs[i].leg); }
            _bodyCh = new OffsetChannel(); _bodyCh.SetTarget(body);
            _headCh = new OffsetChannel(); _headCh.SetTarget(head);
        }

        protected override void Evaluate(float t, float dt, float w)
        {
            Ensure();
            float ph = t * stepsPerSecond;
            float dir = direction >= 0f ? 1f : -1f;

            for (int i = 0; i < _legCh.Length; i++)
            {
                float a = 2f * Mathf.PI * (ph + legs[i].phase);
                AnimPose p = AnimPose.Identity;
                p.rotation = Quaternion.Euler(0f, 0f, dir * swingDegrees * Mathf.Sin(a));
                p.position = new Vector3(0f, liftHeight * Mathf.Max(0f, Mathf.Cos(a)), 0f);
                _legCh[i].Apply(p, w);
            }

            AnimPose b = AnimPose.Identity;
            b.position = new Vector3(0f, bodyBob * Mathf.Cos(4f * Mathf.PI * ph), 0f);
            b.rotation = Quaternion.Euler(0f, 0f, bodyTiltDegrees * Mathf.Sin(2f * Mathf.PI * ph));
            _bodyCh.Apply(b, w);

            AnimPose h = AnimPose.Identity;
            h.position = new Vector3(0f, headBob * Mathf.Cos(4f * Mathf.PI * ph - 0.9f), 0f);
            _headCh.Apply(h, w);
        }

        public override void ClearOffset()
        {
            if (_legCh == null) return;
            for (int i = 0; i < _legCh.Length; i++) _legCh[i].Clear();
            _bodyCh.Clear(); _headCh.Clear();
        }

        public override void GetAffectedTransforms(List<Transform> results)
        {
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i].leg != null) results.Add(legs[i].leg);
            if (body != null) results.Add(body);
            if (head != null) results.Add(head);
        }

        public override void SnapToLoop(float duration) { stepsPerSecond = SnapFrequency(stepsPerSecond, duration); }
    }
}
