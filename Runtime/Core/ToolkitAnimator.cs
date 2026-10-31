using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// Base cho mọi animator của toolkit. Mỗi animator là 1 "layer": nhiều animator chạy cùng lúc sẽ cộng dồn,
    /// weight 0..1 để blend vào/ra. Component bị disable thì không tốn CPU (không có Update) và tự trả pose gốc.
    /// </summary>
    public abstract class ToolkitAnimator : MonoBehaviour
    {
        [Header("Playback")]
        [Tooltip("Playback speed multiplier. 1 = normal.")]
        [SerializeField] float speed = 1f;

        [Tooltip("Layer blend weight. 0 = no effect, 1 = full effect.")]
        [SerializeField, Range(0f, 1f)] float weight = 1f;

        [Tooltip("Start offset in seconds. Use different values on sibling parts so they do not move in sync.")]
        [SerializeField] float timeOffset;

        [Tooltip("Pick a random start time on enable (avoids many characters moving identically).")]
        [SerializeField] bool randomizeStart;

        [Tooltip("Ignore Time.timeScale (e.g. for UI / paused-game idle).")]
        [SerializeField] bool unscaledTime;

        protected float time;
        float _fadeTarget;
        float _fadeSpeed;
        bool _fading;
        bool _destroyAfterFade;
        bool _disableAfterFade;

        public float Speed { get { return speed; } set { speed = value; } }
        public float Weight { get { return weight; } set { weight = Mathf.Clamp01(value); _fading = false; } }
        public float TimeOffset { get { return timeOffset; } set { timeOffset = value; } }
        public float ElapsedTime { get { return time; } }

        /// <summary>true nếu animator lặp vô hạn; false nếu chạy một lần rồi dừng/giữ pose cuối (hoặc chỉ chạy khi được gọi).</summary>
        public virtual bool IsLooping { get { return true; } }

        /// <summary>Độ dài một chu kỳ (giây, CHƯA nhân speed); 0 = không xác định (ngẫu nhiên / phản ứng liên tục).</summary>
        public virtual float CycleDuration { get { return 0f; } }

        /// <summary>Chu kỳ thực tế tính cả speed; 0 nếu không xác định.</summary>
        public float EffectiveCycle
        {
            get { return CycleDuration > 0f && Mathf.Abs(speed) > 0.0001f ? CycleDuration / Mathf.Abs(speed) : 0f; }
        }

        /// <summary>Mô tả ngắn cho Inspector/Scene view (loại + thông số chính).</summary>
        public virtual string Describe() { return GetType().Name.Replace("Animator", ""); }

        /// <summary>false cho animator ngẫu nhiên/không tuần hoàn (Blink...) — Clip baker sẽ bỏ qua.</summary>
        public virtual bool IsBakeable { get { return true; } }

        protected virtual void OnEnable()
        {
            if (randomizeStart) time = Random.Range(0f, 10f);
        }

        protected virtual void OnDisable()
        {
            ClearOffset();
        }

        void LateUpdate()
        {
            Tick(unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        /// <summary>Tiến animator thêm dt giây. Public để Editor preview và baker gọi thủ công.</summary>
        public void Tick(float dt)
        {
            UpdateFade(dt);
            time += dt * speed;
            Evaluate(time + timeOffset, dt * speed, weight);
        }

        public void SetTime(float t) { time = t; }

        /// <summary>Đưa về trạng thái đầu (thời gian, spring...) và trả pose gốc.</summary>
        public virtual void ResetState()
        {
            ClearOffset();
            time = 0f;
        }

        public void FadeTo(float targetWeight, float duration)
        {
            _destroyAfterFade = false; // một fade mới huỷ yêu cầu huỷ/tắt đang chờ của fade cũ
            _disableAfterFade = false;
            _fadeTarget = Mathf.Clamp01(targetWeight);
            if (duration <= 0.0001f) { weight = _fadeTarget; _fading = false; return; }
            _fadeSpeed = Mathf.Abs(_fadeTarget - weight) / duration;
            _fading = true;
        }

        public void FadeIn(float duration) { weight = 0f; FadeTo(1f, duration); }

        /// <summary>Fade về 0 rồi (tuỳ chọn) huỷ component — dùng khi đổi preset.</summary>
        public void FadeOut(float duration, bool destroyWhenDone)
        {
            FadeTo(0f, duration);
            _destroyAfterFade = destroyWhenDone;
            if (duration <= 0.0001f && destroyWhenDone) DestroyNow();
        }

        /// <summary>Fade về 0 rồi tắt component (enabled=false → hết tốn CPU và trả pose gốc). Dùng khi đổi trạng thái (idle ⇄ bay).</summary>
        public void FadeOutAndDisable(float duration)
        {
            FadeTo(0f, duration);
            if (duration <= 0.0001f) { enabled = false; return; }
            _disableAfterFade = true;
        }

        void UpdateFade(float dt)
        {
            if (!_fading) return;
            weight = Mathf.MoveTowards(weight, _fadeTarget, _fadeSpeed * Mathf.Abs(dt));
            if (Mathf.Approximately(weight, _fadeTarget))
            {
                _fading = false;
                if (_destroyAfterFade && _fadeTarget <= 0f) DestroyNow();
                else if (_disableAfterFade && _fadeTarget <= 0f) { _disableAfterFade = false; enabled = false; }
            }
        }

        void DestroyNow()
        {
            ClearOffset();
            if (Application.isPlaying) Destroy(this);
            else DestroyImmediate(this);
        }

        /// <summary>Tính pose tại thời điểm t (đã gồm timeOffset) và áp vào Transform.</summary>
        protected abstract void Evaluate(float t, float dt, float w);

        /// <summary>Trả mọi Transform/Sprite mà animator đã đụng về trạng thái gốc.</summary>
        public abstract void ClearOffset();

        /// <summary>Liệt kê Transform bị ảnh hưởng (cho Clip baker).</summary>
        public abstract void GetAffectedTransforms(List<Transform> results);

        /// <summary>Chỉnh tần số thành số nguyên chu kỳ trong duration để clip loop không giật.</summary>
        public virtual void SnapToLoop(float duration) { }

        protected static float SnapFrequency(float frequency, float duration)
        {
            if (duration <= 0.0001f) return frequency;
            return Mathf.Max(1f, Mathf.Round(frequency * duration)) / duration;
        }

        protected static float Cycle(float t, float frequency, float phase)
        {
            return Mathf.Sin(2f * Mathf.PI * (t * frequency + phase));
        }
    }

    /// <summary>Animator tác động lên đúng 1 Transform (mặc định chính GameObject này).</summary>
    public abstract class OffsetAnimator : ToolkitAnimator
    {
        [Header("Target")]
        [Tooltip("Transform to animate. Leave empty to animate this GameObject.")]
        [SerializeField] Transform target;

        readonly OffsetChannel _channel = new OffsetChannel();

        public Transform Target
        {
            get { return target != null ? target : transform; }
            set { target = value; }
        }

        protected sealed override void Evaluate(float t, float dt, float w)
        {
            _channel.SetTarget(Target);
            AnimPose pose = AnimPose.Identity;
            Sample(t, dt, ref pose);
            _channel.Apply(pose, w);
        }

        /// <summary>Điền offset vào pose (đã được khởi tạo Identity).</summary>
        protected abstract void Sample(float t, float dt, ref AnimPose pose);

        public override void ClearOffset() { _channel.Clear(); }

        public override void GetAffectedTransforms(List<Transform> results)
        {
            results.Add(Target);
        }
    }
}
