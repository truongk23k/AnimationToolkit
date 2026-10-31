using System;
using UnityEngine;

namespace AnimationToolkit
{
    [Serializable]
    public class PoseKey
    {
        [Tooltip("Time in seconds.")]
        public float time;
        [Tooltip("Position offset from the rest pose.")]
        public Vector3 position;
        [Tooltip("Euler rotation offset (degrees). Interpolated per axis, so 360 = a full spin.")]
        public Vector3 euler;
        [Tooltip("Scale multiplier.")]
        public Vector3 scale = Vector3.one;
        [Tooltip("Easing used when moving INTO this key from the previous one.")]
        public EaseType ease = EaseType.SineInOut;
    }

    /// <summary>
    /// Keyframe pose thủ công cho một Transform: đặt vài pose theo thời gian, toolkit nội suy với easing.
    /// Dùng cho hành động mà procedural không diễn tả được (gật đầu, vồ, giật mình). Offset tính từ pose gốc.
    /// Muốn loop mượt: key cuối trùng key đầu.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Pose Sequence Animator")]
    public class PoseSequenceAnimator : OffsetAnimator
    {
        [SerializeField] PoseKey[] keys = new PoseKey[]
        {
            new PoseKey { time = 0f },
            new PoseKey { time = 0.4f, position = new Vector3(0f, 0.1f, 0f), euler = new Vector3(0f, 0f, 8f) },
            new PoseKey { time = 0.8f }
        };
        [SerializeField] AnimLoopMode loopMode = AnimLoopMode.Loop;
        [Tooltip("Wait before the first play (seconds).")]
        [SerializeField, Min(0f)] float delay;

        bool _completed;

        public event Action Completed;

        float LastKeyTime { get { return keys != null && keys.Length > 0 ? keys[keys.Length - 1].time : 0f; } }
        public override bool IsLooping { get { return loopMode != AnimLoopMode.Once; } }
        public override float CycleDuration { get { return loopMode == AnimLoopMode.PingPong ? LastKeyTime * 2f : LastKeyTime; } }
        public override string Describe()
        {
            return "Pose sequence · " + (keys != null ? keys.Length : 0) + " keys · " + loopMode + " · " + LastKeyTime.ToString("0.##") + "s";
        }

        public PoseKey[] Keys { get { return keys; } set { keys = value; } }
        public AnimLoopMode LoopMode { get { return loopMode; } set { loopMode = value; } }
        public bool IsFinished { get { return _completed; } }

        /// <summary>Chạy lại từ đầu (dùng cho one-shot như mở rương).</summary>
        public void Play()
        {
            time = 0f;
            _completed = false;
            enabled = true;
        }

        public override void ResetState()
        {
            base.ResetState();
            _completed = false;
        }

        /// <summary>
        /// Keyframe mở nắp (hinge): hơi nhún ngược (anticipation) → bật mở có overshoot → lắng về góc mở.
        /// cycle &gt; 0: mở, giữ, rồi đóng lại và lặp mỗi cycle giây; cycle = 0: mở một lần rồi giữ.
        /// </summary>
        public static PoseKey[] BuildOpenKeys(float angle, float duration, float overshoot, float cycle)
        {
            float sign = angle >= 0f ? 1f : -1f;
            float a = Mathf.Abs(angle);
            float t1 = 0.12f;
            float t2 = t1 + Mathf.Max(0.1f, duration);
            float t3 = t2 + 0.2f; // lắng từ overshoot về góc mở
            System.Collections.Generic.List<PoseKey> l = new System.Collections.Generic.List<PoseKey>();
            l.Add(new PoseKey { time = 0f });
            l.Add(new PoseKey { time = t1, euler = new Vector3(0f, 0f, -sign * 6f), ease = EaseType.SineInOut });
            l.Add(new PoseKey { time = t2, euler = new Vector3(0f, 0f, sign * (a + overshoot)), ease = EaseType.CubicOut });
            l.Add(new PoseKey { time = t3, euler = new Vector3(0f, 0f, sign * a), ease = EaseType.SineInOut });
            if (cycle > t3 + 0.5f)
            {
                l.Add(new PoseKey { time = cycle - 0.4f, euler = new Vector3(0f, 0f, sign * a), ease = EaseType.Linear });
                l.Add(new PoseKey { time = cycle, ease = EaseType.CubicInOut });
            }
            return l.ToArray();
        }

        protected override void Sample(float t, float dt, ref AnimPose pose)
        {
            if (keys == null || keys.Length == 0) return;
            float duration = keys[keys.Length - 1].time;
            bool finished = false;
            float local = t - delay;
            float time;
            if (duration <= 0.0001f) { time = 0f; }
            else
            {
                float p = AnimEasing.Progress(local, duration, loopMode, out finished);
                time = p * duration;
            }
            if (finished && !_completed && local > 0f)
            {
                _completed = true;
                if (Completed != null) Completed();
            }

            if (keys.Length == 1 || time <= keys[0].time) { Apply(keys[0], ref pose); return; }
            for (int i = 1; i < keys.Length; i++)
            {
                if (time <= keys[i].time || i == keys.Length - 1)
                {
                    PoseKey a = keys[i - 1], b = keys[i];
                    float span = Mathf.Max(0.0001f, b.time - a.time);
                    float k = AnimEasing.Evaluate(b.ease, Mathf.Clamp01((time - a.time) / span));
                    pose.position = Vector3.Lerp(a.position, b.position, k);
                    pose.rotation = Quaternion.Euler(Vector3.Lerp(a.euler, b.euler, k));
                    pose.scale = Vector3.Lerp(a.scale, b.scale, k);
                    return;
                }
            }
        }

        static void Apply(PoseKey k, ref AnimPose pose)
        {
            pose.position = k.position;
            pose.rotation = Quaternion.Euler(k.euler);
            pose.scale = k.scale;
        }
    }
}
