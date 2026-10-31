using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationToolkit
{
    [Serializable]
    public class AnimState
    {
        public string name = "Idle";
        [Tooltip("true: lặp mãi. false: chạy một lần rồi giữ pose cuối và báo StateCompleted.")]
        public bool loop = true;
        [Tooltip("Độ dài một lượt (giây) khi loop tắt. 0 = tự tính từ chu kỳ dài nhất của các animator.")]
        public float duration;
        [Tooltip("Animators that run while this state is active. All other states' animators fade out and switch off.")]
        public ToolkitAnimator[] animators;
    }

    /// <summary>
    /// Nhiều animation có tên trên một object (như danh sách animation của Spine): chọn một animation để chạy,
    /// có Loop và Time Scale. Chuyển animation có cross-fade: animation mới chạy lại từ đầu, animation cũ fade-out rồi tự tắt.
    /// Gọi từ code: stateSet.Play("Fly"). Loop tắt → chạy một lần rồi giữ pose cuối, sự kiện StateCompleted.
    /// </summary>
    [AddComponentMenu("Animation Toolkit/Anim State Set")]
    public class AnimStateSet : MonoBehaviour
    {
        [SerializeField] AnimState[] states;
        [Tooltip("Animation chạy lúc bắt đầu. Trống = animation đầu tiên.")]
        [SerializeField] string startState;
        [SerializeField, Min(0f)] float crossFade = 0.35f;
        [Tooltip("Nhân tốc độ mọi animator của animation đang chạy (như Time Scale của Spine). 1 = bình thường.")]
        [SerializeField, Min(0f)] float timeScale = 1f;

        int _index = -1;
        float _stateClock;
        bool _frozen;
        readonly Dictionary<ToolkitAnimator, float> _baseSpeed = new Dictionary<ToolkitAnimator, float>();

        /// <summary>Báo khi animation có loop=false chạy hết một lượt (tên animation).</summary>
        public event Action<string> StateCompleted;

        public string Current { get { return _index >= 0 && states != null ? states[_index].name : null; } }
        public int CurrentIndex { get { return _index; } }
        public int Count { get { return states != null ? states.Length : 0; } }
        public bool IsFrozen { get { return _frozen; } }

        public string StartState { get { return startState; } set { startState = value; } }

        public string StateName(int index) { return states[index].name; }

        public ToolkitAnimator[] StateAnimators(int index)
        {
            return states[index].animators != null ? states[index].animators : new ToolkitAnimator[0];
        }

        public bool StateLoop(int index) { return states[index].loop; }

        /// <summary>Đổi Loop của animation; nếu là animation đang chạy thì chạy lại để thấy ngay.</summary>
        public void SetStateLoop(int index, bool loop)
        {
            if (states[index].loop == loop) return;
            states[index].loop = loop;
            if (index == _index) Restart();
        }

        /// <summary>Độ dài một lượt của animation (giây thực): duration nếu đặt, nếu không thì chu kỳ dài nhất / tốc độ gốc.</summary>
        public float StateLength(int index)
        {
            if (states[index].duration > 0.01f) return states[index].duration;
            float m = 0f;
            ToolkitAnimator[] list = StateAnimators(index);
            for (int i = 0; i < list.Length; i++)
            {
                ToolkitAnimator a = list[i];
                if (a == null || a.CycleDuration <= 0f) continue;
                float sp = Mathf.Max(0.0001f, Mathf.Abs(BaseSpeed(a)));
                m = Mathf.Max(m, a.CycleDuration / sp);
            }
            return m > 0f ? m : 2f;
        }

        /// <summary>Time Scale của animation đang chạy; đổi lúc runtime có hiệu lực ngay.</summary>
        public float TimeScale
        {
            get { return timeScale; }
            set { timeScale = Mathf.Max(0f, value); if (!_frozen) ApplySpeeds(); }
        }

        float BaseSpeed(ToolkitAnimator a)
        {
            float b;
            if (!_baseSpeed.TryGetValue(a, out b)) { b = a.Speed; _baseSpeed[a] = b; }
            return b;
        }

        void ApplySpeeds()
        {
            if (states == null || _index < 0) return;
            ToolkitAnimator[] list = StateAnimators(_index);
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null) list[i].Speed = BaseSpeed(list[i]) * timeScale;
        }

        void Freeze()
        {
            _frozen = true;
            ToolkitAnimator[] list = StateAnimators(_index);
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null) { BaseSpeed(list[i]); list[i].Speed = 0f; } // thời gian dừng → giữ pose cuối
            if (StateCompleted != null) StateCompleted(states[_index].name);
        }

        void Awake()
        {
            if (states == null || states.Length == 0) return;
            // Tắt hết, rồi bật animation khởi đầu (không fade)
            for (int s = 0; s < states.Length; s++)
                if (states[s].animators != null)
                    for (int i = 0; i < states[s].animators.Length; i++)
                        if (states[s].animators[i] != null) { BaseSpeed(states[s].animators[i]); states[s].animators[i].enabled = false; }
            int start = Find(startState);
            Play(start < 0 ? 0 : start, 0f);
        }

        int Find(string stateName)
        {
            if (states == null || string.IsNullOrEmpty(stateName)) return -1;
            for (int i = 0; i < states.Length; i++)
                if (string.Equals(states[i].name, stateName, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>Trả về false nếu không có animation tên đó.</summary>
        public bool Play(string stateName) { return Play(stateName, crossFade); }

        public bool Play(string stateName, float fade)
        {
            int i = Find(stateName);
            if (i < 0) return false;
            Play(i, fade);
            return true;
        }

        /// <summary>Chạy lại animation hiện tại từ đầu.</summary>
        public void Restart() { if (_index >= 0) Play(_index, crossFade); }

        /// <summary>Chạy animation (kể cả khi đang chạy chính nó) từ đầu, không fade — dùng cho preview trong Editor.</summary>
        public void Restart(int index) { _index = -1; Play(index, 0f); }

        /// <summary>Đưa về trạng thái chưa chạy (dùng khi kết thúc preview trong Editor).</summary>
        public void ResetRuntime() { _index = -1; _frozen = false; _stateClock = 0f; _baseSpeed.Clear(); }

        public void Play(int index, float fade)
        {
            if (states == null || index < 0 || index >= states.Length) return;
            bool same = index == _index;
            if (same && !_frozen) return; // đang chạy rồi

            AnimState target = states[index];
            AnimState previous = (_index >= 0 && !same) ? states[_index] : null;

            // Animator của animation cũ không có trong animation mới → fade-out và tắt
            if (previous != null && previous.animators != null)
                for (int i = 0; i < previous.animators.Length; i++)
                {
                    ToolkitAnimator a = previous.animators[i];
                    if (a != null && a.enabled && (target.animators == null || Array.IndexOf(target.animators, a) < 0)) a.FadeOutAndDisable(fade);
                }

            _index = index;
            _frozen = false;
            _stateClock = 0f;

            // Animator của animation mới: về tốc độ gốc, chạy lại từ đầu và fade-in
            if (target.animators != null)
                for (int i = 0; i < target.animators.Length; i++)
                {
                    ToolkitAnimator a = target.animators[i];
                    if (a == null) continue;
                    bool shared = previous != null && previous.animators != null && Array.IndexOf(previous.animators, a) >= 0;
                    if (shared) continue;
                    a.Speed = BaseSpeed(a) * timeScale;
                    a.enabled = true;
                    a.ResetState();
                    a.FadeIn(fade);
                }
            ApplySpeeds();
        }

        void Update() { Tick(Time.deltaTime); }

        /// <summary>Tiến đồng hồ animation (đếm lượt cho loop=false). Public để Editor preview gọi.</summary>
        public void Tick(float dt)
        {
            if (states == null || _index < 0 || _frozen || states[_index].loop) return;
            _stateClock += dt * Mathf.Max(0.0001f, timeScale);
            if (_stateClock >= StateLength(_index)) Freeze();
        }
    }
}
