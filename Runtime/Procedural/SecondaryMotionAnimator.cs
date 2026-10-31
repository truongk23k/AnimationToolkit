using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Chuyển động trễ / follow-through kiểu lò xo: bộ phận này "kéo lê" theo chuyển động của source
    /// (mặc định Transform cha), vượt quá rồi dao động tắt dần. Không dùng physics.
    /// Chỉ có tác dụng khi source thực sự di chuyển/xoay (do animator khác, code, hoặc người chơi kéo).
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Secondary Motion Animator")]
    public class SecondaryMotionAnimator : OffsetAnimator
    {
        [Header("Source")]
        [Tooltip("Transform whose movement we follow. Empty = parent. Must NOT be a child of this object.")]
        [SerializeField] Transform source;

        [Header("Spring")]
        [Tooltip("Spring stiffness. Higher = snaps back faster (typical 60-300).")]
        [SerializeField, Min(1f)] float stiffness = 120f;
        [Tooltip("Damping ratio. 1 = no overshoot, 0.2-0.5 = bouncy follow-through, 0 = never settles.")]
        [SerializeField, Range(0f, 2f)] float damping = 0.35f;

        [Header("Influence")]
        [Tooltip("Positional lag per axis (world units of lag per unit of source offset). 0 disables.")]
        [SerializeField] Vector3 positionInfluence = new Vector3(1f, 1f, 0f);
        [Tooltip("Rotational lag (2D, around Z). 0 disables.")]
        [SerializeField, Range(0f, 2f)] float rotationInfluence = 0.5f;
        [Tooltip("Clamp for the position lag (world units).")]
        [SerializeField, Min(0f)] float maxLag = 0.5f;
        [Tooltip("Clamp for the rotation lag (degrees).")]
        [SerializeField, Min(0f)] float maxAngleLag = 30f;

        Vector3 _p, _v, _prevSrc;
        float _a, _av, _prevAngle;
        bool _init;

        public void Configure(float influence, float stiff, float dampingRatio)
        {
            rotationInfluence = influence; stiffness = stiff; damping = dampingRatio;
        }

        public override void ResetState()
        {
            base.ResetState();
            _init = false;
        }

        public override string Describe() { return "Spring follow-through · reacts to parent motion · stiffness " + stiffness.ToString("0") + ", damping " + damping.ToString("0.##"); }

        Transform Source
        {
            get { return source != null ? source : Target.parent; }
        }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            Transform src = Source;
            if (src == null || dt <= 0.00001f) return;

            Vector3 sp = src.position;
            float sa = src.eulerAngles.z;
            if (!_init)
            {
                _p = sp; _v = Vector3.zero; _prevSrc = sp;
                _a = sa; _av = 0f; _prevAngle = sa;
                _init = true;
            }

            // Source "teleport" (reset level, snap) → snap theo thay vì kéo lê vô lý
            if ((sp - _prevSrc).sqrMagnitude > 25f) { _p = sp; _v = Vector3.zero; }
            _prevSrc = sp;

            float k = stiffness;
            float c = 2f * damping * Mathf.Sqrt(k);
            float h = Mathf.Min(dt, 0.05f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(h / 0.008f));
            float sub = h / steps;
            float da = Mathf.DeltaAngle(_a, sa);
            for (int i = 0; i < steps; i++)
            {
                // semi-implicit Euler: ổn định với k lớn
                _v += ((sp - _p) * k - _v * c) * sub;
                _p += _v * sub;

                float angleErr = Mathf.DeltaAngle(_a, sa);
                _av += (angleErr * k - _av * c) * sub;
                _a += _av * sub;
            }

            // lag = vị trí lò xo - vị trí source (âm khi source đi tới → bộ phận bị kéo lê phía sau)
            Vector3 lag = _p - sp;
            lag = Vector3.Scale(lag, positionInfluence);
            if (lag.magnitude > maxLag) lag = lag.normalized * maxLag;
            Transform par = Target.parent;
            pose.position = par != null ? par.InverseTransformVector(lag) : lag;

            float angLag = Mathf.Clamp(Mathf.DeltaAngle(sa, _a) * rotationInfluence, -maxAngleLag, maxAngleLag);
            pose.rotation = Quaternion.Euler(0f, 0f, angLag);
        }
    }
}
