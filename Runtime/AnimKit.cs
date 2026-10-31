using UnityEngine;

namespace AnimationToolkit
{
    /// <summary>
    /// API một dòng cho hiệu ứng thường gặp, dùng được trên MỌI Transform (cá, rương, nút, chim, mèo, UI, vật phẩm...).
    /// Mỗi hàm gắn một animator đã cấu hình sẵn giá trị tự nhiên và trả về nó để chỉnh thêm.
    /// Ví dụ: AnimKit.PopIn(chest); AnimKit.Bounce(cat, 0.5f); AnimKit.Hover(coin);
    /// Chuyển động phức tạp hơn: ghép nhiều hàm trên cùng một object (các layer cộng dồn), hoặc dùng Keyframes().
    /// </summary>
    public static class AnimKit
    {
        // ── Vòng lặp nền (idle) ──

        /// <summary>Phình/xẹp nhẹ theo nhịp thở.</summary>
        public static BreathingAnimator Breathe(Transform t, float amount = 0.03f, float breathsPerSecond = 0.25f)
        {
            BreathingAnimator a = t.gameObject.AddComponent<BreathingAnimator>();
            a.Configure(amount, breathsPerSecond);
            return a;
        }

        /// <summary>Lơ lửng lên xuống và trôi nhẹ.</summary>
        public static FloatingAnimator Hover(Transform t, float height = 0.1f, float hz = 0.35f)
        {
            FloatingAnimator a = t.gameObject.AddComponent<FloatingAnimator>();
            a.Configure(height, hz);
            return a;
        }

        /// <summary>Lắc xoay qua lại quanh pivot.</summary>
        public static SwingAnimator Swing(Transform t, float degrees = 8f, float hz = 0.5f)
        {
            SwingAnimator a = t.gameObject.AddComponent<SwingAnimator>();
            a.Configure(degrees, hz, 0f, 0f);
            return a;
        }

        /// <summary>Lắc nhẹ (rotation Z) kiểu wobble.</summary>
        public static SineWaveAnimator Wobble(Transform t, float degrees = 6f, float hz = 0.8f)
        {
            SineWaveAnimator a = t.gameObject.AddComponent<SineWaveAnimator>();
            a.Configure(Vector3.zero, degrees, hz, 0f);
            return a;
        }

        /// <summary>Nhấp nháy scale (pulse) — nút kêu gọi hành động, ngôi sao, bong bóng.</summary>
        public static SineWaveAnimator Pulse(Transform t, float amount = 0.08f, float hz = 1.2f)
        {
            SineWaveAnimator a = t.gameObject.AddComponent<SineWaveAnimator>();
            a.Configure(Vector3.zero, 0f, hz, 0f);
            SetScaleAmplitude(a, new Vector3(amount, amount, 0f));
            return a;
        }

        /// <summary>Rung nhanh theo trục X (giật mình, rương khoá, lỗi).</summary>
        public static SineWaveAnimator Shake(Transform t, float distance = 0.05f, float hz = 12f)
        {
            SineWaveAnimator a = t.gameObject.AddComponent<SineWaveAnimator>();
            a.Configure(new Vector3(distance, 0f, 0f), 0f, hz, 0f);
            return a;
        }

        /// <summary>Xoay liên tục.</summary>
        public static TweenAnimator Spin(Transform t, float secondsPerTurn = 2f, bool clockwise = true)
        {
            TweenAnimator a = t.gameObject.AddComponent<TweenAnimator>();
            a.Configure(Vector3.zero, new Vector3(0f, 0f, clockwise ? -360f : 360f), Vector3.one, secondsPerTurn, EaseType.Linear, AnimLoopMode.Loop);
            return a;
        }

