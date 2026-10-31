using System;
using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Nhảy có đủ nhịp: chuẩn bị (ngồi xuống, squash) → bật lên (stretch) → bay theo parabol → tiếp đất (squash rồi nảy tắt dần).
    /// Đặt pivot ở chân để squash/stretch bám mặt đất. Dùng cho mèo, cá, nhân vật, vật phẩm bất kỳ.
    /// autoRepeat nhảy lặp (bake được); tắt autoRepeat rồi gọi Jump() để nhảy một lần theo yêu cầu.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Jump Animator")]
    public class JumpAnimator : OffsetAnimator
    {
        [Header("Trajectory")]
        [Tooltip("Peak height in local units.")]
        [SerializeField, Min(0f)] float height = 1.2f;
        [Tooltip("Seconds in the air.")]
        [SerializeField, Min(0.1f)] float airTime = 0.55f;
        [Tooltip("Horizontal distance covered during the air phase (local units). Keep 0 with autoRepeat or the object snaps back each loop.")]
        [SerializeField] Vector2 travel;

        [Header("Timing")]
        [Tooltip("Crouch before take-off (anticipation).")]
        [SerializeField, Min(0.02f)] float anticipation = 0.18f;
        [Tooltip("Landing recovery (squash + bounce settle).")]
        [SerializeField, Min(0.05f)] float landing = 0.3f;
        [SerializeField] bool autoRepeat = true;
        [Tooltip("Idle time between jumps when autoRepeat is on.")]
        [SerializeField, Min(0f)] float pause = 0.6f;

        [Header("Squash & Stretch")]
        [Tooltip("Crouch/impact squash amount (0.25 = 25% wider, 25% shorter).")]
        [SerializeField, Range(0f, 0.6f)] float squash = 0.22f;
        [Tooltip("Stretch while moving fast vertically.")]
        [SerializeField, Range(0f, 0.5f)] float stretch = 0.15f;

        float _start = -9999f;
        bool _landedRaised;

        public event Action Landed;

        public float Period { get { return anticipation + airTime + landing + pause; } }

        public override bool IsLooping { get { return autoRepeat; } }
        public override float CycleDuration { get { return Period; } }
        public override string Describe()
        {
            return "Jump · h " + height.ToString("0.##") + " · air " + airTime.ToString("0.##") + "s · " + (autoRepeat ? "repeats" : "on Jump() only");
        }

        public void Configure(float jumpHeight, float air, bool repeat)
        {
            height = jumpHeight; airTime = Mathf.Max(0.1f, air); autoRepeat = repeat;
        }

        /// <summary>Nhảy một lần ngay bây giờ (dùng khi autoRepeat tắt).</summary>
        public void Jump()
        {
            _start = ElapsedTime + TimeOffset;
            _landedRaised = false;
        }

        public override void ResetState()
        {
            base.ResetState();
            _start = -9999f;
            _landedRaised = false;
        }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            float local = autoRepeat ? t - Period * Mathf.Floor(t / Period) : t - _start;
            float total = anticipation + airTime + landing;
            if (local < 0f || local >= total) { return; }

            Vector3 sq = new Vector3(1f + squash, 1f - squash, 1f);

            if (local < anticipation)
            {
                float u = AnimEasing.Evaluate(EaseType.SineInOut, local / anticipation);
                pose.scale = Vector3.Lerp(Vector3.one, sq, u);
                return;
            }

            float air = local - anticipation;
            if (air < airTime)
            {
                float u = air / airTime;
                float y = height * 4f * u * (1f - u);
                float vy = (1f - 2f * u); // 1 lúc rời đất, -1 lúc chạm đất
                float s = Mathf.Abs(vy);
                Vector3 st = new Vector3(1f - stretch * 0.6f * s, 1f + stretch * s, 1f);
                // Chuyển mượt từ squash lúc chuẩn bị sang stretch khi bật lên
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.2f));
                pose.scale = Vector3.Lerp(st, sq, w);
                pose.position = new Vector3(travel.x * u, y + travel.y * u, 0f);
                return;
            }

            if (!_landedRaised)
            {
                _landedRaised = true;
                if (Landed != null) Landed();
            }

            float v = (local - anticipation - airTime) / landing;
            // Va chạm → squash mạnh nhất ngay lúc chạm, nảy tắt dần về 1
            float k = Mathf.Exp(-5f * v) * Mathf.Cos(2f * Mathf.PI * 1.25f * v);
            Vector3 impact = new Vector3(1f + squash * 1.1f * k, 1f - squash * 1.1f * k, 1f);
            Vector3 stLand = new Vector3(1f - stretch * 0.6f, 1f + stretch, 1f);
            float w2 = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.12f));
            pose.scale = Vector3.Lerp(impact, stLand, w2);
            pose.position = new Vector3(travel.x, travel.y, 0f);
        }
    }
}