        /// <summary>Nhảy lặp có chuẩn bị, squash &amp; stretch và nảy khi chạm đất (pivot ở chân).</summary>
        public static JumpAnimator Bounce(Transform t, float height = 0.4f, float airTime = 0.4f, bool repeat = true)
        {
            JumpAnimator a = t.gameObject.AddComponent<JumpAnimator>();
            a.Configure(height, airTime, repeat);
            return a;
        }

        /// <summary>Bay lượn quỹ đạo elip.</summary>
        public static OrbitAnimator Orbit(Transform t, float radiusX = 1.5f, float radiusY = 0.6f, float orbitsPerSecond = 0.2f)
        {
            OrbitAnimator a = t.gameObject.AddComponent<OrbitAnimator>();
            a.Configure(new Vector2(radiusX, radiusY), orbitsPerSecond);
            return a;
        }

        // ── Một lần (one-shot) ──

        /// <summary>Hiện ra bằng cách phóng to từ fromScale lên kích thước hiện tại với overshoot (BackOut).</summary>
        public static TweenAnimator PopIn(Transform t, float duration = 0.4f, float fromScale = 0.6f)
        {
            Vector3 rest = t.localScale;
            t.localScale = rest * fromScale; // pose gốc của tween là kích thước nhỏ; kết thúc tại rest
            TweenAnimator a = t.gameObject.AddComponent<TweenAnimator>();
            a.Configure(Vector3.zero, Vector3.zero, Vector3.one * (1f / fromScale), duration, EaseType.BackOut, AnimLoopMode.Once);
            return a;
        }

        /// <summary>Di chuyển một lần theo offset (local) với easing.</summary>
        public static TweenAnimator MoveBy(Transform t, Vector3 offset, float duration = 0.5f, EaseType ease = EaseType.CubicOut)
        {
            TweenAnimator a = t.gameObject.AddComponent<TweenAnimator>();
            a.Configure(offset, Vector3.zero, Vector3.one, duration, ease, AnimLoopMode.Once);
            return a;
        }

        /// <summary>Xoay một lần thêm degrees (Z) với easing.</summary>
        public static TweenAnimator RotateBy(Transform t, float degrees, float duration = 0.5f, EaseType ease = EaseType.BackOut)
        {
            TweenAnimator a = t.gameObject.AddComponent<TweenAnimator>();
            a.Configure(Vector3.zero, new Vector3(0f, 0f, degrees), Vector3.one, duration, ease, AnimLoopMode.Once);
            return a;
        }

        // ── Tuỳ ý: keyframe pose ──

        public static PoseKey Key(float time, Vector3 position, float rotZ = 0f, float scale = 1f, EaseType ease = EaseType.SineInOut)
        {
            return new PoseKey { time = time, position = position, euler = new Vector3(0f, 0f, rotZ), scale = new Vector3(scale, scale, 1f), ease = ease };
        }

        /// <summary>
        /// Bất kỳ chuyển động nào bằng vài keyframe pose (offset từ pose gốc). Loop mượt: key cuối trùng key đầu.
        /// Ví dụ gật đầu: Keyframes(head, Loop, Key(0,zero), Key(0.3f,zero,-12), Key(0.6f,zero,0)).
        /// </summary>
        public static PoseSequenceAnimator Keyframes(Transform t, AnimLoopMode loop, params PoseKey[] keys)
        {
            PoseSequenceAnimator a = t.gameObject.AddComponent<PoseSequenceAnimator>();
            a.Keys = keys;
            a.LoopMode = loop;
            return a;
        }

        // ── Quản lý ──

        /// <summary>Dừng mọi animator của toolkit dưới root (fade mượt rồi huỷ component).</summary>
        public static void StopAll(GameObject root, float fade = 0.25f)
        {
            ToolkitAnimator[] all = root.GetComponentsInChildren<ToolkitAnimator>(true);
            for (int i = 0; i < all.Length; i++) all[i].FadeOut(fade, true);
        }

        static void SetScaleAmplitude(SineWaveAnimator a, Vector3 amp) { a.ScaleAmplitude = amp; }
    }
}
